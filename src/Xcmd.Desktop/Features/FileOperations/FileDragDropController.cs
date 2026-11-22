// @implements DragFiles
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.FileOperations;

public sealed class FileDragDropController
{
    private ShellInteractionContext context = null!;
    private Func<bool> canDrag = null!;
    private Action endPointerDrag = null!;
    private Window Owner => context.Window;
    private readonly IFileDragSource source;
    private readonly DispatcherTimer scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private WindowsFileDropTarget? target;
    private PanelView? pressedPanel;
    private FileRow? pressedRow;
    private Point pressPoint;
    private bool rightButton;
    private bool deferredSelection;
    private bool toggleSelection;
    private bool sending;
    private bool receiving;
    private FileRow? highlighted;
    private PanelView? scrollPanel;
    private int scrollDirection;
    public bool IsBusy => sending || receiving;

    public FileDragDropController(IFileDragSource? source = null) => this.source = source ?? new WindowsFileDragSource();

    internal void Attach(ShellInteractionContext context, Func<bool> canDrag, Action endPointerDrag)
    {
        this.context = context;
        this.canDrag = canDrag;
        this.endPointerDrag = endPointerDrag;
        var owner = context.Window;
        owner.Opened += (_, _) => Register();
        owner.Closed += (_, _) => { target?.Dispose(); scrollTimer.Stop(); };
        owner.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        owner.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel, handledEventsToo: true);
        owner.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel, handledEventsToo: true);
        scrollTimer.Tick += (_, _) => Scroll();
    }

    private void Register()
    {
        if (Owner.TryGetPlatformHandle() is not { HandleDescriptor: "HWND", Handle: not 0 } handle) return;
        target = new(handle.Handle, point => DestinationAt(new(point.X, point.Y)), SetReceiving, context.Ui.SetError,
            effect => { if (effect != 0) context.Ui.SetStatus("Windows processed the dropped items."); });
        try { target.Register(); }
        catch (COMException error) { context.Ui.SetError("Windows drag and drop is unavailable: " + error.Message); }
    }

    private bool CanInteract => Owner.IsEnabled && canDrag();
    private static bool Physical(string path) => path.Length > 0 && !ArchivePath.IsEntry(path) && !GitPath.IsEntry(path);

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        pressedPanel = null; pressedRow = null; deferredSelection = false;
        if (!CanInteract || sending || e.ClickCount != 1 || e.Source is not Control control) return;
        var panel = control.GetSelfAndVisualAncestors().OfType<PanelView>().FirstOrDefault();
        var row = control.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault()?.DataContext as FileRow;
        var buttons = e.GetCurrentPoint(Owner).Properties;
        if (panel?.EditingRow != null || panel == null || row == null || row.Entry.IsParent || !Physical(row.Entry.FullPath) ||
            (!buttons.IsLeftButtonPressed && !buttons.IsRightButtonPressed)) return;
        pressedPanel = panel; pressedRow = row; pressPoint = e.GetPosition(Owner); rightButton = buttons.IsRightButtonPressed;
        DeferSelection(panel, row, e);
    }

    private void DeferSelection(PanelView panel, FileRow row, PointerPressedEventArgs e)
    {
        if (!rightButton && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && panel.Table.SelectedItems?.Contains(row) == true)
        {
            deferredSelection = true;
            toggleSelection = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            context.Activate(panel); panel.FocusDragRow(row);
            e.Handled = true;
        }
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (pressedPanel is not { } panel || pressedRow is not { } row || sending) return;
        var buttons = e.GetCurrentPoint(Owner).Properties;
        if (!(rightButton ? buttons.IsRightButtonPressed : buttons.IsLeftButtonPressed)) { pressedPanel = null; return; }
        var delta = e.GetPosition(Owner) - pressPoint;
        var size = source.DragSize;
        if (Math.Abs(delta.X) * Owner.RenderScaling < size.Width && Math.Abs(delta.Y) * Owner.RenderScaling < size.Height) return;
        pressedPanel = null; pressedRow = null;
        if (!CanInteract) return;
        var selection = panel.Table.SelectedItems?.OfType<FileRow>().Where(item => !item.Entry.IsParent).ToArray() ?? [];
        var rows = selection.Contains(row) ? selection : [row];
        if (rows.Any(item => !Physical(item.Entry.FullPath))) return;
        panel.CancelShellPress();
        e.Pointer.Capture(null); e.Handled = true;
        sending = true;
        try
        {
            var effect = source.Drag(rows.Select(item => item.Entry.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), rightButton);
            if (effect != 0) context.Ui.SetStatus("Windows processed the dragged items.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or COMException)
        { context.Ui.SetError("Drag and drop: " + error.Message); }
        finally
        {
            sending = false; deferredSelection = false; endPointerDrag();
            SetReceiving(false); panel.FocusCurrent(scrollIntoView: false);
        }
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (deferredSelection && pressedPanel is { } panel && pressedRow is { } row && e.InitialPressMouseButton == MouseButton.Left)
        { panel.SelectClickedRow(row, toggleSelection); e.Handled = true; }
        pressedPanel = null; pressedRow = null; deferredSelection = false;
    }

    public string? DestinationAt(PixelPoint point)
    {
        Highlight(null); scrollPanel = null; scrollDirection = 0;
        if (!CanInteract) return null;
        foreach (var panel in context.Panels)
        {
            if (panel.ShowsAgentSessions || panel.IsSearchResults || panel.EditingRow != null || !Physical(panel.Session.DirectoryPath)) continue;
            var local = panel.Table.PointToClient(point);
            if (!new Rect(panel.Table.Bounds.Size).Contains(local) || local.Y < UiPalette.RowHeight) continue;
            var control = panel.Table.InputHitTest(local) as Control;
            if (control?.GetSelfAndVisualAncestors().OfType<ScrollBar>().Any() == true) return null;
            return DestinationInPanel(panel, local, control);
        }
        return null;
    }

    private string? DestinationInPanel(PanelView panel, Point local, Control? control)
    {
        scrollPanel = panel;
        scrollDirection = local.Y < UiPalette.RowHeight * 2 ? -1 : local.Y > panel.Table.Bounds.Height - UiPalette.RowHeight ? 1 : 0;
        var row = control?.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault()?.DataContext as FileRow;
        if (row?.Entry.IsDirectory != true) return panel.Session.DirectoryPath;
        Highlight(row);
        return Physical(row.Entry.FullPath) ? row.Entry.FullPath : null;
    }

    private void SetReceiving(bool value)
    {
        receiving = value; context.RecordActivity();
        if (value) scrollTimer.Start();
        else { scrollTimer.Stop(); scrollPanel = null; Highlight(null); }
    }

    private void Highlight(FileRow? row)
    {
        if (highlighted == row) return;
        highlighted?.SetDropTarget(false); highlighted = row; highlighted?.SetDropTarget(true);
    }

    private void Scroll()
    {
        if (scrollDirection == 0 || scrollPanel == null) return;
        var scroll = scrollPanel.Table.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scroll != null) scroll.Offset = new(scroll.Offset.X, Math.Clamp(scroll.Offset.Y + scrollDirection * UiPalette.RowHeight, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
    }
}

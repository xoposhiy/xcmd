// @implements CreateArchive, ShellActions
using Avalonia;
using Avalonia.Controls;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.FileOperations;

public sealed class ShellInteractionController
{
    private readonly IShellInteraction shellInteraction;
    private readonly FileDragDropController fileDragDrop;
    public ShellInteractionController() : this(null, null) { }
    public ShellInteractionController(IShellInteraction? interaction, FileDragDropController? dragDrop = null)
    {
        shellInteraction = interaction ?? new WindowsShellInteraction();
        fileDragDrop = dragDrop ?? new();
    }
    public bool IsDragBusy => fileDragDrop.IsBusy;
    private ShellInteractionContext context = null!;
    private Func<bool> canOpenMenu = null!;
    private ZipPackingController packing = null!;
    public bool IsMenuOpen { get; private set; }

    internal void Attach(ShellInteractionContext context, ZipPackingController packing, Func<bool> canOpenMenu,
        Func<bool> canDrag, Action endPointerDrag)
    {
        this.context = context;
        this.packing = packing;
        this.canOpenMenu = canOpenMenu;
        fileDragDrop.Attach(context, canDrag, endPointerDrag);
        Win32Properties.AddWndProcHookCallback(context.Window, ShellMessage);
        foreach (var panel in context.Panels) panel.ShellMenuRequested += OpenShellMenu;
    }

    private nint ShellMessage(nint window, uint message, nint wParam, nint lParam, ref bool handled)
    {
        if (shellInteraction.HandleMessage(message, wParam, lParam, out var result)) handled = true;
        return result;
    }

    internal void OpenCurrentMenu(PanelView panel)
    {
        if (panel.Current is not { } row) return;
        panel.FocusCurrent();
        var container = panel.Table.ContainerFromIndex(panel.Rows.IndexOf(row));
        var point = container != null
            ? container.PointToScreen(new Point(48, container.Bounds.Height))
            : panel.Table.PointToScreen(new Point(48, 32));
        OpenShellMenu(panel, row, point, extended: false);
    }

    private void OpenShellMenu(PanelView panel, FileRow row, PixelPoint point, bool extended)
    {
        if (!canOpenMenu() || IsMenuOpen) return;
        context.Activate(panel);
        context.Ui.Execute(() =>
        {
            if (!CanUseShell(row)) return Task.CompletedTask;
            var selection = panel.Table.SelectedItems?.OfType<FileRow>().Where(item => !item.Entry.IsParent).ToArray() ?? [];
            var items = selection.Contains(row) ? new[] { row }.Concat(selection.Where(item => item != row)).ToArray() : [row];
            if (items.Any(item => !CanUseShell(item))) return Task.CompletedTask;
            var paths = items.Select(item => item.Entry.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
            { context.Ui.SetError("Windows group menus require files from one folder. Select items in the same folder or right-click an unmarked item."); return Task.CompletedTask; }
            IsMenuOpen = true;
            ShellMenuAction action;
            try { action = shellInteraction.ShowContextMenu(paths, context.Window.TryGetPlatformHandle()?.Handle ?? 0, point.X, point.Y, extended); }
            finally { IsMenuOpen = false; context.RecordActivity(); panel.FocusCurrent(scrollIntoView: false); }
            if (action == ShellMenuAction.AddToZip) packing.Open(panel, items.Select(item => item.Entry).ToArray());
            return Task.CompletedTask;
        });
    }

    internal void OpenFileProperties(PanelView panel)
    {
        if (panel.Current is not { } row) return;
        context.Ui.Execute(() =>
        {
            if (CanUseShell(row)) shellInteraction.ShowProperties(row.Entry.FullPath, context.Window.TryGetPlatformHandle()?.Handle ?? 0);
            return Task.CompletedTask;
        });
    }

    private bool CanUseShell(FileRow row)
    {
        if (row.Entry.IsParent) return false;
        if (!ArchivePath.IsEntry(row.Entry.FullPath)) return true;
        context.Ui.SetError(GitPath.IsEntry(row.Entry.FullPath)
            ? "Windows shell actions are unavailable in Git history. F3 views the selected file."
            : "Windows shell actions are unavailable inside archives. Extract the item first.");
        return false;
    }
}

internal sealed record ShellInteractionContext(Window Window, IReadOnlyList<PanelView> Panels,
    Action<PanelView> Activate, UiActions Ui, Action RecordActivity);

// @implements DragFiles, ShellActions
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    public event Action<PanelView, FileRow, PixelPoint, bool>? ShellMenuRequested;
    private FileRow? shellPressedRow;
    internal void CancelShellPress() => shellPressedRow = null;
    internal void FocusDragRow(FileRow row) => selection.FocusPointerRow(row);
    internal void SelectClickedRow(FileRow row, bool toggle) => selection.SelectClickedRow(row, toggle);

    private void AttachShellInput()
    {
        Table.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(Table).Properties.IsRightButtonPressed) return;
            shellPressedRow = null;
            if (EditingRow != null || e.Source is not Control control) return;
            var row = control.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault()?.DataContext as FileRow;
            if (row == null) return;
            Activated?.Invoke(this);
            selection.FocusPointerRow(row);
            shellPressedRow = row;
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        Table.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Right || shellPressedRow is not { } row) return;
            shellPressedRow = null;
            e.Handled = true;
            if (e.Source is not Control control || control.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault()?.DataContext != row) return;
            ShellMenuRequested?.Invoke(this, row, Table.PointToScreen(e.GetPosition(Table)), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }, RoutingStrategies.Tunnel);
    }
}

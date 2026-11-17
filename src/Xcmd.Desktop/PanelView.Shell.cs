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
    internal void FocusDragRow(FileRow row) { cursorName = row.Name; FocusCurrent(scrollIntoView: false); }
    internal void SelectClickedRow(FileRow row, bool toggle)
    {
        keyboardMarks = toggle; rangeAnchor = null;
        if (toggle) Table.SelectedItems?.Remove(row);
        else { Table.SelectedItems?.Clear(); Table.SelectedItem = row; }
        FocusDragRow(row);
    }

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
            cursorName = row.Name;
            FocusCurrent(scrollIntoView: false);
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

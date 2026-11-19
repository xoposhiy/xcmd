// @implements DragFiles, ShellActions
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Xcmd.Desktop;

internal sealed class PanelShellInput
{
    private FileRow? pressedRow;
    public event Action<FileRow, PixelPoint, bool>? MenuRequested;

    public void CancelPress() => pressedRow = null;

    public PanelShellInput(TableView table, PanelSelection selection, Func<bool> isEditing, Action activate)
    {
        table.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(table).Properties.IsRightButtonPressed) return;
            pressedRow = null;
            if (isEditing() || e.Source is not Control control) return;
            var row = control.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault()?.DataContext as FileRow;
            if (row == null) return;
            activate();
            selection.FocusPointerRow(row);
            pressedRow = row;
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        table.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Right || pressedRow is not { } row) return;
            pressedRow = null;
            e.Handled = true;
            if (e.Source is not Control control || control.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault()?.DataContext != row) return;
            MenuRequested?.Invoke(row, table.PointToScreen(e.GetPosition(table)), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }, RoutingStrategies.Tunnel);
    }
}

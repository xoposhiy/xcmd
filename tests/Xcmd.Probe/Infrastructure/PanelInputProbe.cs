// @tests NavigatePanels, SortEntries, RenameEntry
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Desktop;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class PanelInputProbe
{
    public static async Task SelectAsync(this ProbeContext context, string name, PanelView? panel = null)
    {
        panel ??= context.Window.ActivePanel;
        if (context.Window.ActivePanel != panel) await context.KeyAsync(Key.Tab);
        await context.KeyAsync(Key.Home, RawInputModifiers.Control);
        var index = panel.Rows.FindIndex(row => row.Name == name);
        Check(index >= 0, "Fixture item exists: " + name);
        for (var i = 0; i < index; i++) await context.KeyAsync(Key.Down);
        Equal(name, panel.Current?.Name, "Keyboard cursor");
    }

    public static Task ClickHeaderAsync(this ProbeContext context, string field, PanelView? panel = null)
    {
        panel ??= context.Window.ActivePanel;
        return context.ClickAsync(panel.Table.GetVisualDescendants().OfType<Button>().Single(button => button.Name == panel.Id + "Sort" + field));
    }

    public static Control Row(this ProbeContext context, string name, PanelView? panel = null)
    {
        panel ??= context.Window.ActivePanel;
        var index = panel.Rows.FindIndex(row => row.Name == name);
        panel.Table.ScrollIntoView(index);
        panel.Table.UpdateLayout();
        return panel.Table.ContainerFromIndex(index) ?? throw new InvalidOperationException("The row is not realized.");
    }

    public static async Task BeginRenameAsync(this ProbeContext context, string name)
    {
        await context.SelectAsync(name);
        await context.KeyAsync(Key.F6, RawInputModifiers.Shift);
        Check(context.Window.ActivePanel.EditingRow != null, "Rename mode is active.");
        var editor = context.Window.ActivePanel.RenameEditor;
        Check(editor?.IsFocused == true, "The inline editor has keyboard focus.");
        Equal(name, editor?.SelectedText, "The full name, including the extension, is selected.");
    }
}

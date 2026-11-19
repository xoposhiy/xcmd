// @tests ReusePaths, ComparePanelFiles, TransferEntries, DuplicateEntry
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Xcmd.Probe.Infrastructure;

public static class MainMenuProbe
{
    public static async Task InvokeFileCommandAsync(ProbeContext context, string title)
    {
        await context.ClickAsync(FileMenu(context));
        await context.ClickAsync(FileCommand(context, title));
    }

    public static MenuItem FileCommand(ProbeContext context, string title) =>
        FileMenu(context).Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == title);

    private static MenuItem FileMenu(ProbeContext context) =>
        context.Window.GetVisualDescendants().OfType<Menu>().Single(control => control.Name == "MainMenu")
            .Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "File");
}

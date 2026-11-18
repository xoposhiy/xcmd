// @tests ReusePaths, ComparePanelFiles
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Xcmd.Probe.Infrastructure;

public static class MainMenuProbe
{
    public static async Task InvokeFileCommandAsync(ProbeContext context, string title)
    {
        var menu = context.Window.GetVisualDescendants().OfType<Menu>().Single(control => control.Name == "MainMenu");
        var file = menu.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "File");
        await context.ClickAsync(file);
        await context.ClickAsync(file.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == title));
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Xcmd.Desktop;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class FeedbackRemarksProbe
{
    public static CheckBox Option(FileSearchWindow window, string name) => window.GetVisualDescendants().OfType<CheckBox>().Single(control => control.Name == name);
    public static Button Button(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(control => control.Name == name);

    public static void PhysicalShortcut(ProbeContext context, Key key, PhysicalKey physical, string? symbol)
    {
        context.Input.KeyDown(key, RawInputModifiers.Control, physical, symbol);
        context.Input.KeyUp(key, RawInputModifiers.Control, physical, symbol);
    }

    public static async Task CheckNativeIconsAsync(ProbeContext context)
    {
        var panels = new[] { context.Window.Left, context.Window.Right };
        foreach (var panel in panels) await panel.Icons.Completion;
        var icons = panels.SelectMany(panel => panel.Table.GetVisualDescendants().OfType<Image>()).Where(image => image.Source is WriteableBitmap).ToArray();
        Check(icons.Length > 0, "Real Windows icons are loaded");
        foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d, 1.5d, 1d })
        {
            context.Window.SetScaleForTest(scale); await context.SettleAsync();
            foreach (var panel in panels) await panel.Icons.Completion;
            await context.SettleAsync();
            foreach (var image in icons)
            {
                var bitmap = (WriteableBitmap)image.Source!;
                Equal(16d, image.Width, "DPI changes preserve the logical icon width");
                Equal(16d, image.Height, "DPI changes preserve the logical icon height");
                Equal(16d, image.Bounds.Width, "The actual icon layout stays inside its slot");
                Equal(16d, image.Bounds.Height, "The actual icon height stays inside its row");
                Equal((int)Math.Ceiling(16 * scale), bitmap.PixelSize.Width, "Windows renders the exact physical width for the display scale");
                Equal((int)Math.Ceiling(16 * scale), bitmap.PixelSize.Height, "Windows renders the exact physical height for the display scale");
            }
            if (scale == 1.5) await context.ScreenshotAsync("native-icons-150");
        }
        await context.ScreenshotAsync("native-icons-100");
    }

    public static void CheckCompactResults(FileSearchWindow window)
    {
        var rows = window.Results.GetVisualDescendants().OfType<TableViewRow>().ToArray();
        Check(rows.Length > 0 && rows.All(row => row.Bounds.Height <= 28), "Search results use compact single-line rows");
        var row = rows[0];
        var path = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SearchResultPath");
        var preview = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SearchResultPreview");
        Check(path.FontSize >= 14, "Search paths use a readable font");
        Check(preview.TranslatePoint(default, window)!.Value.X > path.TranslatePoint(default, window)!.Value.X + path.Bounds.Width, "The excerpt occupies a separate right column");
    }
}

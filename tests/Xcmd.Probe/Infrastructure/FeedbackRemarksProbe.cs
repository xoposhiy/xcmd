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
        await context.Window.Left.Icons.Completion;
        var icons = context.Window.Left.Table.GetVisualDescendants().OfType<Image>().Where(image => image.Source is WriteableBitmap).ToArray();
        Check(icons.Length > 0, "Real Windows icons are loaded");
        foreach (var scale in new[] { 1d, 1.5d })
        {
            context.Window.SetScaleForTest(scale); await context.SettleAsync(); await context.Window.Left.Icons.Completion; await context.SettleAsync();
            foreach (var image in icons)
            {
                var bitmap = (WriteableBitmap)image.Source!;
                Check(bitmap.PixelSize.Width >= Math.Ceiling(16 * scale), "Icons use a native source large enough for the display scale");
                Check(Math.Abs(image.Width * scale - bitmap.PixelSize.Width) < 0.01, "Icons use one source pixel per display pixel");
                Check(Math.Abs(image.Height * scale - bitmap.PixelSize.Height) < 0.01, "Icon height keeps its native pixel size");
            }
        }
        await context.ScreenshotAsync("native-icons-150");
        context.Window.SetScaleForTest(1);
    }

    public static void CheckCompactResults(FileSearchWindow window)
    {
        var rows = window.Results.GetVisualDescendants().OfType<TableViewRow>().ToArray();
        Check(rows.Length > 0 && rows.All(row => row.Bounds.Height <= 27), "Search results use compact single-line rows");
        var row = rows[0];
        var path = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SearchResultPath");
        var preview = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SearchResultPreview");
        Check(path.FontSize >= 14, "Search paths use a readable font");
        Check(preview.TranslatePoint(default, window)!.Value.X > path.TranslatePoint(default, window)!.Value.X + path.Bounds.Width, "The excerpt occupies a separate right column");
    }
}

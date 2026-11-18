// @tests OpenViewers, HighlightSource, ReadLargeFiles
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using SkiaSharp;
using Xcmd.Desktop;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

internal static class SyntaxDragProbe
{
    public const string SampleLine = "public const string Name = \"cafe\u0301 😀\"; // sample\r\n";

    public static async Task<FileViewerWindow> OpenAsync(ProbeContext context)
    {
        var path = Path.Combine(context.LeftPath, "Sample.cs");
        await File.WriteAllTextAsync(path, string.Concat(Enumerable.Repeat(SampleLine, 400)));
        return await SyntaxProbe.OpenAsync(context, path, new ViewerFiles());
    }

    public static double Drag(FileViewerWindow viewer, int from, int to)
    {
        var presenter = viewer.Reader.TextContent.GetVisualDescendants().OfType<TextPresenter>().Single();
        Point At(int position)
        {
            var rect = presenter.TextLayout.HitTestTextPosition(position);
            return presenter.TranslatePoint(new Point(rect.X + 0.1, rect.Center.Y), viewer)!.Value;
        }

        var start = At(from);
        var end = At(to);
        viewer.MouseDown(start, MouseButton.Left);
        var layout = presenter.TextLayout;
        var times = new List<double>();
        for (var step = 1; step <= 20; step++)
        {
            var timer = Stopwatch.StartNew();
            viewer.MouseMove(start + (end - start) * (step / 20d), RawInputModifiers.LeftMouseButton);
            viewer.UpdateLayout();
            times.Add(timer.Elapsed.TotalMilliseconds);
            ProbeContext.Check(ReferenceEquals(layout, presenter.TextLayout), "Moving the selection reuses the page layout");
        }
        viewer.MouseUp(end, MouseButton.Left);
        return times.Order().ElementAt(times.Count / 2);
    }

    public static async Task CheckSelectionColorAsync(ProbeContext context, FileViewerWindow viewer)
    {
        var presenter = viewer.Reader.TextContent.GetVisualDescendants().OfType<TextPresenter>().Single();
        await context.ScreenshotAsync("syntax-drag", viewer);
        using var image = SKBitmap.Decode(Path.Combine(context.OutputPath, "syntax-drag.png"));
        var color = ((ISolidColorBrush)presenter.SelectionForegroundBrush!).Color;
        var expected = new SKColor(color.R, color.G, color.B);
        var origin = presenter.TranslatePoint(default, viewer)!.Value;
        var scale = viewer.RenderScaling;
        var word = presenter.TextLayout.HitTestTextRange(0, "public".Length).Single();
        var bounds = PixelRect.FromRect(word.Translate(new Vector(origin.X, origin.Y)), scale);
        for (var y = bounds.Y; y < bounds.Bottom; y++)
            for (var x = bounds.X; x < bounds.Right; x++)
                if (image.GetPixel(x, y) == expected) return;
        throw new InvalidOperationException("The selected keyword must use the selection foreground.");
    }
}

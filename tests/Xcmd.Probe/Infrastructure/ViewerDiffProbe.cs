// @tests ReadWorkingFileDiff
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using SkiaSharp;
using Xcmd.Desktop;

namespace Xcmd.Probe.Infrastructure;

public static class ViewerDiffProbe
{
    public static async Task<IDisposable> InitializeAsync(ProbeContext context, params string[] tracked)
    {
        await GitFixture.InitializeAsync(context.LeftPath);
        await GitFixture.RunAsync(context.LeftPath, "config", "user.name", "Probe");
        await GitFixture.RunAsync(context.LeftPath, "config", "user.email", "probe@example.test");
        if (tracked.Length > 0)
        {
            await GitFixture.RunAsync(context.LeftPath, ["add", "--", .. tracked]);
            await GitFixture.RunAsync(context.LeftPath, "commit", "-m", "Fixture");
        }
        return new RepositoryCleanup(context.LeftPath);
    }

    public static async Task<FileViewerWindow> OpenAsync(ProbeContext context, string name, string? directory = null)
    {
        await context.Window.Left.LoadAsync(directory ?? context.LeftPath);
        await context.SelectAsync(name, context.Window.Left);
        await context.KeyAsync(Key.F3);
        return context.Window.Viewer!;
    }

    public static TextBlock GitStatus(Control reader) => reader.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "ViewerGitStatus");

    public static void CheckGitState(FileViewerWindow viewer, string expected)
    {
        var status = GitStatus(viewer);
        ProbeContext.Equal("Git: " + expected, status.Text, "The status bar identifies the Git state");
        var point = status.TranslatePoint(default, viewer);
        ProbeContext.Check(status.IsVisible && status.Bounds.Height > 0 && point is { X: >= 0 and <= 16 } position
            && position.Y + status.Bounds.Height <= viewer.ClientSize.Height, "The Git status is visible at the start of the status bar");
    }

    public static string Text(FileViewerWindow viewer) => viewer.Reader.TextContent.Text ?? "";

    public static Task ToggleAsync(ProbeContext context, FileViewerWindow viewer) => context.KeyAsync(Key.D, RawInputModifiers.Control, viewer);

    public static async Task ToggleWithPhysicalKeyAsync(ProbeContext context, FileViewerWindow viewer)
    {
        var input = new InputDriver(viewer);
        input.KeyDown(Key.None, RawInputModifiers.Control, PhysicalKey.D, "в");
        input.KeyUp(Key.None, RawInputModifiers.Control, PhysicalKey.D, "в");
        await context.SettleAsync();
    }

    public static async Task ReleaseDiffKeyAsync(ProbeContext context, Window window)
    {
        new InputDriver(window).KeyUp(Key.D, RawInputModifiers.Control, PhysicalKey.D, "d");
        await context.SettleAsync();
    }

    public static void CheckRenderedSyntax(FileViewerWindow viewer)
    {
        var presenter = viewer.Reader.TextContent.GetVisualDescendants().OfType<TextPresenter>().Single();
        var colors = presenter.TextLayout.TextLines.SelectMany(line => line.TextRuns)
            .Select(run => run.Properties?.ForegroundBrush).OfType<ISolidColorBrush>().Select(brush => brush.Color);
        ProbeContext.Check(colors.Distinct().Count() >= 3, "The diff shows distinct syntax colors in the actual text layout");
    }

    public static async Task ShowFromMenuAsync(ProbeContext context, FileViewerWindow viewer)
    {
        await context.ClickAsync((MenuItem)viewer.Reader.Commands.Items[1]!, target: viewer);
        await context.ClickAsync(ViewerProbe.Command(viewer, "viewer.diff"), target: viewer);
    }

    public static async Task CaptureLightThemeAsync(ProbeContext context, FileViewerWindow viewer)
    {
        await AppearanceProbe.ChooseThemeAsync(context, viewer, "github");
        viewer.SetScaleForTest(1.5);
        viewer.Width = viewer.MinWidth;
        await context.ScreenshotAsync("viewer-git-diff-light-150", viewer);
    }

    public static async Task<SKColor[]> ReadBackgroundsAsync(ProbeContext context, FileViewerWindow viewer, string screenshot, params int[] positions)
    {
        await context.ScreenshotAsync(screenshot, viewer);
        using var image = SKBitmap.Decode(Path.Combine(context.OutputPath, screenshot + ".png"));
        var presenter = viewer.Reader.TextContent.GetVisualDescendants().OfType<TextPresenter>().Single();
        var scale = viewer.RenderScaling;
        return positions.Select(position =>
        {
            var rect = presenter.TextLayout.HitTestTextPosition(position);
            var point = presenter.TranslatePoint(new Point(rect.X + 1, rect.Bottom - 2), viewer)!.Value;
            return image.GetPixel((int)(point.X * scale), (int)(point.Y * scale));
        }).ToArray();
    }

    public static async Task HoverLineAsync(ProbeContext context, FileViewerWindow viewer, string text)
    {
        var box = viewer.Reader.TextContent;
        var position = Text(viewer).IndexOf(text, StringComparison.Ordinal);
        ProbeContext.Check(position >= 0, "The displayed line can be hovered");
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().Single();
        var bounds = presenter.TextLayout.HitTestTextPosition(position);
        viewer.MouseMove(presenter.TranslatePoint(new Point(bounds.X + 4, bounds.Y + 5), viewer)!.Value);
        await FileOperationProbe.WaitUntilAsync(() => ToolTip.GetIsOpen(box));
        await context.SettleAsync();
    }

    private sealed class RepositoryCleanup(string path) : IDisposable
    {
        public void Dispose() => GitFixture.AllowCleanup(path);
    }
}

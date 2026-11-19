// @tests ChooseViewMode, ReadLargeFiles, FindViewerText, OpenExternalTool, ReportControl, ExternalLaunch, SharedControls
using System.Diagnostics;
using System.Text;
using SkiaSharp;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public static class ViewerProbe
{
    public static Button Button(FileViewerWindow viewer, string name) => viewer.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    public static MenuItem Command(FileViewerWindow viewer, string name) => Command(viewer.Reader, name);

    public static MenuItem Command(FileViewer reader, string name) => reader.Commands.Items.OfType<MenuItem>()
        .SelectMany(menu => menu.Items.OfType<MenuItem>()).Single(item => item.Name == name);

    public static void CheckTextActions(FileViewerWindow viewer, bool enabled)
    {
        foreach (var id in new[] { "copy", "select-all", "find", "find-next", "find-previous", "clear-search", "first-page", "last-page" })
            ProbeContext.Equal(enabled, Command(viewer, "viewer." + id).IsEnabled, "Text actions apply only to loaded text and byte views");
    }

    public static void CheckFeedbackHint(FileViewerWindow viewer, bool visible)
    {
        var texts = viewer.GetVisualDescendants().OfType<TextBlock>().ToArray();
        var hint = texts.Single(text => text.Name == "ViewerFeedbackStatus");
        ProbeContext.Equal(visible, hint.IsVisible, "Feedback guidance appears in the active viewer");
        ProbeContext.Equal(!visible, texts.Single(text => text.Name == "ViewerReadingStatus").IsVisible, "Reading status returns after feedback ends");
        if (visible) ProbeContext.Check(hint.Text!.Contains("Ctrl+click", StringComparison.Ordinal), "Feedback guidance explains how to use controls");
    }

    public static double TextHeight(FileViewerWindow viewer) => viewer.Reader.TextContent.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().Single().TextLayout.Height;

    public static async Task StartSearchAsync(ProbeContext c, FileViewerWindow viewer, string text)
    {
        var input = new InputDriver(viewer);
        var key = Enum.Parse<Key>(text[..1].ToUpperInvariant());
        input.KeyDown(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.None, null);
        input.KeyDown(key, RawInputModifiers.Alt, PhysicalKey.None, text[..1]);
        input.KeyUp(key, RawInputModifiers.Alt, PhysicalKey.None, text[..1]);
        input.KeyUp(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.None, null);
        await c.TextAsync(text[1..], viewer);
    }

    public static async Task WheelAsync(ProbeContext c, FileViewerWindow viewer, double delta)
    {
        var point = viewer.Reader.TextContent.TranslatePoint(new Point(100, 100), viewer)!.Value;
        viewer.MouseMove(point); viewer.MouseWheel(point, new Vector(0, delta));
        await c.SettleAsync();
    }

    public static async Task ChooseEncodingAsync(ProbeContext c, FileViewerWindow viewer, int index)
    {
        await c.ClickAsync((MenuItem)viewer.Reader.Commands.Items[2]!, target: viewer);
        for (var i = 0; i <= index; i++) await c.KeyAsync(Key.Down, target: viewer);
        await c.KeyAsync(Key.Enter, target: viewer);
    }

    public static void CheckReadingLayout(FileViewerWindow viewer)
    {
        ProbeContext.Check(!viewer.Reader.IsSearchVisible && !viewer.Reader.IsInformationVisible, "Search and file details stay hidden while reading");
        ProbeContext.Check(!viewer.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "DialogFeedback" || button.Name?.StartsWith("Viewer", StringComparison.Ordinal) == true && button.Name is not ("ViewerMinimize" or "ViewerMaximize" or "ViewerCloseWindow")), "Reading has no action toolbar");
        ProbeContext.Check(viewer.Reader.TextContent.Bounds.Height >= viewer.ClientSize.Height - viewer.Reader.TitleBar.Bounds.Height - 28, "File contents fill the space between the title bar and compact status");
        var border = viewer.Reader.TextContent.GetVisualDescendants().OfType<Border>().Single(item => item.Name == "PART_BorderElement");
        ProbeContext.Equal(new Thickness(0), border.BorderThickness, "Reading content has no accent outline");
    }

    public static void CheckTitleBar(FileViewerWindow viewer)
    {
        ProbeContext.Equal(WindowDecorations.BorderOnly, viewer.WindowDecorations, "The native title bar is replaced by the compact header");
        ProbeContext.Check(viewer.ExtendClientAreaToDecorationsHint, "The compact header uses the window decoration area");
        ProbeContext.Equal(WindowDecorationsElementRole.TitleBar, Avalonia.Controls.Chrome.WindowDecorationProperties.GetElementRole(viewer.Reader.TitleBar), "The header supports native window dragging");
        AppearanceProbe.CheckHeaderInput(viewer, viewer.Reader.TitleBar);
        var progress = viewer.Reader.Progress.TranslatePoint(default, viewer)!.Value;
        ProbeContext.Check(progress.Y < 32 && progress.X > viewer.ClientSize.Width - 220, "Reading percentage is at the top right near the scroll bar");
    }

    public static void WriteOversizedImage(string path)
    {
        using var stream = File.Create(path);
        stream.Write(ImageSample()); stream.SetLength(ViewerFiles.MaximumImageBytes + 1L);
    }

    public static byte[] ImageSample()
    {
        using var surface = SKSurface.Create(new SKImageInfo(640, 360));
        surface.Canvas.Clear(new SKColor(22, 34, 54));
        using var paint = new SKPaint { Color = new SKColor(60, 140, 220), IsAntialias = true };
        surface.Canvas.DrawRect(60, 60, 240, 240, paint);
        paint.Color = new SKColor(120, 220, 180); surface.Canvas.DrawCircle(430, 180, 100, paint);
        using var image = surface.Snapshot(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static void WriteLargeFile(string path, long length)
    {
        using var stream = File.Create(path); stream.SetLength(length);
    }

    public static async Task MeasureOpenAsync(ProbeContext c, string name)
    {
        await c.SelectAsync(name);
        var timer = Stopwatch.StartNew();
        await c.KeyAsync(Avalonia.Input.Key.F3);
        c.Measurements["openToRenderedViewMs"] = timer.Elapsed.TotalMilliseconds;
        c.Measurements["renderedCharacters"] = c.Window.Viewer!.Reader.TextContent.Text?.Length ?? 0;
    }

    public static async Task VerifyEditorBoundaryAsync(ProbeContext c)
    {
        var fixture = CommandProbe.Prepare(c);
        await File.WriteAllTextAsync(Path.Combine(fixture.Directory, "editor-probe.mode"), "");
        var path = Path.Combine(c.LeftPath, "file & Unicode 日本語.txt");
        await File.WriteAllTextAsync(path, "Editor sample");
        var request = await new ExternalEditor(fixture.Executable).PrepareAsync(path, c.LeftPath);
        await new WindowsExternalLauncher(showWindows: false).LaunchAsync(request);
        var report = await CommandProbe.ReadReportAsync(fixture.Report);
        ProbeContext.Equal(path, report.Arguments.Single(), "The real Windows launch passes one literal file argument");
        ProbeContext.Equal(c.LeftPath, report.WorkingDirectory, "The editor receives the source directory");
    }

    public static byte[] LegacySample(int codePage)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(codePage).GetBytes(string.Concat(Enumerable.Repeat("\u042D\u0442\u043E \u0442\u0435\u043A\u0441\u0442 \u0434\u043B\u044F \u043F\u0440\u043E\u0432\u0435\u0440\u043A\u0438 \u043A\u043E\u0434\u0438\u0440\u043E\u0432\u043A\u0438.\r\n", 20)));
    }
}

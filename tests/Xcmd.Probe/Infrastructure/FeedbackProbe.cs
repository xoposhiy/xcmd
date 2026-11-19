// @tests ReportControl, KeepFeedback, FeedbackIdentity, HeadlessVerification
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SkiaSharp;
using Xcmd.Core;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class FeedbackProbe
{
    public static bool ReleaseKeyWhileDialogCloses(ProbeContext context, Window dialog)
    {
        var released = false;
        void Observe(object? sender, KeyEventArgs args) => released |= args.Key == Key.LeftShift;
        context.Window.AddHandler(InputElement.KeyUpEvent, Observe, RoutingStrategies.Tunnel, handledEventsToo: true);
        try
        {
            dialog.KeyPress(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, null);
            Dispatcher.UIThread.Post(dialog.Close);
            context.ReleaseInput(dialog, window => window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null));
            return released;
        }
        finally { context.Window.RemoveHandler(InputElement.KeyUpEvent, Observe); }
    }

    public static T Control<T>(Window window, string id) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.GetValue(Xcmd.Desktop.FeedbackTarget.IdProperty) == id);

    public static async Task CheckHighlightAsync(ProbeContext context, Window window, Control target, string screenshot)
    {
        window.UpdateLayout();
        var topLeft = target.TranslatePoint(default, window)!.Value;
        var middle = topLeft + new Vector(target.Bounds.Width / 2, target.Bounds.Height / 2);
        var pointerSource = "no routed move";
        EventHandler<PointerEventArgs> observe = (_, e) => pointerSource = e.Source?.GetType().Name ?? "no source";
        window.AddHandler(InputElement.PointerMovedEvent, observe, RoutingStrategies.Tunnel, handledEventsToo: true);
        try { window.MouseMove(middle); }
        finally { window.RemoveHandler(InputElement.PointerMovedEvent, observe); }
        await context.ScreenshotAsync(screenshot, window);
        using var image = SKBitmap.Decode(Path.Combine(context.OutputPath, screenshot + ".png"));
        var scale = window.RenderScaling;
        var edge = image.GetPixel((int)((topLeft.X + 1) * scale), (int)(middle.Y * scale));
        var hits = string.Join(", ", window.GetVisualsAt(middle).Take(10).Select(visual => visual.GetType().Name));
        Check(edge == SKColors.Gold, $"The yellow outline is rendered over the hovered control. Edge pixel: {edge}. Pointer source: {pointerSource}. Outline visible: {HasHighlight(window)}. Hits: {hits}.");
    }

    public static bool HasHighlight(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Any(border =>
            border.GetValue(Xcmd.Desktop.FeedbackTarget.IdProperty) == "FeedbackSurface.highlight" && border.IsEffectivelyVisible);

    public static IDisposable WithAgentTools(string directory)
    {
        var tools = Path.Combine(directory, "agent tools");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, "codex.cmd"), "@exit /b 0");
        File.WriteAllText(Path.Combine(tools, "claude.cmd"), "@exit /b 0");
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", tools + Path.PathSeparator + previousPath);
        return new RestorePath(previousPath);
    }

    private sealed class RestorePath(string? path) : IDisposable
    {
        public void Dispose() => Environment.SetEnvironmentVariable("PATH", path);
    }

    public static TextBlock Text(Window window, string value) => window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == value && !text.GetVisualAncestors().OfType<TextBox>().Any());

    public static async Task DoubleClickAsync(ProbeContext context, Window window, Control control)
    {
        window.UpdateLayout();
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        for (var i = 0; i < 2; i++) { window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); }
        await context.SettleAsync();
    }

    public static void SelectImmediately(Window window, Control control)
    {
        window.UpdateLayout();
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
    }

    public static async Task UseControlAsync(ProbeContext context, Window window, Control control,
        RawInputModifiers modifier = RawInputModifiers.Control, MouseButton button = MouseButton.Left)
    {
        window.UpdateLayout();
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, button, modifier);
        window.MouseUp(point, button, modifier);
        await context.SettleAsync();
    }

    public static async Task<FeedbackIssue> CommentAsync(ProbeContext context, Window window, Control element, string expectedName)
    {
        await context.KeyAsync(Key.F12, target: window);
        await context.ClickAsync(element, target: window);
        var comment = context.Window.Feedback.ActiveComment;
        Check(comment != null, "Clicking a visible element opens a comment.");
        await context.TextAsync("Feedback selection check", comment);
        await context.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
        var note = context.Window.Feedback.Notes.Last();
        Equal(expectedName, note.Target.Name, "Feedback identifies the clicked element instead of its window");
        Check(!string.IsNullOrWhiteSpace(note.Target.Id), "Every clicked target has a searchable source identity.");
        return note;
    }
}

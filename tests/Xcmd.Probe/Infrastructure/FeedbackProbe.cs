// @tests ReportControl, KeepFeedback, FeedbackIdentity
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Headless;
using Xcmd.Core;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class FeedbackProbe
{
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

    public static async Task UseControlAsync(ProbeContext context, Window window, Control control)
    {
        window.UpdateLayout();
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.Control);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.Control);
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

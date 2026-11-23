// @tests InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

internal sealed class InspectorProbe(ProbeContext c)
{
    internal Window Window => c.Window.SessionInspector ?? throw new InvalidOperationException("The session inspector is closed.");
    internal T Find<T>(Func<T, bool> match) where T : Control => Window.GetVisualDescendants().OfType<T>().First(match);
    internal bool HasText(string text) => Window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains(text) == true);
    internal Task Ready(string text) => WorkspaceProbe.WaitAsync(() => HasText(text));
    internal Task Click(string caption) => c.ClickAsync(Find<Button>(button => Equals(button.Content, caption)), target: Window);
    internal Task Key(Key key) => c.KeyAsync(key, target: Window);
    internal Task Screenshot(string file) => c.ScreenshotAsync(file, Window);
    internal void FocusTimeline() => Find<Control>(control => control.GetType().Name == "SessionTimeline").Focus();
    internal async Task DragRange(double from, double to)
    {
        var timeline = Find<Control>(control => control.GetType().Name == "SessionTimeline");
        var top = timeline.TranslatePoint(new Point(0, 0), Window)!.Value;
        Point At(double x) => new(top.X + 112 + x * (timeline.Bounds.Width - 124), top.Y + timeline.Bounds.Height - 10);
        var driver = new InputDriver(Window);
        driver.PressPointer(At(from), MouseButton.Left);
        driver.MovePointer(At(to));
        driver.ReleasePointer(At(to), MouseButton.Left);
        await c.SettleAsync();
    }
    internal async Task ChooseResource(string identifier)
    {
        var text = Find<TextBlock>(t => t.Text == identifier);
        var button = text.FindAncestorOfType<Button>()!;
        button.BringIntoView();
        await c.SettleAsync();
        await c.ClickAsync(button, target: Window);
    }

    internal async Task PickFeedback()
    {
        await Key(Avalonia.Input.Key.F12);
        Check(c.Window.Feedback.IsActive, "Feedback can select controls in the inspector");
        await c.ClickAsync(Find<TextBlock>(t => t.Text == "Session timeline"), target: Window);
        Check(c.Window.Feedback.ActiveComment != null, "The timeline heading can receive feedback");
        c.Window.Feedback.ActiveComment!.Close();
        c.Window.Feedback.Cancel();
    }

    internal async Task Reveal(string caption)
    {
        var button = Find<Button>(b => Equals(b.Content, caption));
        button.BringIntoView();
        await c.SettleAsync();
    }

    internal async Task OpenAllResources(string caption)
    {
        await Reveal(caption);
        await Click(caption);
    }

    internal static Task WaitForCancelledRead(AgentSessionFixture sessions)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sessions.Inspector = async (_, token) =>
        {
            using var stop = token.Register(() => cancelled.TrySetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("A cancelled read cannot return a trace.");
        };
        return cancelled.Task;
    }

    internal async Task CheckTokenSteps()
    {
        var start = Find<Slider>(s => AutomationProperties.GetName(s) == "Range start");
        start.Focus();
        var before = start.Value;
        await Key(Avalonia.Input.Key.Right);
        Check(start.Value > before, "One key moves to the first completed result");
        var first = start.Value;
        await Key(Avalonia.Input.Key.Right);
        Check(start.Value > first, "The next key reaches the next result");
    }
}

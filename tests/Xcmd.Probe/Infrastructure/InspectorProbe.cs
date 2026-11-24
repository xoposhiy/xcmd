// @tests InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Xcmd.Desktop.Shared.Ui;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

internal sealed class InspectorProbe(ProbeContext c)
{
    internal Window Window => c.Window.SessionInspector ?? throw new InvalidOperationException("The session inspector is closed.");
    internal T Find<T>(Func<T, bool> match) where T : Control => Window.GetVisualDescendants().OfType<T>().First(match);
    internal bool HasText(string text) => Window.GetVisualDescendants().Any(control => control switch
    {
        TextBlock block => block.Text?.Contains(text) == true,
        TextBox box => box.Text?.Contains(text) == true,
        _ => false
    });
    internal Task Ready(string text) => WorkspaceProbe.WaitAsync(() => HasText(text));
    internal Task Click(string caption) => ClickVisible(Find<Button>(button => Equals(button.Content, caption)));
    internal Task Key(Key key) => c.KeyAsync(key, target: Window);
    internal Task Screenshot(string file) => c.ScreenshotAsync(file, Window);
    internal async Task CheckTableLayout(double width, bool sideBySide)
    {
        Window.Width = width;
        await c.SettleAsync();
        var resources = PanelBounds("Touched resources");
        var activities = PanelBounds("Activity breakdown");
        if (sideBySide)
            Check(activities.Right < resources.Left && activities.Top == resources.Top, "Activities sit to the left of resources");
        else
            Check(activities.Bottom < resources.Top && activities.Left == resources.Left, "Activities sit above resources in a narrow window");

        Rect PanelBounds(string title)
        {
            var panel = Find<TextBlock>(t => t.Text == title).FindAncestorOfType<Border>()!;
            return new Rect(panel.TranslatePoint(default, Window)!.Value, panel.Bounds.Size);
        }
    }

    internal double ActivityRowTop(string label) =>
        Find<TextBlock>(t => Equals(ToolTip.GetTip(t), label)).TranslatePoint(default, Window)!.Value.Y;
    internal void FocusTimeline() => Find<Control>(control => control.GetType().Name == "SessionTimeline").Focus();
    internal Border RangeHandle(string name) => Find<Border>(b => AutomationProperties.GetName(b) == name);
    internal string? MetricValue(string name) =>
        Find<TextBlock>(t => t.Text == name).FindAncestorOfType<StackPanel>()!.Children.OfType<TextBlock>().ElementAt(1).Text;
    internal void CheckRangeCalls(int count)
    {
        var totals = Find<TextBlock>(t => AutomationProperties.GetAutomationId(t) == "session-inspector.range-totals");
        var expected = $"{count:N0} {(count == 1 ? "call" : "calls")} · ";
        Check(totals.Text?.StartsWith(expected, StringComparison.Ordinal) == true,
            $"Selected range reports {count} completed calls");
    }

    internal bool AxisSelected(string name) => AutomationProperties.GetItemStatus(AxisLink(name)) == "Selected";

    private HyperlinkButton AxisLink(string name) => Find<HyperlinkButton>(b => Equals(b.Content, name));

    internal void CheckTimelineHeader()
    {
        var title = Find<TextBlock>(t => t.Text == "Session timeline");
        var time = AxisLink("Time");
        var cost = AxisLink("API cost");
        Check(title.Parent == time.Parent && time.Parent == cost.Parent &&
            title.Bounds.Right <= time.Bounds.Left && time.Bounds.Right <= cost.Bounds.Left,
            "The two axis links sit directly after the title");
        Check(!title.FindAncestorOfType<Border>()!.GetVisualDescendants().OfType<Image>().Any(),
            "The timeline does not repeat its action symbols in a legend");
    }
    internal string? ActivityCalls(string label) => Find<TextBlock>(t => Equals(ToolTip.GetTip(t), label))
        .FindAncestorOfType<Grid>()!.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 3).Text;
    internal void CheckActivityNumbers(string label, string cost, string calls)
    {
        var row = Find<TextBlock>(t => Equals(ToolTip.GetTip(t), label)).FindAncestorOfType<Grid>()!;
        var cells = row.Children.OfType<TextBlock>().ToDictionary(Grid.GetColumn);
        Equal(cost, cells[2].Text, "The share is in the cost column");
        Equal(calls, cells[3].Text, "The completed tool call count has its own column");
        Equal(cost + " API cost", ToolTip.GetTip(cells[2]), "The share has an explicit currency and unit");
        Equal(calls + " completed tool calls", ToolTip.GetTip(cells[3]), "The call count has an explicit unit");
        var edge = cells[2].TranslatePoint(new Point(cells[2].Bounds.Width, 0), row)!.Value.X;
        var separator = row.Children.OfType<Border>().Single(b => b.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.activityCallsSeparator");
        Check(edge < separator.Bounds.Left && separator.Bounds.Right < cells[3].Bounds.Left,
            "The divider stays between the visible cost and call count");
    }
    internal Task SortActivities(string column) => ClickVisible(Find<Button>(b => Equals(ToolTip.GetTip(b), "Sort by " + column)));
    internal CheckBox CombineActionsOption => Find<CheckBox>(b => Equals(b.Content, "Combine actions by target"));
    internal Task CombineActions() => ClickVisible(CombineActionsOption);

    internal Button ResourceFilterButton(string field) => Find<Button>(b => AutomationProperties.GetName(b) == "Resource " + field);

    internal Task OpenResourceFilter(string field) => ClickVisible(ResourceFilterButton(field));

    private IEnumerable<CheckBox> ResourceFilterChoices() => Find<StackPanel>(panel =>
        panel.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.resourceFilterList").Children.OfType<CheckBox>();

    internal CheckBox ResourceFilterChoice(string option) => ResourceFilterChoices().Single(check => Equals(check.Content, option));

    internal Task ToggleResourceOption(string option) => c.ClickAsync(ResourceFilterChoice(option), target: Window);

    internal async Task CloseResourceFilter()
    {
        await Key(Avalonia.Input.Key.Escape);
        Check(!Window.GetVisualDescendants().OfType<StackPanel>().Any(p =>
            p.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.resourceFilterList"), "Escape closes only the filter menu");
    }

    internal async Task FilterResources(string field, params string[] options)
    {
        await OpenResourceFilter(field);
        var all = ResourceFilterChoice("All");
        if (options.Length == 0 && all.IsChecked != true) await ToggleResourceOption("All");
        if (options.Length > 0 && all.IsChecked == true) await ToggleResourceOption("All");
        foreach (var check in ResourceFilterChoices().Where(check => check != all && options.Length > 0))
            if (options.Contains((string)check.Content!) != check.IsChecked)
                await c.ClickAsync(check, target: Window);
        await CloseResourceFilter();
    }

    internal async Task CheckCallCopy()
    {
        var field = Find<TextBox>(box => AutomationProperties.GetName(box) == "Tool call details");
        await ClickVisible(field);
        Check(field.IsReadOnly, "The details field protects the recorded call");
        await c.KeyAsync(Avalonia.Input.Key.Home, RawInputModifiers.Control, Window);
        for (var i = 0; i < 4; i++) await c.KeyAsync(Avalonia.Input.Key.Right, RawInputModifiers.Shift, Window);
        await c.KeyAsync(Avalonia.Input.Key.C, RawInputModifiers.Control, Window);
        Equal(field.Text![..4], await Window.Clipboard!.TryGetTextAsync(), "Ctrl+C copies only the selected characters");
        await c.KeyAsync(Avalonia.Input.Key.A, RawInputModifiers.Control, Window);
        await c.KeyAsync(Avalonia.Input.Key.C, RawInputModifiers.Control, Window);
        Equal(field.Text.ReplaceLineEndings(), (await Window.Clipboard!.TryGetTextAsync())?.ReplaceLineEndings(), "Ctrl+A and Ctrl+C copy every call section");
    }

    private Point RangePoint(double fraction)
    {
        var timeline = Find<Control>(control => control.GetType().Name == "SessionTimeline");
        var endHandle = RangeHandle("Range end");
        var width = timeline.Bounds.Width - 112 - endHandle.Bounds.Width - 2;
        return timeline.TranslatePoint(new Point(112 + fraction * width, endHandle.Bounds.Center.Y), Window)!.Value;
    }

    internal async Task DragRange(double from, double to)
    {
        var driver = new InputDriver(Window);
        driver.PressPointer(RangePoint(from), MouseButton.Left);
        driver.MovePointer(RangePoint(to));
        driver.ReleasePointer(RangePoint(to), MouseButton.Left);
        await c.SettleAsync();
    }

    internal Point TimelinePoint(double fraction, int track, double rowY)
    {
        var timeline = Find<Control>(control => control.GetType().Name == "SessionTimeline");
        var origin = timeline.TranslatePoint(default, Window)!.Value;
        return new(RangePoint(fraction).X, origin.Y + 3 * 30 + track * 16 + rowY);
    }

    internal async Task<string?> TimelineTip(double fraction, int track, double rowY = 8)
    {
        new InputDriver(Window).MovePointer(TimelinePoint(fraction, track, rowY));
        await c.SettleAsync();
        return ToolTip.GetTip(Find<Control>(control => control.GetType().Name == "SessionTimeline")) as string;
    }

    internal async Task OpenTimelineCall(double fraction, int track = 0, double rowY = 8)
    {
        var timeline = Find<Control>(control => control.GetType().Name == "SessionTimeline");
        timeline.BringIntoView();
        await c.SettleAsync();
        var driver = new InputDriver(Window);
        var point = TimelinePoint(fraction, track, rowY);
        driver.PressPointer(point, MouseButton.Left);
        driver.ReleasePointer(point, MouseButton.Left);
        await Ready(AxisSelected("API cost") ? "Model response" : "Tool call details");
    }

    internal async Task DragHandle(string name, double to, Action whileHeld)
    {
        var handle = RangeHandle(name);
        var center = new Point(handle.Bounds.Width / 2, handle.Bounds.Height / 2);
        var press = handle.TranslatePoint(center, Window)!.Value;
        var target = RangePoint(to) + new Vector(name == "Range start" ? -center.X : center.X, 0);
        var driver = new InputDriver(Window);
        driver.PressPointer(press, MouseButton.Left);
        try
        {
            driver.MovePointer(target);
            await c.SettleAsync();
            whileHeld();
        }
        finally { driver.ReleasePointer(target, MouseButton.Left); }
        await c.SettleAsync();
    }
    private async Task ClickVisible(Control control)
    {
        control.BringIntoView();
        await c.SettleAsync();
        await c.ClickAsync(control, target: Window);
    }

    private Button ResourceButton(string identifier) =>
        Find<Button>(b => AutomationProperties.GetName(b)?.StartsWith(identifier + ",", StringComparison.Ordinal) == true);

    internal Task ChooseResource(string identifier) =>
        ClickVisible(((Grid)ResourceButton(identifier).Content!).Children.OfType<TextBlock>().Single(text => Grid.GetColumn(text) == 0));

    internal async Task<string?> ResourceInterval(string identifier, double fraction, bool click)
    {
        var plot = ResourceButton(identifier).GetVisualDescendants().OfType<Control>()
            .Single(control => control.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.resourceTimeline");
        plot.BringIntoView();
        await c.SettleAsync();
        var point = plot.TranslatePoint(new Point(plot.Bounds.Width * fraction, plot.Bounds.Height / 2), Window)!.Value;
        var driver = new InputDriver(Window);
        driver.MovePointer(point);
        if (click)
        {
            driver.PressPointer(point, MouseButton.Left);
            driver.ReleasePointer(point, MouseButton.Left);
        }
        await c.SettleAsync();
        return ToolTip.GetTip(plot) as string;
    }
    internal double ResourceRowTop(string identifier) => ResourceButton(identifier).TranslatePoint(default, Window)!.Value.Y;

    internal Task ToggleGroup(string action) => ClickVisible(Find<Button>(b => AutomationProperties.GetName(b) == action));
    internal async Task ToggleKind(string name)
    {
        await OpenResourceFilter("type");
        await ToggleResourceOption(name);
        await CloseResourceFilter();
    }
    internal bool VisibleText(string value) =>
        Window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == value && t.IsEffectivelyVisible);

    internal async Task PickFeedback()
    {
        await Key(Avalonia.Input.Key.F12);
        Check(c.Window.Feedback.IsActive, "Feedback can select controls in the inspector");
        await c.ClickAsync(Find<TextBlock>(t => t.Text == "Session timeline"), target: Window);
        Check(c.Window.Feedback.ActiveComment != null, "The timeline heading can receive feedback");
        c.Window.Feedback.ActiveComment!.Close();
        c.Window.Feedback.Cancel();
    }

    internal async Task CheckAxisLinkFeedback()
    {
        var link = AxisLink("Time");
        link.BringIntoView();
        await c.SettleAsync();
        var note = await FeedbackProbe.CommentAsync(c, Window, link, Window.Title + ": Time");
        Equal("SessionInspector.axisLink", note.Target.Id, "The Time link keeps its own feedback identity");
        Check(AxisSelected("API cost"), "Picking the link leaves the selected axis alone");
    }

    internal async Task CheckResourceFilterFeedback()
    {
        var filter = ResourceFilterButton("action");
        filter.BringIntoView();
        await c.SettleAsync();
        await Key(Avalonia.Input.Key.F12);
        await FeedbackProbe.UseControlAsync(c, Window, filter);
        var execute = ResourceFilterChoice("Execute");
        await c.ClickAsync(execute, target: Window);
        var comment = c.Window.Feedback.ActiveComment;
        Check(comment != null, "The checkbox in the open filter can receive feedback");
        Check(execute.IsChecked == true, "Selecting the checkbox for feedback leaves the filter state alone");
        await c.TextAsync("Action filter option", comment);
        await c.KeyAsync(Avalonia.Input.Key.Enter, RawInputModifiers.Control, comment);
        var target = c.Window.Feedback.Notes.Last().Target;
        Equal("SessionInspector.resourceKind", target.Id, "The report identifies the checkbox source");
        Check(target.Name.StartsWith("Resource action:", StringComparison.Ordinal) && target.Name.EndsWith(": Execute", StringComparison.Ordinal),
            "The report names the filter and the selected checkbox");
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

    internal async Task CheckCostSteps()
    {
        var start = RangeHandle("Range start");
        FocusTimeline();
        await Key(Avalonia.Input.Key.Tab);
        Check(start.IsFocused, "Tab reaches the start handle from the timeline");
        var before = start.Bounds.X;
        await Key(Avalonia.Input.Key.Right);
        Check(start.Bounds.X > before, "One key moves to the first response boundary");
        var first = start.Bounds.X;
        await Key(Avalonia.Input.Key.Right);
        Check(start.Bounds.X > first, "The next key reaches the next response boundary");
        await Key(Avalonia.Input.Key.Tab);
        var end = RangeHandle("Range end");
        Check(end.IsFocused, "Tab reaches the end handle from the start handle");
        var last = end.Bounds.X;
        await Key(Avalonia.Input.Key.Left);
        Check(end.Bounds.X < last, "The end handle moves to the preceding response boundary");
    }
}

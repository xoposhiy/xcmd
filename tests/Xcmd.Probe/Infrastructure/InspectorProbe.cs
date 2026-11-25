// @tests InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.LogicalTree;
using Avalonia.Media;
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

    internal async Task CheckSessionId(Guid sessionId)
    {
        var value = Find<TextBlock>(text => text.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.sessionIdValue");
        var copy = Find<Button>(button => AutomationProperties.GetName(button) == "Copy session ID");
        var icon = (PathIcon)copy.Content!;
        var copyGlyph = icon.Data;
        var expected = sessionId.ToString("D");
        Equal(expected, value.Text, "The session summary shows the full session ID");
        Check(copy.Bounds.Width <= 24 && copy.BorderThickness == default && Equals(copy.Background, Brushes.Transparent),
            "The copy button shows a compact icon without a border or background");
        var right = copy.TranslatePoint(new Point(copy.Bounds.Width, 0), Window)!.Value.X;
        Check(right <= Window.Bounds.Width && value.Bounds.Right <= copy.Bounds.Left, "The full ID and copy action fit in the window");
        var clipboard = Window.Clipboard!;
        await clipboard.SetTextAsync("Previous clipboard text");
        await ClickVisible(copy);
        await WorkspaceProbe.WaitAsync(() => AutomationProperties.GetItemStatus(copy) == "Session ID copied");
        Check(icon.Data != copyGlyph, "The icon confirms that the ID was copied");
        Equal(expected, await clipboard.TryGetTextAsync(), "Clicking Copy places the exact ID on the clipboard");
        await WorkspaceProbe.WaitAsync(() => icon.Data == copyGlyph);
    }

    internal async Task CheckSessionIdFeedback()
    {
        var copy = Find<Button>(button => AutomationProperties.GetName(button) == "Copy session ID");
        copy.BringIntoView();
        await c.SettleAsync();
        var note = await FeedbackProbe.CommentAsync(c, Window, copy, Window.Title + ": Copy session ID");
        Equal("SessionInspector.copySessionId", note.Target.Id, "Feedback identifies the session ID copy button");
    }

    internal async Task CheckSummaryFeedback()
    {
        const string summaryId = "SessionInspector.sessionSummary";
        var summary = FeedbackProbe.Control<Border>(Window, summaryId);
        var panel = await FeedbackProbe.CommentAsync(c, Window, summary, Window.Title + ": " + summaryId,
            new Point(0.995, 0.5));
        Equal(summaryId, panel.Target.Id, "The summary border reports its own source ID");
        var label = Find<TextBlock>(t => t.Text == "Model");
        var heading = await FeedbackProbe.CommentAsync(c, Window, label, Window.Title + ": Model");
        Equal("SessionInspector.modelLabel", heading.Target.Id, "The model label has its own source ID");
        var value = Find<TextBlock>(t => t.Text == "codex-fixture");
        var model = await FeedbackProbe.CommentAsync(c, Window, value, Window.Title + ": codex-fixture");
        Equal("SessionInspector.modelValue", model.Target.Id, "The model value has a different source ID");
    }

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

    internal async Task<Window> OpenToolCall()
    {
        await ClickVisible(ToolCallLink);
        await WorkspaceProbe.WaitAsync(() => Window.OwnedWindows.Any(dialog => dialog.Title == "Tool call" && dialog.IsVisible));
        return Window.OwnedWindows.Single(dialog => dialog.Title == "Tool call");
    }

    private HyperlinkButton ToolCallLink => Find<HyperlinkButton>(button =>
        button.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.viewToolCall");

    internal string? ToolCallLinkName => ((TextBlock)ToolCallLink.Content!).Text;

    internal static TextBox ToolCallPayload(Window dialog, string name) => dialog.GetVisualDescendants().OfType<TextBox>()
        .Single(box => AutomationProperties.GetName(box) == name);

    internal static string? ToolCallField(Window dialog, string field) => dialog.GetVisualDescendants().OfType<SelectableTextBlock>()
        .Single(text => text.GetValue(FeedbackTarget.IdProperty) == $"SessionInspector.{field}").Text;

    internal static void CheckToolCallTabs(Window dialog)
    {
        var tabs = dialog.GetVisualDescendants().OfType<TabControl>().Single();
        Equal("Input,Output", string.Join(',', tabs.Items.OfType<TabItem>().Select(item => item.Header)),
            "The window shows the selected call's input and output");
        Check(!dialog.GetVisualDescendants().OfType<Button>().Any(button => Equals(button.Content, "Close")),
            "The tool call window uses the system close control");
    }

    internal async Task SelectCallTab(Window dialog, string name)
    {
        var tab = dialog.GetVisualDescendants().OfType<TabItem>().Single(item => Equals(item.Header, name));
        await c.ClickAsync(tab, target: dialog);
    }

    internal async Task CloseToolCall(Window dialog)
    {
        await c.KeyAsync(Avalonia.Input.Key.Escape, target: dialog);
        await WorkspaceProbe.WaitAsync(() => !dialog.IsVisible);
        Check(Window.IsVisible && Window.FocusManager?.GetFocusedElement() == ToolCallLink,
            "Escape closes the dialog and restores focus to the tool name link");
    }

    internal async Task CheckCallCopy(Window dialog)
    {
        var field = ToolCallPayload(dialog, "Output");
        await c.ClickAsync(field, target: dialog);
        Check(field.IsReadOnly, "The details field protects the recorded call");
        await c.KeyAsync(Avalonia.Input.Key.Home, RawInputModifiers.Control, dialog);
        for (var i = 0; i < 4; i++) await c.KeyAsync(Avalonia.Input.Key.Right, RawInputModifiers.Shift, dialog);
        await c.KeyAsync(Avalonia.Input.Key.C, RawInputModifiers.Control, dialog);
        Equal(field.Text![..4], await dialog.Clipboard!.TryGetTextAsync(), "Ctrl+C copies only the selected characters");
        await c.KeyAsync(Avalonia.Input.Key.A, RawInputModifiers.Control, dialog);
        await c.KeyAsync(Avalonia.Input.Key.C, RawInputModifiers.Control, dialog);
        Equal(field.Text.ReplaceLineEndings(), (await dialog.Clipboard!.TryGetTextAsync())?.ReplaceLineEndings(), "Ctrl+A and Ctrl+C copy the full payload");
    }

    internal void CheckCallCardLayout(bool sideBySide)
    {
        var facts = Find<Border>(control => control.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.callFactsPanel");
        var activity = Find<StackPanel>(control => control.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.callActivity");
        Check(sideBySide ? facts.Bounds.Right <= activity.Bounds.Left : facts.Bounds.Bottom <= activity.Bounds.Top,
            "The technical panel sits to the left of the activity at wide widths and above it at narrow widths");
    }

    internal bool HasCallResourceDisclosure => Window.GetVisualDescendants().OfType<Button>().Any(button =>
        button.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.callResourceDisclosure");

    private IEnumerable<TextBlock> CallResourceLabels() => Window.GetVisualDescendants().OfType<TextBlock>().Where(text =>
        text.IsEffectivelyVisible && text.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.callResourceName");

    internal string CallResourceNames() => string.Join(' ', CallResourceLabels().Select(label => label.Text));

    internal void CheckResourceNamesShareLine(string first, string second)
    {
        var labels = CallResourceLabels().ToArray();
        var left = labels.Single(text => text.Text == first).TranslatePoint(default, Window)!.Value;
        var right = labels.Single(text => text.Text == second).TranslatePoint(default, Window)!.Value;
        Check(right.X > left.X && Math.Abs(right.Y - left.Y) < 1, "The files share one comma-separated line");
    }

    internal void CheckResourceGroupsInViewport()
    {
        var scroll = Find<ScrollViewer>(control => control.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.scroll");
        var groups = scroll.GetVisualDescendants().OfType<Border>().Where(control =>
            control.IsEffectivelyVisible && control.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.callResourceGroupPanel").ToArray();
        Check(groups.Length > 1 && groups.All(group =>
        {
            var top = group.TranslatePoint(default, scroll)!.Value.Y;
            return top >= -1 && top + group.Bounds.Height <= scroll.Viewport.Height + 1;
        }), "All resource groups fit inside the viewport after scrolling");
    }

    internal string? CallCardText(string field) =>
        Find<TextBlock>(control => control.GetValue(FeedbackTarget.IdProperty) == $"SessionInspector.{field}").Text;

    private TextBlock SpanCallNumber(int position) => Find<TextBlock>(control =>
        control.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.callPickerNumber" && control.Text == position.ToString("N0"));

    internal Task SelectSpanCall(int position) => ClickVisible(SpanCallNumber(position));

    internal string? SpanCallDuration(int position) => ((Grid)SpanCallNumber(position).Parent!).Children.OfType<TextBlock>()
        .Single(text => text.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.callPickerDuration").Text;

    internal async Task ShowCallCardInViewport()
    {
        Find<Border>(control => control.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.callCard").BringIntoView();
        await c.SettleAsync();
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
        return TimelineTipContent() switch
        {
            Control control => AutomationProperties.GetName(control),
            string text => text,
            _ => null
        };
    }

    private object? TimelineTipContent() => ToolTip.GetTip(Find<Control>(control => control.GetType().Name == "SessionTimeline"));

    internal Task WaitForTimelineTooltip() => WorkspaceProbe.WaitAsync(() =>
        TimelineTipContent() is Control tip && TopLevel.GetTopLevel(tip) != null);

    internal string TimelineTipText() => string.Join('\n', ((Control)TimelineTipContent()!).GetLogicalDescendants()
        .OfType<TextBlock>().Select(text => text.Text));

    internal string TimelineTipActions(string target)
    {
        var tip = (Panel)TimelineTipContent()!;
        var row = tip.GetLogicalDescendants().OfType<StackPanel>().Single(panel =>
            panel.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.timelineTipTargetActions" &&
            panel.Children.OfType<TextBlock>().Single().Text == target);
        return string.Join(',', row.Children.OfType<PathIcon>().Select(AutomationProperties.GetName));
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
        await c.SettleAsync();
        await Ready("Selected span");
    }

    internal async Task ClickTimelineSpan(double fraction, bool task, MouseButton button, int clicks = 1)
    {
        var timeline = Find<Control>(control => control.GetType().Name == "SessionTimeline");
        timeline.BringIntoView();
        await c.SettleAsync();
        var origin = timeline.TranslatePoint(default, Window)!.Value;
        var point = new Point(RangePoint(fraction).X, origin.Y + (task ? 45 : 75));
        var driver = new InputDriver(Window);
        driver.MovePointer(point);
        for (var i = 0; i < clicks; i++)
        {
            driver.PressPointer(point, button);
            driver.ReleasePointer(point, button);
        }
        await c.SettleAsync();
    }

    internal bool HasZoomAction(string text) => Window.GetVisualDescendants().OfType<Button>()
        .Any(button => Equals(button.Content, text) && button.IsEnabled);

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
        var comment = c.Window.Feedback.ActiveComment;
        Check(comment != null, "The timeline heading can receive feedback");
        await c.TextAsync("Timeline title", comment);
        await c.KeyAsync(Avalonia.Input.Key.Enter, RawInputModifiers.Control, comment);
        Equal("SessionInspector.timelineTitle", c.Window.Feedback.Notes.Last().Target.Id, "The timeline heading reports its own source ID");
    }

    internal async Task CheckAxisLinkFeedback()
    {
        foreach (var (axis, id) in new[] { ("Time", "SessionInspector.timeAxis"), ("API cost", "SessionInspector.costAxis") })
        {
            var link = AxisLink(axis);
            link.BringIntoView();
            await c.SettleAsync();
            var note = await FeedbackProbe.CommentAsync(c, Window, link, Window.Title + ": " + axis);
            Equal(id, note.Target.Id, $"The {axis} link has a distinct feedback identity");
            Check(AxisSelected("API cost"), "Picking the link leaves the selected axis alone");
        }
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

// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class InspectorOverview
{
    private void RenderSummary()
    {
        metrics.Children.Clear();
        Metric(0, "Wall time", Duration(selection.To - selection.From), Duration(summary.OutsideTools) + " outside tools");
        Metric(1, "Tool time", Duration(summary.ToolTime), "Parallel calls add up");
        Metric(2, "Est. tokens", Number(summary.Results.Sum(c => c.EstimatedTokens)), "Tool input + output");
        Metric(3, "Tool calls", Number(summary.Results.Count), "Completed");
        RenderResources();
        activities.Children.Clear();
        activities.Children.Add(SectionTitle("Activity breakdown", null));
        if (summary.Results.Any(c => c.Classification?.Pairs.Length > 1)) activities.Children.Add(Note("A call can appear under several target/action pairs."));
        var header = TableRow("Target/action", "Tool time", "Est. tokens", "Calls");
        header.Background = Subtle; activities.Children.Add(header);
        var total = summary.Activities.Sum(AxisAmount);
        foreach (var activity in summary.Activities)
            activities.Children.Add(TableRow(activity.Activity.ToString(), Duration(activity.Milliseconds),
                Number(activity.Tokens), Number(activity.Calls), Color(activity.Activity.Target), (double)AxisAmount(activity) / Math.Max(1, total)));
        if (summary.Activities.Count == 0) activities.Children.Add(Note("No tool activity in this range."));
        if (summary.Activities.Count > 0 && summary.Results.Count == 0)
            activities.Children.Add(Note("No tool calls finished in this range."));
        if (summary.Results.Count > 0)
        {
            var calls = Action("View tool calls  ›", ShowCalls);
            calls.HorizontalAlignment = HorizontalAlignment.Left;
            calls.Margin = new Thickness(10, 7);
            activities.Children.Add(calls);
        }
    }

    private long AxisAmount(ActivityTotal total) => timeline.Tokens ? total.Tokens : total.Milliseconds;

    private void Metric(int column, string title, string value, string description)
    {
        var stack = Stack(1);
        stack.Children.Add(Text(title, 12, UiPalette.Secondary));
        stack.Children.Add(Text(value, 22, strong: true));
        var sub = Text(description, 11, UiPalette.Secondary);
        ToolTip.SetTip(sub, description); stack.Children.Add(sub);
        Put(metrics, Box(stack, padding: new Thickness(10, 6)), column);
    }

    private static Control SectionTitle(string title, string? count)
    {
        var row = Grid("*,Auto"); row.Margin = new Thickness(11, 8);
        Put(row, Text(title, 16, strong: true));
        if (count != null) Put(row, Text(count, 12, UiPalette.Secondary), 1);
        return row;
    }

    private Grid TableRow(string name, string time, string tokens, string calls, IBrush? color = null, double share = 0)
    {
        var row = Grid("*,74,78,42"); row.MinHeight = 26;
        foreach (var (s, i) in new[] { name, time, tokens, calls }.Select((s, i) => (s, i)))
        {
            var cell = Text(i == 0 && color != null ? "■  " + s : s, 12, i == 0 ? color ?? UiPalette.Secondary : UiPalette.Text);
            if (i == 0) ToolTip.SetTip(cell, name);
            cell.HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            cell.Margin = new Thickness(i == 0 ? 11 : 0, 2, 9, 2);
            Put(row, cell, i);
        }
        if (share > 0)
        {
            var bar = new Border
            {
                Background = color,
                Width = Math.Max(2, share * 60),
                Height = 2,
                Margin = new Thickness(0, 0, 9, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            }.WithFeedbackId("SessionInspector.activityShare");
            Put(row, bar, timeline.Tokens ? 2 : 1);
        }
        return row;
    }

    private static TextBlock Note(string value)
    {
        var note = SessionInspectorWindow.Paragraph(value);
        note.Margin = new Thickness(11, 8); return note;
    }

    private void ShowCalls() => detail($"Tool calls   ·   {stamp.Text}", CallList(summary.Results));

    private ListBox CallList(IReadOnlyList<TraceCall> calls, bool showResources = true)
    {
        return new ListBox
        {
            ItemsSource = calls,
            ItemTemplate = new FuncDataTemplate<TraceCall>((call, _) => call == null ? null : CallRow(call, showResources))
        }.WithFeedbackId("SessionInspector.callList");
    }

    private Control CallRow(TraceCall call, bool showResources)
    {
        var row = Stack(2); row.Margin = new Thickness(6, 4);
        row.Children.Add(Text($"{Time(selection.Trace, call.End)}   ·   {call.Agent}   ·   #{call.Id}" +
            (call.Failed ? "   ·   Failed" : ""), 12, Color(call.Labels.First().Target)));
        row.Children.Add(SessionInspectorWindow.Paragraph(call.Classification?.Summary ?? "Unclassified"));
        if (call.Classification is { } classification)
        {
            foreach (var pair in classification.Pairs)
                row.Children.Add(SessionInspectorWindow.Paragraph(pair.Label + "   ·   " + string.Join(", ", pair.Objects)));
            if (showResources)
                foreach (var resource in classification.Resources)
                    row.Children.Add(SessionInspectorWindow.Paragraph(resource.Kind + "   ·   " + resource.Value));
        }
        var title = Text(call.Title, 13, UiPalette.Secondary);
        if (call.Evidence is { } evidence && evidence != call.Title)
            row.Children.Add(new Expander
            {
                Header = title,
                Content = SessionInspectorWindow.Paragraph(evidence),
                HorizontalAlignment = HorizontalAlignment.Stretch
            }.WithFeedbackId("SessionInspector.recordedOperation"));
        else row.Children.Add(title);
        return row;
    }
}

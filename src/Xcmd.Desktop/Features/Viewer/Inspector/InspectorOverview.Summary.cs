// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class InspectorOverview
{
    private void RenderSummary()
    {
        var calls = summary.Results.Count;
        rangeTotals.Text = $"{Number(calls)} {(calls == 1 ? "call" : "calls")} · {Usd(summary.Cost)} API cost";
        if (summary.MissingCosts > 0) rangeTotals.Text += $" · {Number(summary.MissingCosts)} without cost";
        if (hasClassifications) RenderResources();
        if (!hasCalls) return;
        RenderActivities();
    }

    private decimal AxisAmount(ActivityTotal total) => selection.CostAxis ? total.Cost : total.Milliseconds;

    private Control BuildInfo(AgentSession session, SessionTrace trace)
    {
        var info = new WrapPanel().WithFeedbackId("SessionInspector.info");
        var started = DateTimeOffset.FromUnixTimeMilliseconds(trace.Start).ToLocalTime();
        Add("Started", Text($"{started:dd MMM yyyy, HH:mm}", 16));
        Add("Agent", Text(AgentSession.AgentName(session.Agent), 16));
        if (trace.Model != null) Add("Model", Text(trace.Model, 16));
        var counts = trace.CostAxis.At(trace.Responses.Count);
        var cost = Text(trace.Responses.Count > 0 && counts.Missing == trace.Responses.Count ? "Unavailable" : Usd(counts.Usd), 16);
        var callCount = Text(Number(trace.Calls.Count), 16);
        var snapshot = trace.Responses.Select(response => response.Cost.Snapshot).FirstOrDefault(date => date != null);
        var description = $"{Usd(counts.Usd)} API cost for the whole session";
        if (counts.Missing > 0) description += $" · {Number(counts.Missing)} responses without cost";
        if (snapshot != null) description += $" · Standard global API rates · {snapshot}";
        DescribeNumber(cost, description);
        DescribeNumber(callCount, $"{callCount.Text} completed tool calls for the whole session");
        Add("Duration", Text(Duration(trace.End - trace.Start), 16));
        Add("API cost", cost);
        Add("Tool calls", callCount);
        return Box(info);

        void Add(string label, TextBlock value)
        {
            var field = Stack(1);
            field.Margin = new Thickness(12, 6);
            field.Children.Add(Text(label, 13, UiPalette.Secondary));
            field.Children.Add(value);
            info.Children.Add(field);
        }
    }

    private static Control SectionTitle(string title, string? count)
    {
        var row = Grid("*,Auto"); row.Margin = new Thickness(11, 8);
        Put(row, Text(title, 16, strong: true));
        if (count != null) Put(row, Text(count, 12, UiPalette.Secondary), 1);
        return row;
    }

    private Grid TableRow(ActivityTotal activity, double share)
    {
        var row = ActivityGrid();
        var color = Color(activity.Target);
        var name = activity.Name;
        var time = Duration(activity.Milliseconds);
        var cost = Usd(activity.Cost);
        var calls = Number(activity.Calls);
        foreach (var (s, i) in new[] { name, time, cost, calls }.Select((s, i) => (s, i)))
        {
            var cell = Text(i == 0 ? "■  " + s : s, 12, i == 0 ? color : UiPalette.Text);
            if (i == 0) ToolTip.SetTip(cell, name);
            if (i == 2) DescribeNumber(cell, $"{cost} API cost");
            if (i == 3) DescribeNumber(cell, $"{calls} completed tool calls");
            cell.HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            cell.Margin = new Thickness(i == 0 ? 11 : 0, 2, 9, 2);
            Put(row, cell, i);
        }
        if (share > 0)
        {
            var bar = new Border
            {
                Background = color,
                Height = 2,
                Margin = new Thickness(0, 0, 9, 1),
                RenderTransform = new ScaleTransform(share, 1),
                RenderTransformOrigin = new RelativePoint(1, 1, RelativeUnit.Relative),
                VerticalAlignment = VerticalAlignment.Bottom
            }.WithFeedbackId("SessionInspector.activityShare");
            Put(row, bar, selection.CostAxis ? 2 : 1);
        }
        return row;
    }

    private static void DescribeNumber(TextBlock cell, string description)
    {
        ToolTip.SetTip(cell, description);
        Avalonia.Automation.AutomationProperties.SetName(cell, description);
    }

    private static TextBlock Note(string value)
    {
        var note = SessionInspectorWindow.Paragraph(value);
        note.Margin = new Thickness(11, 8); return note;
    }

    internal bool ClearSelectedCalls()
    {
        if (!selectedCalls.IsVisible) return false;
        selectedCalls.IsVisible = false;
        selectedCalls.Children.Clear();
        timeline.SelectedBlock = null;
        responseFocus.Select(null);
        timeline.InvalidateVisual();
        return true;
    }

    private void ShowBlock(SessionCallBlock block)
    {
        timeline.SelectedBlock = block;
        selectedCalls.IsVisible = true;
        selectedCalls.Children.Clear();
        selectedCalls.Children.Add(SectionTitle("Tool call details", block.Calls.Count > 1 ? $"{block.Calls.Count} calls" : null));
        if (block.Calls.Count == 1)
            selectedCalls.Children.Add(CallDetails(block.Calls[0]));
        else
        {
            var list = CallList(block.Calls);
            list.MaxHeight = 240;
            list.DoubleTapped += (_, _) => OpenSelected();
            list.KeyDown += (_, e) => { if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; } };
            selectedCalls.Children.Add(list);
            void OpenSelected()
            {
                if (list.SelectedItem is TraceCall call) detail("Tool call details", CallDetails(call));
            }
        }
    }

    private void ShowResponse(ResponseHit hit)
    {
        var response = hit.Response;
        responseFocus.Select(response);
        selectedCalls.IsVisible = true;
        selectedCalls.Children.Clear();
        selectedCalls.Children.Add(SectionTitle("Model response", $"{Number(response.Calls.Count)} calls"));
        selectedCalls.Children.Add(Note(CostDetails(response)));
        if (response.Calls.Count == 0) return;
        if (response.Calls.Count == 1)
        {
            selectedCalls.Children.Add(CallDetails(response.Calls[0]));
            return;
        }
        selectedCalls.Children.Add(SectionTitle(hit.Context, $"{Number(hit.Calls.Count)} matching calls"));
        var matched = hit.Calls.ToHashSet();
        var list = CallList(response.Calls.OrderByDescending(matched.Contains).ToArray());
        list.SelectedItem = hit.Calls.FirstOrDefault();
        list.MaxHeight = 240;
        list.DoubleTapped += (_, _) => OpenSelected();
        list.KeyDown += (_, e) => { if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; } };
        selectedCalls.Children.Add(list);

        void OpenSelected()
        {
            if (list.SelectedItem is TraceCall call) detail("Tool call details", CallDetails(call));
        }
    }

    private Control CallDetails(TraceCall call)
    {
        List<string> sections = [call.Classification?.Summary ?? call.Title,
            $"{call.Agent}   ·   #{call.Id}   ·   {Time(selection.Trace, call.Start)} – {Time(selection.Trace, call.End)}   ·   {CallStatus(call)}"];
        if (!string.IsNullOrEmpty(call.WorkingDirectory))
            sections.Add("Working directory: " + call.WorkingDirectory);
        if (call.Classification is { } classified)
        {
            sections.AddRange(classified.Pairs.Select(pair => pair.Label + "   ·   " + string.Join(", ", pair.Objects)));
            sections.AddRange(classified.Resources.Select(resource => resource.Kind + "   ·   " + resource.Value));
        }
        foreach (var (label, value) in new[] { ("Input", call.Input), ("Output", call.Output) })
            if (!string.IsNullOrEmpty(value)) sections.Add(label + Environment.NewLine + value);
        var field = new TextBox
        {
            Text = string.Join(Environment.NewLine + Environment.NewLine, sections),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 320,
            FontSize = 14,
            Padding = new Thickness(8)
        }.WithFeedbackId("SessionInspector.callDetails");
        Avalonia.Automation.AutomationProperties.SetName(field, "Tool call details");
        ScrollViewer.SetVerticalScrollBarVisibility(field, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        return field;
    }

    private static string CallStatus(TraceCall call) => !call.Completed ? "Awaiting result" : call.Failed ? "Failed" : "Completed";

    private ListBox CallList(IReadOnlyList<TraceCall> calls)
    {
        return new ListBox
        {
            ItemsSource = calls,
            ItemTemplate = new FuncDataTemplate<TraceCall>((call, _) => call == null ? null : CallRow(call))
        }.WithFeedbackId("SessionInspector.callList");
    }

    private Control CallRow(TraceCall call)
    {
        var row = Stack(2); row.Margin = new Thickness(6, 4);
        row.Children.Add(Text($"{Time(selection.Trace, call.End)}   ·   {call.Agent}   ·   #{call.Id}" +
            (call.Completed && !call.Failed ? "" : "   ·   " + CallStatus(call)), 12, Color(call.Labels.First().Target)));
        row.Children.Add(SessionInspectorWindow.Paragraph(call.Classification?.Summary ?? "Unclassified"));
        if (call.Classification is { } classification)
        {
            foreach (var pair in classification.Pairs)
                row.Children.Add(SessionInspectorWindow.Paragraph(pair.Label + "   ·   " + string.Join(", ", pair.Objects)));
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

// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
        var info = new WrapPanel().WithFeedbackId("SessionInspector.summaryFields");
        var started = DateTimeOffset.FromUnixTimeMilliseconds(trace.Start).ToLocalTime();
        Add("SessionInspector.startedField", "SessionInspector.startedLabel", "Started",
            Text("SessionInspector.startedValue", $"{started:dd MMM yyyy, HH:mm}", 16));
        Add("SessionInspector.agentField", "SessionInspector.agentLabel", "Agent",
            Text("SessionInspector.agentValue", AgentSession.AgentName(session.Agent), 16));
        if (trace.Model != null)
            Add("SessionInspector.modelField", "SessionInspector.modelLabel", "Model",
                Text("SessionInspector.modelValue", trace.Model, 16));
        var counts = trace.CostAxis.At(trace.Responses.Count);
        var cost = Text("SessionInspector.sessionCostValue",
            trace.Responses.Count > 0 && counts.Missing == trace.Responses.Count ? "Unavailable" : Usd(counts.Usd), 16);
        var callCount = Text("SessionInspector.sessionCallCountValue", Number(trace.Calls.Count), 16);
        var snapshot = trace.Responses.Select(response => response.Cost.Snapshot).FirstOrDefault(date => date != null);
        var description = $"{Usd(counts.Usd)} API cost for the whole session";
        if (counts.Missing > 0) description += $" · {Number(counts.Missing)} responses without cost";
        if (snapshot != null) description += $" · Standard global API rates · {snapshot}";
        DescribeNumber(cost, description);
        DescribeNumber(callCount, $"{callCount.Text} completed tool calls for the whole session");
        Add("SessionInspector.durationField", "SessionInspector.durationLabel", "Duration",
            Text("SessionInspector.durationValue", Duration(trace.End - trace.Start), 16));
        Add("SessionInspector.sessionCostField", "SessionInspector.sessionCostLabel", "API cost", cost);
        Add("SessionInspector.sessionCallCountField", "SessionInspector.sessionCallCountLabel", "Tool calls", callCount);
        Add("SessionInspector.sessionIdField", "SessionInspector.sessionIdLabel", "Session ID", BuildSessionId(session.Id));
        return Box("SessionInspector.sessionSummary", info);

        void Add([FeedbackId] string fieldId, [FeedbackId] string labelId, string label, Control value)
        {
            var field = Stack(fieldId, 1);
            field.Margin = new Thickness(12, 6);
            field.Children.Add(Text(labelId, label, 13, UiPalette.Secondary));
            field.Children.Add(value);
            info.Children.Add(field);
        }
    }

    private static Control BuildSessionId(Guid sessionId)
    {
        const string copyLabel = "Copy session ID";
        var id = sessionId.ToString("D");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 }
            .WithFeedbackId("SessionInspector.sessionId");
        row.Children.Add(new TextBlock { Text = id, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }
            .WithFeedbackId("SessionInspector.sessionIdValue"));
        var copyGlyph = Geometry.Parse("M 1,1 H 10 V 2.5 H 2.5 V 11 H 1 Z M 5,4 H 15 V 15 H 5 Z M 6.5,5.5 V 13.5 H 13.5 V 5.5 Z");
        var copiedGlyph = Geometry.Parse("M 1,7 L 2.5,5.5 L 6,9 L 13,2 L 14.5,3.5 L 6,12 Z");
        var errorGlyph = Geometry.Parse("M 6,1 H 8 V 10 H 6 Z M 6,12 H 8 V 14 H 6 Z");
        var icon = new PathIcon { Data = copyGlyph, Width = 14, Height = 14 }.WithFeedbackId("SessionInspector.copySessionIdIcon");
        var copy = new Button
        {
            Content = icon,
            Width = 24,
            Height = 22,
            MinHeight = 0,
            MinWidth = 0,
            Padding = default,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        }.WithFeedbackId("SessionInspector.copySessionId");
        copy.Classes.Add("quiet");
        AutomationProperties.SetName(copy, copyLabel);
        ToolTip.SetTip(copy, copyLabel);
        IDisposable? resetIcon = null;
        copy.Click += async (_, _) =>
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(copy)?.Clipboard ?? throw new InvalidOperationException();
                await clipboard.SetTextAsync(id);
                await clipboard.FlushAsync();
                ShowResult(copiedGlyph, "Session ID copied");
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException or NotSupportedException)
            {
                ShowResult(errorGlyph, "Could not copy the session ID");
            }
        };
        row.Children.Add(copy);
        return row;

        void ShowResult(Geometry glyph, string message)
        {
            icon.Data = glyph;
            AutomationProperties.SetItemStatus(copy, message);
            ToolTip.SetTip(copy, message);
            resetIcon?.Dispose();
            resetIcon = DispatcherTimer.RunOnce(() =>
            {
                resetIcon = null;
                icon.Data = copyGlyph;
                AutomationProperties.SetItemStatus(copy, "");
                ToolTip.SetTip(copy, copyLabel);
            }, TimeSpan.FromSeconds(1.2));
        }
    }

    private static Control SectionTitle([FeedbackId] string id, TextBlock title, TextBlock? count = null)
    {
        var row = Grid(id, "*,Auto"); row.Margin = new Thickness(11, 8);
        Put(row, title);
        if (count != null) Put(row, count, 1);
        return row;
    }

    private Grid TableRow(ActivityTotal activity, double share)
    {
        var row = ActivityGrid("SessionInspector.activityRow");
        var color = Color(activity.Target);
        var name = activity.Name;
        var time = Duration(activity.Milliseconds);
        var cost = Usd(activity.Cost);
        var calls = Number(activity.Calls);
        var targetCell = Text("SessionInspector.activityTargetValue", "■  " + name, 12, color);
        ToolTip.SetTip(targetCell, name);
        AddCell(targetCell, 0);
        AddCell(Text("SessionInspector.activityToolTimeValue", time, 12), 1);
        var costCell = Text("SessionInspector.activityCostValue", cost, 12);
        DescribeNumber(costCell, $"{cost} API cost");
        AddCell(costCell, 2);
        var callsCell = Text("SessionInspector.activityCallCountValue", calls, 12);
        DescribeNumber(callsCell, $"{calls} completed tool calls");
        AddCell(callsCell, 3);
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

        void AddCell(TextBlock cell, int column)
        {
            cell.HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            cell.Margin = new Thickness(column == 0 ? 11 : 0, 2, 9, 2);
            Put(row, cell, column);
        }
    }

    private static void DescribeNumber(TextBlock cell, string description)
    {
        ToolTip.SetTip(cell, description);
        Avalonia.Automation.AutomationProperties.SetName(cell, description);
    }

    private static TextBlock Note([FeedbackId] string id, string value)
    {
        var note = SessionInspectorWindow.Paragraph(id, value);
        note.Margin = new Thickness(11, 8); return note;
    }

    internal bool ClearSelectedCalls()
    {
        if (!selectedCalls.IsVisible) return false;
        selectedCalls.IsVisible = false;
        selectedCalls.Children.Clear();
        ResetTimelineHint();
        timeline.SelectedBlock = null;
        responseFocus.Select(null);
        timeline.InvalidateVisual();
        return true;
    }

    private void ShowBlock(SessionCallBlock block)
    {
        timeline.SelectedBlock = block;
        ShowSpan(block.Calls);
    }

    private void ShowResponse(ResponseHit hit)
    {
        responseFocus.Select(hit.Response);
        ShowSpan(hit.Response.Calls, hit.SelectedCall, hit.Response);
    }

    private void ShowSpan(IReadOnlyList<TraceCall> calls, TraceCall? selected = null, SessionResponse? response = null)
    {
        SetTimelineHint("See Selected span below the timeline.");
        selectedCalls.IsVisible = true;
        selectedCalls.Children.Clear();
        var header = Stack("SessionInspector.responseHeader", 8);
        header.Orientation = Orientation.Horizontal;
        header.Margin = new Thickness(9, 2);
        header.Children.Add(Text("SessionInspector.responseTitle", "Selected span", 14, strong: true));
        if (calls.Count > 1)
            header.Children.Add(Text("SessionInspector.responseCallCount", $"{Number(calls.Count)} calls", 12, UiPalette.Secondary));
        if (calls.Count == 0 && response != null)
        {
            var price = response.Cost.Usd is { } cost ? $"{Usd(cost)} API cost" : response.Cost.Unavailable!;
            var status = Text("SessionInspector.messageCostValue", price, 12, UiPalette.Secondary);
            ToolTip.SetTip(status, CostDetails(response));
            header.Children.Add(status);
        }
        selectedCalls.Children.Add(header);
        if (response?.Record.Message is { Length: > 0 } message)
            selectedCalls.Children.Add(Note("SessionInspector.responseMessage", message));
        if (calls.Count == 1)
            selectedCalls.Children.Add(BuildCallCard(calls[0]));
        else if (calls.Count > 1)
            ShowCallPicker(calls, selected);
    }

    private sealed record CallChoice(int Position, TraceCall Call);

    private void ShowCallPicker(IReadOnlyList<TraceCall> calls, TraceCall? selected = null)
    {
        var choices = calls.Select((call, index) => new CallChoice(index + 1, call)).ToArray();
        var card = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch }
            .WithFeedbackId("SessionInspector.activeCallCard");
        var list = new ListBox
        {
            ItemsSource = choices,
            MaxHeight = 140,
            ItemTemplate = new FuncDataTemplate<CallChoice>((choice, _) => CallPickerRow(choice!))
        }.WithFeedbackId("SessionInspector.callPicker");
        AutomationProperties.SetName(list, "Tool calls");
        list.SelectionChanged += (_, _) =>
            card.Content = list.SelectedItem is CallChoice choice ? BuildCallCard(choice.Call, choice.Position) : null;
        list.DoubleTapped += (_, _) => OpenSelected();
        list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            OpenSelected();
            e.Handled = true;
        };
        var browser = Stack("SessionInspector.callBrowser");
        browser.Children.Add(list);
        browser.Children.Add(card);
        selectedCalls.Children.Add(browser);
        list.SelectedItem = choices.FirstOrDefault(choice => choice.Call == selected) ?? choices[0];

        void OpenSelected()
        {
            if (list.SelectedItem is CallChoice choice) viewToolCall(choice.Call);
        }
    }

    private static Control CallPickerRow(CallChoice choice)
    {
        var row = Grid("SessionInspector.callPickerRow", "27,76,*");
        row.ColumnSpacing = 8;
        var position = Number(choice.Position);
        var duration = CallDuration(choice.Call);
        var summary = CallSummary(choice.Call);
        Put(row, Text("SessionInspector.callPickerNumber", position, 12, UiPalette.Secondary, strong: true));
        Put(row, Text("SessionInspector.callPickerDuration", duration, 12, UiPalette.Secondary), 1);
        var label = Text("SessionInspector.callPickerSummary", summary, 13);
        ToolTip.SetTip(label, summary);
        Put(row, label, 2);
        AutomationProperties.SetName(row, $"Call {position}, {duration}, {summary}");
        return row;
    }

    private Control BuildCallCard(TraceCall call, int? position = null) =>
        InspectorCallCard.Build(selection.Trace, call, () => viewToolCall(call), position);

    private static string CallStatus(TraceCall call) => !call.Completed ? "Awaiting result" : call.Failed ? "Failed" : "Completed";

    private ListBox CallList([FeedbackId] string id, IReadOnlyList<TraceCall> calls)
    {
        return new ListBox
        {
            ItemsSource = calls,
            ItemTemplate = new FuncDataTemplate<TraceCall>((call, _) => call == null ? null : CallRow(call))
        }.WithFeedbackId(id);
    }

    private Control CallRow(TraceCall call)
    {
        var row = Stack("SessionInspector.callRow", 2); row.Margin = new Thickness(6, 4);
        row.Children.Add(Text("SessionInspector.callRowStatus", $"{Time(selection.Trace, call.End)}   ·   {call.Agent}   ·   #{call.Id}" +
            (call.Completed && !call.Failed ? "" : "   ·   " + CallStatus(call)), 12, Color(call.Labels.First().Target)));
        row.Children.Add(SessionInspectorWindow.Paragraph("SessionInspector.callRowSummary", call.Classification?.Summary ?? "Unclassified"));
        if (call.Classification is { } classification)
        {
            foreach (var pair in classification.Pairs)
                row.Children.Add(SessionInspectorWindow.Paragraph("SessionInspector.callRowClassification", pair.Label + "   ·   " + string.Join(", ", pair.Objects)));
        }
        var title = Text("SessionInspector.callRowOperationTitle", call.Title, 13, UiPalette.Secondary);
        if (call.Evidence is { } evidence && evidence != call.Title)
            row.Children.Add(new Expander
            {
                Header = title,
                Content = SessionInspectorWindow.Paragraph("SessionInspector.callRowOperationEvidence", evidence),
                HorizontalAlignment = HorizontalAlignment.Stretch
            }.WithFeedbackId("SessionInspector.recordedOperation"));
        else row.Children.Add(title);
        return row;
    }
}

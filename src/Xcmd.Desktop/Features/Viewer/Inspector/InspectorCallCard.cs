// @implements InspectAgentSession
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal static class InspectorCallCard
{
    internal static Control Build(SessionTrace trace, TraceCall call, Action open, int? position)
    {
        var layout = Grid("SessionInspector.callCardLayout", "216,*", "Auto,Auto");
        var facts = Facts(trace, call, open, position);
        var activity = Activity(trace, call);
        Put(layout, facts);
        Put(layout, activity, 1);
        layout.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 610;
            if (narrow == (Avalonia.Controls.Grid.GetRow(activity) == 1)) return;
            layout.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "216,*");
            Avalonia.Controls.Grid.SetColumn(activity, narrow ? 0 : 1);
            Avalonia.Controls.Grid.SetRow(activity, narrow ? 1 : 0);
            facts.BorderThickness = narrow ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 0);
        };
        var card = Box("SessionInspector.callCard", layout);
        if (position.HasValue)
        {
            card.BorderBrush = UiPalette.Focus;
            card.BorderThickness = new Thickness(1, 2, 1, 1);
        }
        return card;
    }

    private static Border Facts(SessionTrace trace, TraceCall call, Action open, int? position)
    {
        var body = Stack("SessionInspector.callFactsContent", 4);
        body.Children.Add(ToolLink(call, open, position));
        var fields = Grid("SessionInspector.callFacts", "Auto,*", "Auto,Auto,Auto,Auto,Auto");
        fields.ColumnSpacing = 8;
        fields.MaxWidth = 240;
        fields.HorizontalAlignment = HorizontalAlignment.Left;
        var elapsed = TimeSpan.FromMilliseconds(Math.Max(0, call.Start - trace.Start));
        Add("SessionInspector.callStartLabel", "SessionInspector.callStartValue", "Call start",
            $"{(long)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}", 0);
        Add("SessionInspector.callDurationLabel", "SessionInspector.callDurationValue", "Call duration",
            CallDuration(call), 1);
        var response = trace.ResponseFor(call);
        var cost = response?.Cost.Usd;
        var price = Add("SessionInspector.callResponseCostLabel", "SessionInspector.callResponseCostValue", "Response API cost",
            cost is { } total ? Usd(total) : "Unavailable", 2);
        if (response != null) ToolTip.SetTip(price, CostDetails(response));
        if (cost is { } amount && response!.Calls.Count > 1)
        {
            var count = response.Calls.Count;
            var share = Add("SessionInspector.callShareLabel", "SessionInspector.callShareValue", "Est. call share",
                $"≈{Usd(amount / count)} (1/{count})", 3);
            ToolTip.SetTip(share, "Response API cost divided equally among all calls in the response.");
        }
        Add("SessionInspector.callModelLabel", "SessionInspector.callModelValue", "Model",
            response?.Cost.Model is { Length: > 0 } model ? model : "Unavailable", 4);
        body.Children.Add(fields);
        var panel = Box("SessionInspector.callFactsPanel", body, Subtle, new Thickness(9, 6));
        panel.BorderThickness = new Thickness(0, 0, 1, 0);
        return panel;

        TextBlock Add([FeedbackId] string labelId, [FeedbackId] string valueId, string label, string value, int row)
        {
            Put(fields, Text(labelId, label, 12, UiPalette.Secondary), 0, row);
            var text = Text(valueId, value, 12);
            text.TextAlignment = TextAlignment.Right;
            ToolTip.SetTip(text, value);
            Put(fields, text, 1, row);
            return text;
        }
    }

    private static Control ToolLink(TraceCall call, Action open, int? position)
    {
        var header = Grid("SessionInspector.callFactsHeader", "Auto,*");
        header.ColumnSpacing = 6;
        if (position is { } index)
        {
            var label = Text("SessionInspector.activeCallNumber", Number(index), 12, strong: true);
            label.TextAlignment = TextAlignment.Center;
            label.MinWidth = 16;
            var badge = Box("SessionInspector.activeCallNumberBadge", label, UiPalette.Cursor, new Thickness(3, 1));
            badge.BorderThickness = default;
            Put(header, badge);
        }
        var name = ToolCallName(call);
        var linkLabel = Text("SessionInspector.toolCallLinkLabel", name, 13, UiPalette.Focus, true);
        linkLabel.TextDecorations = TextDecorations.Underline;
        var link = new HyperlinkButton
        {
            Content = linkLabel,
            Foreground = UiPalette.Focus,
            Padding = new Thickness(0, 1),
            HorizontalAlignment = HorizontalAlignment.Left
        }.WithFeedbackId("SessionInspector.viewToolCall");
        link.Click += (_, _) => open();
        AutomationProperties.SetName(link, $"Open tool call details for {name}");
        ToolTip.SetTip(link, $"Open tool call details\n{name}");
        Put(header, link, 1);
        return header;
    }

    private static Control Activity(SessionTrace trace, TraceCall call)
    {
        var content = Stack("SessionInspector.callActivity", 4);
        content.Margin = new Thickness(11, 6);
        var summary = Text("SessionInspector.callSummary", CallSummary(call), 14, strong: true);
        summary.TextWrapping = TextWrapping.Wrap;
        summary.TextTrimming = TextTrimming.None;
        content.Children.Add(summary);
        if (call.Failed || !call.Completed)
            content.Children.Add(Text("SessionInspector.callStatus", call.Failed ? "Failed" : "Awaiting result", 12,
                call.Failed ? UiPalette.Error : UiPalette.Secondary));
        content.Children.Add(InspectorCallResources.Build(trace, call));
        return content;
    }
}

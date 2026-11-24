// @implements InspectAgentSession
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal static class TimelineCallTip
{
    internal static Control Build(SessionCallBlock block)
    {
        var call = block.Calls[0];
        var content = Stack("SessionInspector.timelineCallTip", 4);
        content.MaxWidth = 450;
        var name = ToolCallName(call);
        var summary = CallSummary(call);
        content.Children.Add(Wrapped("SessionInspector.timelineTipTool", name, 14, UiPalette.Text, strong: true));
        if (summary != name)
            content.Children.Add(Wrapped("SessionInspector.timelineTipSummary", summary, 13, UiPalette.Text));
        if (block.Categories.Count == 0)
            content.Children.Add(Text("SessionInspector.timelineTipUnclassified", "Unclassified", 12, UiPalette.Secondary));
        foreach (var category in block.Categories)
            content.Children.Add(Category(category));
        AutomationProperties.SetName(content, Describe(block));
        return content;
    }

    internal static string Describe(SessionCallBlock block)
    {
        var call = block.Calls[0];
        var name = ToolCallName(call);
        var summary = CallSummary(call);
        var lines = new List<string> { name };
        if (summary != name) lines.Add(summary);
        if (block.Categories.Count == 0) lines.Add("Unclassified");
        foreach (var category in block.Categories)
        {
            var actions = category.Actions.Select(action => TimelineCaption.ActionIcon(action)?.Name ?? action);
            lines.Add($"{TimelineCaption.Name(category.Target)}: {string.Join(", ", actions)}");
        }
        return string.Join('\n', lines);
    }

    private static Control Category(TimelineCategory category)
    {
        var row = Stack("SessionInspector.timelineTipTargetActions", 5);
        row.Orientation = Orientation.Horizontal;
        var target = category.Target;
        row.Children.Add(Text("SessionInspector.timelineTipTarget", TimelineCaption.Name(target), 13, Color(target), strong: true));
        foreach (var action in category.Actions)
            if (ActionGlyph("SessionInspector.timelineTipAction", action, target) is { } icon) row.Children.Add(icon);
        return row;
    }

    private static TextBlock Wrapped([FeedbackId] string id, string value, double size, IBrush color, bool strong = false)
    {
        var block = Text(id, value, size, color, strong);
        block.TextWrapping = TextWrapping.Wrap;
        block.TextTrimming = TextTrimming.None;
        return block;
    }
}

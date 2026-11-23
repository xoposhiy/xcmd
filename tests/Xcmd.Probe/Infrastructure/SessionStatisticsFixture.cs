// @tests ViewRecentAgentSessions
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Shared.Ui;

namespace Xcmd.Probe.Infrastructure;

internal static class SessionStatisticsFixture
{
    internal static TextBlock Bar(PanelView panel) => panel.GetVisualDescendants().OfType<TextBlock>()
        .Single(text => text.GetValue(FeedbackTarget.IdProperty) == "AgentSessionView.statistics");

    internal static void CheckFooter(PanelView panel)
    {
        var summary = Bar(panel);
        var footer = summary.FindAncestorOfType<Border>()!;
        var count = footer.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "2 sessions");
        ProbeContext.Check(footer.BorderThickness.Top > 0 && footer.BorderBrush != null, "A rule separates the footer from the session list");
        var start = summary.TranslatePoint(default, footer)!.Value;
        var end = count.TranslatePoint(default, footer)!.Value;
        ProbeContext.Check(Math.Abs(start.Y - end.Y) < 1 && start.X + summary.Bounds.Width <= end.X && end.X + count.Bounds.Width <= footer.Bounds.Width,
            "Session size and list count fit side by side on the same line");
    }

    internal static AgentSession CodexTranscript(string root, AgentSession session) => Write(root, session, """
        {"timestamp":"2026-01-01T00:00:00Z","type":"turn_context","payload":{"model":"test-model"}}
        {"timestamp":"2026-01-01T00:01:00Z","type":"event_msg","payload":{"type":"user_message","message":"First"}}
        {"timestamp":"2026-01-01T00:01:00Z","type":"event_msg","payload":{"type":"item_completed","item":{"type":"UserMessage"}}}
        {"type":"response_item","payload":{"type":"function_call","call_id":"one"}}
        {"type":"response_item","payload":{"type":"function_call_output","call_id":"one"}}
        {"type":"response_item","payload":{"type":"custom_tool_call","call_id":"two"}}
        {"timestamp":"2026-01-01T00:09:00Z","type":"event_msg","payload":{"type":"item_completed","item":{"type":"UserMessage"}}}
        {"type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"total_tokens":1200},"total_token_usage":{"total_tokens":10000123},"model_context_window":64000}}}
        {"type":"event_msg","payload":{"type":"token_count","info":null}}
        {"unfinished":
        """);

    internal static AgentSession ClaudeTranscript(string root, AgentSession session) => Write(root, session, """
        {"type":"user","timestamp":"2026-01-02T09:00:00Z","message":{"content":"Check the panel"}}
        {"type":"assistant","message":{"id":"a","model":"claude-test","content":[{"type":"tool_use","id":"one"}],"usage":{"input_tokens":99900,"cache_creation_input_tokens":40,"cache_read_input_tokens":60,"output_tokens":7}}}
        {"type":"user","timestamp":"2026-01-02T09:10:00Z","message":{"content":[{"type":"tool_result","tool_use_id":"one"}]}}
        {"type":"user","timestamp":"2026-01-02T09:15:00Z","message":{"content":[{"type":"text","text":"Continue"}]}}
        {"type":"user","isMeta":true,"timestamp":"2026-01-02T09:20:00Z","message":{"content":"Context"}}
        {"type":"cost-state","totalCostUSD":1.256}
        {"type":"cost-state","totalCostUSD":2.75}
        """);

    private static AgentSession Write(string root, AgentSession session, string records)
    {
        var path = Path.Combine(root, session.Id + ".jsonl");
        File.WriteAllText(path, records);
        return session with { TranscriptPath = path };
    }
}

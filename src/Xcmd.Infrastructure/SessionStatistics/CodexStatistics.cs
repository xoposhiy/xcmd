// @implements ViewRecentAgentSessions
using System.Text.Json;

namespace Xcmd.Infrastructure;

internal sealed class CodexStatistics(Guid session) : TranscriptStatistics
{
    private readonly PromptTimes legacyPrompts = new();
    private readonly PromptTimes messagePrompts = new();
    protected override PromptTimes UserPrompts => Prompts.Count > 0 ? Prompts : legacyPrompts.Count > 0 ? legacyPrompts : messagePrompts;

    public override void Record(JsonElement record)
    {
        var data = record.Field("payload");
        if (Guid.TryParse(data.Str("thread_id"), out var owner) && owner != session) return;
        var type = data.Str("type");
        switch (record.Str("type"))
        {
            case "session_meta":
                ContextWindow = Tokens(data, "context_window") ?? ContextWindow;
                break;
            case "turn_context":
                if (data.Str("model") is { Length: > 0 } model) Model = model;
                break;
            case "response_item" when type is "function_call" or "custom_tool_call" or "web_search_call" or "local_shell_call" or "image_generation_call" or "tool_search_call":
                ToolCall(data.Str("call_id") is { Length: > 0 } call ? call : data.Str("id"));
                break;
            case "response_item" when type == "message" && data.Str("role") == "user":
                messagePrompts.Add(record);
                break;
            case "event_msg" when type == "item_completed" && data.Field("item").Str("type") == "UserMessage":
                Prompts.Add(record);
                break;
            case "event_msg" when type == "user_message":
                legacyPrompts.Add(record);
                break;
            case "event_msg" when type == "task_started":
                ContextWindow = Tokens(data, "model_context_window") ?? ContextWindow;
                break;
            case "event_msg" when type == "token_count":
                var info = data.Field("info");
                ContextTokens = Tokens(info.Field("last_token_usage"), "total_tokens") ?? ContextTokens;
                TotalTokens = Tokens(info.Field("total_token_usage"), "total_tokens") ?? TotalTokens;
                ContextWindow = Tokens(info, "model_context_window") ?? ContextWindow;
                break;
            case "token_usage_record":
                ContextTokens = Tokens(data.Field("usage"), "total_tokens") ?? ContextTokens;
                break;
        }
    }
}

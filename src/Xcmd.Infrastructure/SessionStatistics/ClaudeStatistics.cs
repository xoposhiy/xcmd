// @implements ViewRecentAgentSessions
using System.Text.Json;

namespace Xcmd.Infrastructure;

internal sealed class ClaudeStatistics : TranscriptStatistics
{
    public override void Record(JsonElement record)
    {
        var message = record.Field("message");
        switch (record.Str("type"))
        {
            case "user":
                if (IsUserPrompt(record, message.Field("content"))) Prompts.Add(record);
                break;
            case "assistant":
                AssistantMessage(message);
                break;
            case "result":
            case "cost-state":
                var cost = record.Field(record.Str("type") == "result" ? "total_cost_usd" : "totalCostUSD");
                if (cost.ValueKind == JsonValueKind.Number && cost.TryGetDecimal(out var usd))
                    Cost = record.Field("hasUnknownModelCost").ValueKind == JsonValueKind.True ? null : usd;
                break;
        }
    }

    private static bool IsUserPrompt(JsonElement record, JsonElement content)
    {
        if (record.Field("isMeta").ValueKind == JsonValueKind.True || record.Field("isSidechain").ValueKind == JsonValueKind.True
            || record.Field("isCompactSummary").ValueKind == JsonValueKind.True) return false;
        return content.ValueKind == JsonValueKind.String || content.Items().Any() && content.Items().All(part => part.Str("type") != "tool_result");
    }

    private void AssistantMessage(JsonElement message)
    {
        foreach (var part in message.Field("content").Items().Where(part => part.Str("type") == "tool_use"))
            ToolCall(part.Str("id"));
        var model = message.Str("model");
        if (model == "<synthetic>") return;
        if (model.Length > 0) Model = model;
        var usage = message.Field("usage");
        if (Tokens(usage, "input_tokens") is { } input)
            ContextTokens = input + (Tokens(usage, "cache_read_input_tokens") ?? 0)
                + (Tokens(usage, "cache_creation_input_tokens") ?? 0) + (Tokens(usage, "output_tokens") ?? 0);
    }
}

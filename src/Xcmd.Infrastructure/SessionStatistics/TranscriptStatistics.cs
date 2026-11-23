// @implements ViewRecentAgentSessions
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal abstract class TranscriptStatistics
{
    protected sealed class PromptTimes
    {
        private DateTimeOffset? first;
        private DateTimeOffset? last;
        public int Count { get; private set; }
        public TimeSpan? Span => last - first;
        public void Add(JsonElement record)
        {
            Count++;
            if (record.Field("timestamp") is not { ValueKind: JsonValueKind.String } time || !time.TryGetDateTimeOffset(out var at)) return;
            if (first == null || at < first) first = at;
            if (last == null || at > last) last = at;
        }
    }

    protected PromptTimes Prompts { get; } = new();
    protected virtual PromptTimes UserPrompts => Prompts;
    private readonly HashSet<string> calls = [];
    private int unnamedCalls;
    protected void ToolCall(string id)
    {
        if (id.Length == 0) unnamedCalls++;
        else calls.Add(id);
    }

    protected long? ContextTokens { get; set; }
    protected long? TotalTokens { get; set; }
    protected long? ContextWindow { get; set; }
    protected decimal? Cost { get; set; }
    protected string? Model { get; set; }
    protected static long? Tokens(JsonElement value, string name) =>
        value.Field(name) is { ValueKind: JsonValueKind.Number } n && n.TryGetInt64(out var amount) && amount >= 0 ? amount : null;

    public abstract void Record(JsonElement record);
    public SessionStatistics Result() => new(calls.Count + unnamedCalls, UserPrompts.Count, UserPrompts.Span,
        ContextTokens, ContextWindow, Cost, Model, TotalTokens);
}

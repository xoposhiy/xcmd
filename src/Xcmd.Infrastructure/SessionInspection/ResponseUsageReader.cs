// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class ResponseUsageReader
{
    internal static ResponseUsage? OpenAi(JsonElement usage, string retention)
    {
        if (Count(usage, "input_tokens") is not { } input || Count(usage, "output_tokens") is not { } output) return null;
        var details = usage.Field("input_tokens_details");
        var read = Count(usage, "cached_input_tokens") ?? Count(details, "cached_tokens") ?? 0;
        var write = Count(usage, "cache_write_input_tokens") ?? Count(details, "cache_write_tokens") ?? 0;
        if (read > input || write > input - read) return null;
        var duration = usage.Str("prompt_cache_retention");
        if (duration.Length == 0) duration = details.Str("cache_write_duration");
        if (duration.Length == 0) duration = retention;
        return new(input - read - write, read, write > 0 ? new Dictionary<string, long> { [duration] = write } : EmptyWrites,
            output, Count(usage, "reasoning_output_tokens") ?? Count(usage.Field("output_tokens_details"), "reasoning_tokens"));
    }

    internal static ResponseUsage? Claude(JsonElement usage)
    {
        if (Count(usage, "input_tokens") is not { } input || Count(usage, "output_tokens") is not { } output) return null;
        var cache = usage.Field("cache_creation");
        var minutes = Count(cache, "ephemeral_5m_input_tokens") ?? 0;
        var hour = Count(cache, "ephemeral_1h_input_tokens") ?? 0;
        var total = Count(usage, "cache_creation_input_tokens");
        var writes = new Dictionary<string, long>();
        if (minutes > 0) writes["5m"] = minutes;
        if (hour > 0) writes["1h"] = hour;
        if (total is { } count && count - minutes >= hour && count - minutes - hour > 0) writes[""] = count - minutes - hour;
        return new(input, Count(usage, "cache_read_input_tokens") ?? 0, writes, output,
            Count(usage.Field("output_tokens_details"), "thinking_tokens") ?? Count(usage, "thinking_tokens"));
    }

    internal static long? Count(JsonElement value, string name) =>
        value.Field(name) is { ValueKind: JsonValueKind.Number } field && field.TryGetInt64(out var count) && count >= 0 ? count : null;

    private static readonly IReadOnlyDictionary<string, long> EmptyWrites = new Dictionary<string, long>();
}

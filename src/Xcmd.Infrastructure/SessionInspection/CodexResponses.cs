// @implements InspectAgentSession
using System.Globalization;
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class CodexResponses(TraceRecords trace, string agent, Guid thread)
{
    private readonly string stream = thread.ToString("D");
    private readonly Dictionary<ResponseKey, ResponseKey> resolved = [];
    private readonly Dictionary<string, (ResponseKey Key, int Order)> calls = [];
    private readonly Dictionary<string, string> models = [];
    private readonly List<(long At, ResponseKey Key)> emitted = [];
    private ResponseKey? current;
    private long started;
    private string? message;
    private string? model;
    private string retention = "";
    private bool hasResponseIds;
    private bool toolReturned;
    private long? legacyTotal;

    internal void Context(JsonElement payload)
    {
        if (payload.Str("model") is { Length: > 0 } name)
        {
            model = name;
            if (payload.Str("turn_id") is { Length: > 0 } turn) models[turn] = name;
        }
        if (payload.Str("prompt_cache_retention") is { Length: > 0 } duration) retention = duration;
    }

    internal ResponseKey Output(long at, int line, string? title = null, string? call = null)
    {
        if (toolReturned) Boundary(at, line);
        if (current == null)
        {
            current = new(stream, "line:" + line.ToString(CultureInfo.InvariantCulture));
            started = at;
            emitted.Add((at, current.Value));
        }
        if (!string.IsNullOrWhiteSpace(title)) message ??= TraceJson.Caption(title);
        if (!string.IsNullOrEmpty(call)) calls.TryAdd(call, (current.Value, line));
        return current.Value;
    }

    internal void Usage(JsonElement payload, long at, int line)
    {
        var id = payload.Str("response_id");
        if (id.Length == 0) return;
        hasResponseIds = true;
        var key = new ResponseKey(stream, id);
        var actualModel = payload.Str("model") is { Length: > 0 } name ? name : models.GetValueOrDefault(payload.Str("turn_id")) ?? model;
        var duration = payload.Str("prompt_cache_retention");
        var usage = ResponseUsageReader.OpenAi(payload.Field("usage"), duration.Length > 0 ? duration : retention);
        var cost = SessionApiPrices.Price("openai", actualModel, usage);
        if (trace.Responses.TryGetValue(key, out var previous))
        {
            if (previous.Cost.Usage == null && usage != null) trace.Responses[key] = previous with { Cost = cost };
            return;
        }
        Close(at, line, key, cost);
    }

    internal void LegacyUsage(JsonElement info, long at, int line)
    {
        if (hasResponseIds) return;
        var total = ResponseUsageReader.Count(info.Field("total_token_usage"), "total_tokens");
        if (total.HasValue && total == legacyTotal) return;
        var usage = ResponseUsageReader.OpenAi(info.Field("last_token_usage"), retention);
        if (usage == null || !total.HasValue && current == null) return;
        legacyTotal = total;
        Close(at, line, null, SessionApiPrices.Price("openai", model, usage));
    }

    internal void Boundary(long at, int line)
    {
        if (current.HasValue) Close(at, line, null, ResponseCost.Missing(model, SessionApiPrices.Snapshot));
    }

    internal void ToolReturned(ResponseKey? response)
    {
        if (current != null && current == response) toolReturned = true;
    }

    private void Close(long at, int line, ResponseKey? key, ResponseCost cost)
    {
        var provisional = current ?? Output(at, line);
        var actual = key ?? provisional;
        resolved[provisional] = actual;
        trace.Responses[actual] = new(actual, agent, started, message, cost);
        current = null;
        message = null;
        toolReturned = false;
    }

    internal ResponseKey? Resolve(ResponseKey? key) => key.HasValue ? resolved.GetValueOrDefault(key.Value, key.Value) : null;

    internal (ResponseKey? Key, int Order) Tool(string id) => calls.TryGetValue(id, out var tool) ? (tool.Key, tool.Order) : (null, 0);

    internal ResponseKey Missing(long at, int line)
    {
        var key = new ResponseKey(stream, "runtime:" + line.ToString(CultureInfo.InvariantCulture));
        trace.Responses.TryAdd(key, new(key, agent, at, null, ResponseCost.Missing(snapshot: SessionApiPrices.Snapshot)));
        return key;
    }

    internal ResponseKey? Runtime(long start)
    {
        var first = 0;
        var last = emitted.Count;
        while (first < last)
        {
            var middle = first + (last - first) / 2;
            if (emitted[middle].At <= start) first = middle + 1;
            else last = middle;
        }
        if (first == 0) return null;
        var key = Resolve(emitted[first - 1].Key)!.Value;
        return trace.Responses.GetValueOrDefault(key)?.Cost.Usage != null ? key : null;
    }
}

// @implements InspectAgentSession
using System.Text.Json;

namespace Xcmd.Infrastructure;

internal static class TraceJson
{
    internal static long Number(this JsonElement value, string name) =>
        value.Field(name) is { ValueKind: JsonValueKind.Number } n && n.TryGetInt64(out var result) ? result : 0;
    internal static string Words(this JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Array => string.Join("\n", value.EnumerateArray().Select(v => v.Str("text")).Where(s => s.Length > 0)),
        _ => ""
    };

    internal static JsonElement Object(string raw)
    {
        if (!raw.TrimStart().StartsWith('{')) return default;
        try { using var doc = JsonDocument.Parse(raw); return doc.RootElement.Clone(); }
        catch (JsonException) { return default; }
    }

    internal static string Caption(string text)
    {
        var line = text.Trim().Split(['\r', '\n'], 2)[0];
        return line.Length <= 220 ? line : line[..219] + "…";
    }
}

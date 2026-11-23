// @implements ViewRecentAgentSessions
using System.Text.Json;

namespace Xcmd.Infrastructure;

internal static class SessionJsonFields
{
    internal static JsonElement Field(this JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) ? field : default;

    internal static string Str(this JsonElement value, string name) =>
        value.Field(name) is { ValueKind: JsonValueKind.String } field ? field.GetString() ?? "" : "";

    internal static IEnumerable<JsonElement> Items(this JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
}

// @implements InspectAgentSession
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Xcmd.Infrastructure;

internal sealed partial class ClassificationText(string project)
{
    internal const int Expanded = 12000;
    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    [GeneratedRegex("""(?i)[a-z]:[\\/]+users[\\/]+[^\\/\s"']+""")]
    private static partial Regex UserDirectory();

    [GeneratedRegex("""(?i)(?:AIza[\w-]{30,}|(?:authorization|access[_-]?token|api[_-]?key|password|secret)\s*["']?\s*[:=]\s*(?:"[^"\r\n]*"|'[^'\r\n]*'|(?:(?:Bearer|Basic)\s+)?[^\s"',;}\]]+))""")]
    private static partial Regex Credential();

    [GeneratedRegex("[A-Za-z0-9+/=_-]{180,}")]
    private static partial Regex Encoded();

    internal string Location(string value) => Clean(value.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath : value);

    internal string Clean(string value)
    {
        if (project.Length > 0)
        {
            value = value.Replace(project.Replace('\\', '/'), ".", StringComparison.OrdinalIgnoreCase)
                .Replace(project.Replace('/', '\\'), ".", StringComparison.OrdinalIgnoreCase);
        }
        var masked = CoverageExcerpt.Redact(Credential().Replace(value, "[redacted]"));
        return Encoded().Replace(UserDirectory().Replace(masked, "~"), "[encoded data]");
    }

    internal static string Clip(string text, int limit)
    {
        if (text.Length <= limit) return text;
        const string gap = " [...] ";
        var tail = (limit - gap.Length) / 3;
        return text[..(limit - gap.Length - tail)] + gap + text[^tail..];
    }

    // Sanitize structured values before escaping them as a single JSON field.
    internal string Clean(JsonElement field) => field.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", field.EnumerateObject().Select(p =>
            JsonSerializer.Serialize(Clean(p.Name)) + ":" + (IsSecret(p.Name) ? "\"[redacted]\"" : JsonValue(p.Value)))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", field.EnumerateArray().Select(JsonValue)) + "]",
        JsonValueKind.String => Clean(field.GetString() ?? ""),
        JsonValueKind.Undefined => "",
        _ => field.GetRawText()
    };

    private string JsonValue(JsonElement field) => field.ValueKind == JsonValueKind.String ? JsonSerializer.Serialize(Clean(field)) : Clean(field);
    private static bool IsSecret(string name) => Credential().IsMatch(name + "=value");
}

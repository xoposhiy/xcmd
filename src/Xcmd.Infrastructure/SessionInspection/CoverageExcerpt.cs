// @implements InspectAgentSession
using System.Text.RegularExpressions;

namespace Xcmd.Infrastructure;

internal static partial class CoverageExcerpt
{
    [GeneratedRegex("""(?i)(?:sk-(?:proj-)?[a-z0-9_-]{12,}|gh[pousr]_[a-z0-9]{12,}|github_pat_[a-z0-9_]+|Bearer\s+[a-z0-9._/-]{8,}|(?:api[_-]?key|access[_-]?token|password|secret)\s*["']?\s*[:=]\s*["']?[^\s"',;]{4,})""")]
    private static partial Regex Secret();

    [GeneratedRegex(@"-----BEGIN [^\r\n]*PRIVATE KEY-----[\s\S]*?(?:-----END [^\r\n]*PRIVATE KEY-----|\z)")]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"\b(?:open|cat|sed|awk|Get-Content|Join-Path|read_text|read_bytes|ReadAll\w+|ReadFile\w*|ParseFile|readlines)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FileOperation();

    internal static string Command(string command)
    {
        var value = Redact(command);
        if (value.Length <= 10000) return value;
        var middle = value[3000..^3000].Split('\n');
        var relevant = new List<string>();
        var length = 0;
        var last = -2;
        for (int i = 0; i < middle.Length; i++)
        {
            var match = FileOperation().Match(middle[i]);
            if (!match.Success) continue;
            int first = Math.Max(last + 1, Math.Max(0, i - 2));
            int end = Math.Min(middle.Length - 1, i + 1);
            if (first > end) continue;
            var block = middle[i].Length > 900 ? middle[i].Substring(Math.Clamp(match.Index - 400, 0, middle[i].Length - 900), 900)
                : Clip(string.Join('\n', middle[first..(end + 1)]), 900);
            if (length + block.Length + 24 > 3850) break;
            if (first > last + 1) relevant.Add("[excerpt omitted]");
            relevant.Add(block);
            length += block.Length + 24;
            last = end;
        }
        return value[..3000] + "\n[excerpt omitted]\n" + string.Join('\n', relevant) + "\n[excerpt omitted]\n" + value[^3000..];
    }

    internal static string Text(string value, int limit) => Clip(Redact(value), limit);

    internal static string Redact(string value) => Secret().Replace(PrivateKey().Replace(value, "[redacted]"), "[redacted]");

    private static string Clip(string value, int limit)
    {
        if (value.Length <= limit) return value;
        var half = (limit - 24) / 2;
        return value[..half] + "\n[excerpt omitted]\n" + value[^half..];
    }
}

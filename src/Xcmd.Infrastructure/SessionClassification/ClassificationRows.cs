// @implements InspectAgentSession
using System.Text.Json;
using System.Text.RegularExpressions;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static partial class ClassificationRows
{
    internal static Dictionary<int, ToolClassification> Parse(string reply, ClassificationCall[] expected, bool expanded)
    {
        using var doc = JsonDocument.Parse(reply);
        var calls = expected.ToDictionary(c => c.Call.Id);
        var rows = new Dictionary<int, ToolClassification>();
        var seen = new HashSet<int>();
        foreach (var row in doc.RootElement.Field("rows").Items())
        {
            var field = row.Field("id");
            if (field.ValueKind != JsonValueKind.Number || !field.TryGetInt32(out var id) || !calls.TryGetValue(id, out var input)) continue;
            if (!seen.Add(id)) { rows.Remove(id); continue; }
            if (ReadAnswer(row, input.Call, expanded) is { } answer) rows[id] = answer;
        }
        return rows;
    }

    private static ToolClassification? ReadAnswer(JsonElement row, CallEvidence call, bool expanded)
    {
        var summary = row.Str("summary").Trim();
        var pairs = ReadPairs(row.Field("pairs"));
        if (pairs.Length == 0 || summary.Length == 0 || GenericSummary().IsMatch(summary)) return null;
        if (call.Tool is "FileChange" or "apply_patch" && pairs.Any(p => p.Label.Action == "exec")) return null;
        if (call.Failed && pairs.Any(p => p.Label.Action == "write") && (!expanded || call.Tool == "FileChange")) return null;
        var resources = ReadResources(row.Field("resources"));
        return resources == null ? null : new(summary, pairs, resources);
    }

    [GeneratedRegex(@"^(?:(?:the |this )?(?:agent |tool |call )?(?:ran|executed|performed|used|processed|completed|handled) (?:the |a |an |this )?(?:tool(?: call)?|shell command|command|operation|task)|(?:read|updated|inspected|checked|modified) (?:the |a |some )?(?:file[s]?|data|output|code))\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex GenericSummary();

    private static ToolResource[]? ReadResources(JsonElement field)
    {
        if (field.ValueKind != JsonValueKind.Array) return null;
        var resources = field.EnumerateArray().Select(item => new ToolResource(item.Str("kind"), item.Str("value").Trim())).ToArray();
        return resources.All(r => r.IsValid) ? resources.Distinct().ToArray() : null;
    }

    private static ToolOperation[] ReadPairs(JsonElement pairs)
    {
        var groups = new Dictionary<ToolLabel, List<string>>();
        foreach (var pair in pairs.Items())
        {
            var label = new ToolLabel(pair.Str("target"), pair.Str("action"));
            var objects = pair.Field("objects").Items().Select(o => o.ValueKind == JsonValueKind.String ? o.GetString()!.Trim() : "").ToArray();
            if (!label.IsValid || objects.Length == 0 || objects.Any(o => o.Length == 0)) return [];
            if (!groups.TryGetValue(label, out var values)) groups[label] = values = [];
            foreach (var obj in objects)
                if (!values.Contains(obj, StringComparer.Ordinal)) values.Add(obj);
        }
        return groups.Select(group => new ToolOperation(group.Key, [.. group.Value])).ToArray();
    }
}

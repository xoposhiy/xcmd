// @implements InspectAgentSession
using System.Text;
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed record ClassificationCall(CallEvidence Call, SessionMessage[] Recent, SessionMessage[] Between, string Fingerprint);

internal static class ClassificationPacket
{
    private static readonly string Instructions = ReadInstructions();
    internal static readonly string Version = ClassificationText.Hash("projection-1/validation-1\n" + Request("", [], false));

    private static string ReadInstructions()
    {
        using var stream = typeof(ClassificationPacket).Assembly.GetManifestResourceStream("Xcmd.SessionClassification.Instructions")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    internal static ClassificationCall[] Prepare(IEnumerable<TraceCall> calls, ClassificationEvidence evidence, string project)
    {
        var recent = new Dictionary<string, SessionMessage>();
        var index = 0;
        var rows = new List<ClassificationCall>();
        foreach (var call in calls.Select(c => evidence.Calls[c.Id]).OrderBy(c => c.Line).ThenBy(c => c.Id))
        {
            var start = index;
            while (index < evidence.Messages.Count && evidence.Messages[index].Line <= call.Line) index++;
            var between = evidence.Messages.GetRange(start, index - start).ToArray();
            var carry = recent.Values.Where(old => !between.Any(m => m.Speaker == old.Speaker)).OrderBy(m => m.Line).ToArray();
            foreach (var message in between) recent[message.Speaker] = message;
            var digest = ClassificationText.Hash(JsonSerializer.Serialize(new
            {
                project,
                call.Digest,
                call.Data,
                messages = carry.Concat(between).Select(m => m.Data)
            }));
            rows.Add(new(call, carry, between, digest));
        }
        return [.. rows];
    }

    internal static string Request(string project, ClassificationCall[] calls, bool expanded)
    {
        var input = new StringBuilder("Return JSON. Project ").Append(project).Append('\n');
        int previousLine = -1;
        foreach (var row in calls)
        {
            foreach (var message in row.Recent.Concat(row.Between).Where(m => m.Line > previousLine))
                input.Append(expanded ? message.Data.Detail : message.Data.Brief).Append('\n');
            input.Append(expanded ? row.Call.Data.Detail : row.Call.Data.Brief).Append('\n');
            previousLine = row.Call.Line;
        }
        return JsonSerializer.Serialize(new
        {
            model = "gpt-6-luna",
            reasoning = new { effort = "medium" },
            store = false,
            truncation = "disabled",
            max_output_tokens = Math.Min(128000, 1800 + 115 * calls.Length),
            instructions = Instructions,
            input = input.ToString().TrimEnd('\n'),
            text = new { format = new { type = "json_object" } }
        });
    }
}

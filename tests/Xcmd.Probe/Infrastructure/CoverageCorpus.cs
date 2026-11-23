// @tests InspectAgentSession
using System.Text.Json;
using System.Text.Json.Nodes;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

internal static class CoverageCorpus
{
    private const string Project = "C:/work/projects/point-taken";
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static async Task CheckAsync(ProbeContext context)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SessionCoverage");
        var cases = Directory.GetFiles(folder, "*.jsonl").SelectMany(File.ReadLines).Select(s => JsonNode.Parse(s)!).ToArray();
        var errors = new List<string>();
        var scores = new Dictionary<string, Score>();
        foreach (var group in cases.GroupBy(c => Text(c, "kind") == "claude" ? SessionAgent.ClaudeCode : SessionAgent.Codex))
        {
            var rows = group.ToArray();
            var dir = Path.Combine(context.FixturePath, "corpus");
            var calls = await ReadAsync(rows, group.Key, dir);
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var events = calls[i + 1].ToArray();
                if (events.Length != 1) errors.Add($"{row["id"]} produced {events.Length} completed calls.");
                var actual = events.SelectMany(c => c.Files).ToArray();
                var expected = row["expected"]!.AsArray();
                var labels = expected.Concat(row["knownOmissions"]?.AsArray() ?? []).Select(f => f!).ToArray();
                var split = Text(row, "split");
                if (!scores.TryGetValue(split, out var score)) scores.Add(split, score = new());
                score.Actions++;
                score.Correct += labels.Count(f => actual.Any(a => Matches(a, f)));
                score.Missed += labels.Count(f => !actual.Any(a => Matches(a, f)));
                var extra = actual.Count(a => !labels.Any(f => Matches(a, f)));
                score.Extra += extra;
                if (extra == 0 && actual.Distinct().Count() == actual.Length && expected.All(f => actual.Any(a => Matches(a, f!)))) continue;
                errors.Add($"{row["id"]} expected {expected.ToJsonString()}, found {JsonSerializer.Serialize(actual)}");
            }
        }
        context.Measurements["coverage"] = scores;
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }

    internal static async Task<ILookup<long, TraceCall>> ReadAsync(JsonNode[] rows, SessionAgent provider, string dir)
    {
        var session = new AgentSession(provider, Guid.NewGuid(), "File evidence", Project, Epoch);
        var path = provider == SessionAgent.Codex ? Path.Combine(dir, "sessions") : Path.Combine(dir, "projects", "test");
        Directory.CreateDirectory(path);
        File.WriteAllLines(Path.Combine(path, session.Id + ".jsonl"), rows.SelectMany((row, index) => Events(row, index + 1)));
        var trace = await SessionTranscriptReader.ForFileCoverageStudy(dir).ReadAsync(session, default);
        return trace.Calls.ToLookup(call => (call.End - Epoch.ToUnixTimeMilliseconds()) / 1000);
    }

    private sealed class Score
    {
        public int Actions { get; set; }
        public int Correct { get; set; }
        public int Extra { get; set; }
        public int Missed { get; set; }
    }

    private static bool Matches(TraceFile actual, JsonNode expected) =>
        actual.Path == Text(expected, "path") && actual.Action == Text(expected, "action") && (expected["first"] == null ||
        actual.FirstLine == (int)expected["first"]! && actual.Lines == (int)expected["lines"]! && actual.TotalLines == (int)expected["total"]!);

    private static string Text(JsonNode row, string key) => (string?)row[key] ?? "";

    private static IEnumerable<string> Events(JsonNode row, int index)
    {
        var time = Epoch.AddSeconds(index);
        var id = Text(row, "id"); var kind = Text(row, "kind"); var cwd = Text(row, "cwd");
        var tool = Text(row, "tool"); var command = Text(row, "cmd"); var output = Text(row, "output");
        if (row["returnedLines"] is { } count)
            output = string.Concat(Enumerable.Repeat(output.Split('\n')[0].TrimEnd('\r') + "\n", (int)count)) + Text(row, "recordTerminator");
        string Event(string type, object payload) => JsonSerializer.Serialize(new { timestamp = time, type, payload });
        if (kind == "claude")
        {
            yield return JsonSerializer.Serialize(new { timestamp = time, cwd, type = "assistant", message = new { content = new[] { new { type = "tool_use", id, name = tool, input = row["input"] } } } });
            yield return JsonSerializer.Serialize(new { timestamp = time, cwd, type = "user", message = new { content = new[] { new { type = "tool_result", tool_use_id = id, is_error = (bool?)row["error"] == true, content = output } } }, toolUseResult = row["result"] });
            yield break;
        }
        if (kind == "raw")
        {
            yield return Event("turn_context", new { cwd });
            var arguments = tool == "shell_command" ? JsonSerializer.Serialize(new { command, workdir = cwd }) : JsonSerializer.Serialize(new { cmd = command, workdir = cwd });
            yield return Event("response_item", new { type = "function_call", call_id = id, name = tool, arguments = tool == "apply_patch" ? command : arguments });
            yield return Event("response_item", new { type = "function_call_output", call_id = id, output = (string?)row["rawResult"] ?? JsonSerializer.Serialize(new { output, exit_code = row["exit"] }) });
            yield break;
        }
        object item;
        if (kind == "change") item = new { type = "FileChange", id, cwd, status = row["status"], changes = row["changes"]!.AsObject().ToDictionary(p => p.Key, p => new { type = p.Value }) };
        else
        {
            var shell = (string?)row["shell"] ?? "pwsh";
            var flag = shell switch { "bash" => "-lc", "cmd" => "/c", _ => "-Command" };
            item = new { type = "CommandExecution", id, cwd, command = new[] { shell, flag, command }, aggregated_output = output, parsed_cmd = row["hint"], exit_code = row["exit"], status = row["status"] };
        }
        yield return Event("event_msg", new { type = "item_completed", item });
    }
}

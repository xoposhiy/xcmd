// @tests InspectAgentSession
using System.Text.Json.Nodes;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

internal static class CoverageReplay
{
    internal static async Task CheckAsync(ProbeContext c)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SessionCoverage");
        var claims = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "luna-replies.json")))!.AsArray()
            .ToDictionary(row => (string)row!["id"]!, row => row!);
        var rows = Directory.GetFiles(dir, "*.jsonl").SelectMany(File.ReadLines).Select(s => JsonNode.Parse(s)!)
            .Where(row => claims.ContainsKey((string)row["id"]!)).ToArray();
        ProbeContext.Equal(claims.Count, rows.Length, "Each saved review maps to one tool call");
        foreach (var group in rows.GroupBy(row => (string?)row["kind"] == "claude" ? SessionAgent.ClaudeCode : SessionAgent.Codex))
        {
            var cases = group.ToArray();
            var calls = await CoverageCorpus.ReadAsync(cases, group.Key, Path.Combine(c.FixturePath, "transcripts"));
            for (int i = 0; i < cases.Length; i++)
            {
                var id = (string)cases[i]["id"]!;
                var recorded = claims[id];
                var call = calls[i + 1].Single();
                using var http = new HttpClient(new CoverageClientFixture.Transport { Answer = recorded["answer"]!.ToJsonString() });
                var client = new LunaCoverageClient(Path.Combine(c.FixturePath, id), () => "fixture", http);
                var reads = call.Files.Where(f => f.Action == "Read").Select(f => f.Path);
                if (call.Review is { } input)
                    reads = reads.Concat((await client.ReviewAsync(input, default)).Reads.Select(f => f.Path));
                var expected = recorded["expected"]!.AsArray().Select(node => (string)node!).Order(StringComparer.Ordinal);
                ProbeContext.Equal(string.Join(", ", expected), string.Join(", ", reads.Distinct().Order(StringComparer.Ordinal)), id);
            }
        }
        await CheckContextAsync(c);
    }

    private static async Task CheckContextAsync(ProbeContext c)
    {
        var pad = string.Concat(Enumerable.Repeat("# padding\n", 720));
        var row = new JsonObject
        {
            ["kind"] = "command",
            ["id"] = "long-script",
            ["shell"] = "pwsh",
            ["cwd"] = "C:/test",
            ["cmd"] = pad + "$p='menu.md'\n[IO.File]::ReadAllText($p)\n" + pad,
            ["output"] = "api_key=private-fixture-value\n" + new string('x', 8000) + "\nlast line",
            ["exit"] = 0,
            ["status"] = "completed"
        };
        var calls = await CoverageCorpus.ReadAsync([row], SessionAgent.Codex, Path.Combine(c.FixturePath, "long-script"));
        var input = calls[1].Single().Review!;
        ProbeContext.Check(input.Command.Contains("menu.md") && input.Command.Contains("ReadAllText"), "Keep the operation and local binding in a long script");
        ProbeContext.Check(input.Command.Length <= 10000 && input.Result.Length <= 6000, "Limit both excerpts");
        ProbeContext.Check(!input.Result.Contains("private-fixture-value") && input.Result.Contains("[redacted]") &&
            input.Result.EndsWith("last line", StringComparison.Ordinal), "Remove credentials and retain result boundaries");
    }
}

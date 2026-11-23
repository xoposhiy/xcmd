// @tests InspectAgentSession
using System.Text.Json;
using System.Text.Json.Nodes;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

internal static class LunaEvaluation
{
    internal static async Task<int> RunAsync(string folder, string split)
    {
        Directory.CreateDirectory(folder);
        using var exclusive = new FileStream(Path.Combine(folder, "evaluation.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        using var http = new HttpClient(new EvaluationMeter(folder)) { Timeout = TimeSpan.FromSeconds(90) };
        var reviewer = new LunaCoverageClient(Path.Combine(folder, "cache"), () => Environment.GetEnvironmentVariable("OPENAI_API_KEY"), http);
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SessionCoverage");
        var cases = Directory.GetFiles(fixture, "*.jsonl").SelectMany(File.ReadLines).Select(s => JsonNode.Parse(s)!)
            .Where(row => split == "all" || (string?)row["split"] == split).ToArray();
        if (cases.Length == 0) throw new ArgumentException("Unknown evaluation split.");
        var scores = new List<object>();
        foreach (var group in cases.GroupBy(row => (string?)row["kind"] == "claude" ? SessionAgent.ClaudeCode : SessionAgent.Codex))
        {
            var rows = group.ToArray();
            var calls = await CoverageCorpus.ReadAsync(rows, group.Key, Path.Combine(folder, "transcripts"));
            await Parallel.ForEachAsync(Enumerable.Range(0, rows.Length), new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (index, token) =>
            {
                var row = rows[index];
                var call = calls[index + 1].Single();
                var answer = call.Review == null ? new CoverageAnswer([], false) : await reviewer.ReviewAsync(call.Review, token);
                var labels = row["expected"]!.AsArray().Concat(row["knownOmissions"]?.AsArray() ?? []).Where(f => (string?)f!["action"] == "Read")
                    .Select(f => (string)f!["path"]!).ToHashSet();
                var before = call.Files.Where(f => f.Action == "Read").Select(f => f.Path).ToHashSet();
                var after = before.Concat(answer.Reads.Select(f => f.Path)).ToHashSet();
                var score = new
                {
                    id = (string?)row["id"],
                    split = (string?)row["split"],
                    routed = call.Review != null,
                    baseline = before.Intersect(labels).Count(),
                    correct = after.Intersect(labels).Count(),
                    extra = after.Except(labels).ToArray(),
                    missed = labels.Except(after).ToArray(),
                    unresolved = answer.Unresolved,
                    reads = answer.Reads
                };
                lock (scores) scores.Add(score);
            });
        }
        await File.WriteAllTextAsync(Path.Combine(folder, "scores-" + split + ".json"), JsonSerializer.Serialize(scores));
        Console.WriteLine($"Scored {scores.Count} calls in {split}.");
        return 0;
    }

    private sealed class EvaluationMeter(string folder) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        private readonly object sync = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var bytes = (await request.Content!.ReadAsByteArrayAsync(token)).Length;
            var reserve = (bytes + 1024) * 0.0000001m + 1600 * 0.0000005m;
            Record(reserve);
            var response = await base.SendAsync(request, token);
            if (response.IsSuccessStatusCode)
            {
                var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(token));
                if (root?["usage"] is { } usage)
                {
                    var input = (long)usage["input_tokens"]!;
                    var output = (long)usage["output_tokens"]!;
                    Record(input * 0.0000001m + output * 0.0000005m - reserve);
                }
            }
            return response;
        }

        private void Record(decimal amount)
        {
            lock (sync)
            {
                var path = Path.Combine(folder, "spend-usd.json");
                var balance = File.Exists(path) ? JsonSerializer.Deserialize<decimal>(File.ReadAllText(path)) : 0m;
                if (balance + amount > 1m) throw new InvalidOperationException("The live benchmark reached its one-dollar budget.");
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(balance + amount));
                File.Move(path + ".tmp", path, true);
            }
        }
    }
}

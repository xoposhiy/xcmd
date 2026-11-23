// @tests InspectAgentSession, OpenViewers
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xcmd.Core;
using Xcmd.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

internal sealed class ClassificationFixture : IDisposable
{
    private readonly string root;
    private readonly string file;
    private readonly HttpClient http;
    private readonly ClassificationTransport transport = new();
    internal AgentSession Session { get; }
    internal bool Offline { get; set; }
    internal int[][] Packets => transport.Packets.ToArray();
    internal int Peak => transport.Peak;
    internal bool UnreadableReply { set => transport.UnreadableReply = value; }
    internal bool DamageFirstReply { set => transport.DamageFirstReply = value; }
    internal bool CheckResourceReplies { set => transport.CheckResourceReplies = value; }
    internal void Reset() => transport.Packets.Clear();

    internal ClassificationFixture(ProbeContext c)
    {
        root = c.FixturePath;
        file = Path.Combine(root, "classification.jsonl");
        Session = c.AgentSessions.AddCodexSession("Classify runtime operations", c.LeftPath, DateTimeOffset.UtcNow) with { TranscriptPath = file };
        http = new(transport);
        c.AgentSessions.Inspector = (_, token) => ReadAsync(token);
    }

    internal void Write(int count)
    {
        File.WriteAllLines(file, new[] { Event(new { type = "UserMessage", content = new[] { new { text = "Inspect component settings." } } }),
            Event(new { type = "AgentMessage", content = new[] { new { text = "Check the current configuration." } } }) }
            .Concat(Enumerable.Range(1, count).Select(Call)));
    }

    internal void Append(int n) => File.AppendAllLines(file, [Call(n)]);
    internal void Message(string text) => File.AppendAllLines(file, [Event(new { type = "UserMessage", content = new[] { new { text } } })]);

    internal void InvalidateVersion()
    {
        var cache = Directory.EnumerateFiles(Path.Combine(root, "cache"), "*.json", SearchOption.AllDirectories).Single();
        var json = JsonNode.Parse(File.ReadAllText(cache))!;
        json["Version"] = "previous-prompt";
        File.WriteAllText(cache, json.ToJsonString());
    }
    internal void Rewrite(string before, string after)
    {
        var original = File.ReadAllText(file);
        var changed = original.Replace(before, after, StringComparison.Ordinal);
        Check(changed != original, "Fixture edit changes a recorded field");
        File.WriteAllText(file, changed);
    }
    internal Task<SessionTrace> ReadAsync(CancellationToken token = default) =>
        new SessionTranscriptReader(root, root, new LunaSessionClassifier(Path.Combine(root, "cache"), () => Offline ? null : "fixture", http))
            .ReadAsync(Session, token);

    internal Task<SessionTrace[]> OpenTwoInspectorsAsync() => Task.WhenAll(ReadAsync(), ReadAsync());

    internal Task<SessionTrace> ReadClaudeAsync(AgentSession session)
    {
        transport.CheckCommandFixture = false;
        var transcripts = Path.Combine(root, "inspector");
        return new SessionTranscriptReader(transcripts, transcripts, new LunaSessionClassifier(Path.Combine(root, "cache"), () => Offline ? null : "fixture", http))
            .ReadAsync(session, default);
    }

    private static string Call(int n) => Event(new
    {
        type = "CommandExecution",
        id = "tool-" + n,
        command = new[] { "pwsh", "-Command", "read component-" + n + ".cs " + string.Concat(Enumerable.Repeat("abcdef ", 400)) + " ending" },
        aggregated_output = "api_key=fixture-secret-value password='secret with spaces' Authorization: Basic TEST-BASIC-12345 " + new string('A', 300) + " done",
        exit_code = 0
    }, n);

    private static string Event(object item, int seconds = 0)
    {
        var end = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero).AddSeconds(seconds);
        return JsonSerializer.Serialize(new
        {
            timestamp = end,
            type = "event_msg",
            payload = new { type = "item_completed", item, started_at_ms = end.AddSeconds(-1).ToUnixTimeMilliseconds(), completed_at_ms = end.ToUnixTimeMilliseconds() }
        });
    }

    public void Dispose() => http.Dispose();

    private sealed class ClassificationTransport : HttpMessageHandler
    {
        internal readonly ConcurrentQueue<int[]> Packets = new();
        internal bool DamageFirstReply;
        internal bool CheckResourceReplies;
        internal bool UnreadableReply;
        internal bool CheckCommandFixture = true;
        internal int Peak;
        private int running;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var active = Interlocked.Increment(ref running);
            lock (Packets) Peak = Math.Max(Peak, active);
            try
            {
                await Task.Delay(25, token);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                var data = body.RootElement;
                Equal("gpt-6-luna", data.GetProperty("model").GetString(), "Classification model");
                Equal("medium", data.GetProperty("reasoning").GetProperty("effort").GetString(), "Classification reasoning effort");
                Equal("json_object", data.GetProperty("text").GetProperty("format").GetProperty("type").GetString(), "JSON object output");
                Equal("disabled", data.GetProperty("truncation").GetString(), "Keep full request");
                Check(!data.GetProperty("store").GetBoolean() && !data.TryGetProperty("tools", out _), "Private request without tools");
                var input = data.GetProperty("input").GetString()!;
                Check(input.StartsWith("Return JSON. Project ", StringComparison.Ordinal), "JSON mode input header");
                if (CheckCommandFixture)
                {
                    Check(!input.Contains("fixture-secret-value") && !input.Contains("with spaces") && !input.Contains("TEST-BASIC") && input.Contains("[encoded data]"),
                        "Both projections hide credentials and blobs");
                    Check(input.Contains("UserMessage") && input.Contains("Check the current configuration."), "Each packet carries both speakers");
                }
                var records = input.Split('\n').Skip(1).Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
                var calls = records.Where(v => v.TryGetProperty("id", out _)).ToArray();
                var ids = calls.Select(v => v.GetProperty("id").GetInt32()).ToArray();
                Equal(1800 + 115 * ids.Length, data.GetProperty("max_output_tokens").GetInt32(), "Output budget follows the requested IDs");
                if (DamageFirstReply && Packets.Count > 0)
                    Check(calls[0].GetProperty("command").GetString()!.Length > 1800, "Repair includes expanded evidence");
                Packets.Enqueue(ids);
                var rows = ids.Select(id => Row(id)).ToList();
                if (DamageFirstReply && ids.Length > 1)
                {
                    rows.Add(Row(ids[0]));
                    rows[1] = Row(ids[1], "Ran the command.");
                    rows.Add(Row(99999));
                }
                if (CheckResourceReplies)
                {
                    for (var i = 0; i < ids.Length; i++)
                    {
                        var row = JsonSerializer.SerializeToNode(rows[i])!.AsObject();
                        if (ids.Length > 1 && i == 0) row.Remove("resources");
                        else if (ids.Length > 1 && i == 1) row["resources"] = new JsonArray(new JsonObject { ["kind"] = "page", ["value"] = "page1" });
                        else if (ids[i] == 5) row["resources"] = new JsonArray();
                        rows[i] = row;
                    }
                }
                var reply = UnreadableReply ? "{" : JsonSerializer.Serialize(new { rows });
                var output = new[] { new { content = new[] { new { type = "output_text", text = reply } } } };
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { status = "completed", output })) };
            }
            finally { Interlocked.Decrement(ref running); }
        }

        private static object Row(int id, string? summary = null) => new
        {
            id,
            summary = summary ?? $"Read and updated component {id}.",
            resources = new[] { new { kind = "file", value = $"src/Component{id:00}.cs" }, new { kind = "directory", value = "src" },
                new { kind = "url", value = "https://example.test/Docs?q=1" }, new { kind = "issue", value = "tracker#49" },
                new { kind = "repository", value = "org/project" }, new { kind = "other", value = "process/worker" },
                new { kind = "url", value = "https://example.test/Docs?q=1" } },
            pairs = new[] { new { target = "source", action = "read", objects = new[] { "settings.cs" } },
                new { target = "source", action = "write", objects = new[] { "settings.cs" } },
                new { target = "source", action = "read", objects = new[] { "defaults.cs" } } }
        };
    }
}

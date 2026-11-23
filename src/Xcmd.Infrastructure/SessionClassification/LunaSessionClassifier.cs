// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class LunaSessionClassifier(string cacheDirectory, Func<string?> apiKey, HttpClient http)
{
    private static readonly HttpClient SharedHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(15) };
    private static readonly SemaphoreSlim Requests = new(5);
    public static LunaSessionClassifier Local() => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Xcmd", "session-classification"), LunaResponses.ReadKey, SharedHttp);

    internal async Task ClassifyAsync(AgentSession session, TraceRecords trace, IProgress<string>? progress, CancellationToken token)
    {
        var answers = new Dictionary<(string Agent, int Id), ToolClassification>();
        var run = new ClassificationRun(progress, trace.Calls.Count);
        var project = new ClassificationText(session.ProjectDirectory).Clean(session.ProjectName).ReplaceLineEndings(" ");
        var folder = Path.Combine(cacheDirectory, session.Agent.ToString(), session.Id.ToString("D"));
        run.Report(0);
        foreach (var group in trace.Calls.GroupBy(c => c.Agent))
        {
            var evidence = trace.Evidence[group.Key];
            var calls = ClassificationPacket.Prepare(group, evidence, project);
            var cache = new ClassificationCache(Path.Combine(folder, evidence.Stream + ".json"));
            var found = await ClassifyStreamAsync(project, calls, cache, run, token).ConfigureAwait(false);
            foreach (var row in found) answers[(group.Key, row.Key)] = row.Value;
        }
        for (var i = 0; i < trace.Calls.Count; i++)
        {
            var call = trace.Calls[i];
            if (answers.TryGetValue((call.Agent, call.Id), out var row)) trace.Calls[i] = call with { Classification = row };
        }
        if (answers.Count < trace.Calls.Count)
            trace.ClassificationError = $"Luna classified {answers.Count:N0} of {trace.Calls.Count:N0} tool calls. " +
                (run.Error ?? "Some replies did not contain a valid classification.");
    }

    private async Task<Dictionary<int, ToolClassification>> ClassifyStreamAsync(string project, ClassificationCall[] calls,
        ClassificationCache cache, ClassificationRun run, CancellationToken token)
    {
        var found = new Dictionary<int, ToolClassification>();
        try
        {
            using var lease = await cache.LockAsync(token).ConfigureAwait(false);
            found = cache.Load(calls);
            run.Report(found.Count);
            var pending = calls.Where(c => !found.ContainsKey(c.Call.Id)).Chunk(20).ToArray();
            await Parallel.ForEachAsync(pending, new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = token }, ReviewBatch).ConfigureAwait(false);
        }
        catch (Exception e) when (Recoverable(e, token)) { run.Fail(e); }
        return found;

        async ValueTask ReviewBatch(ClassificationCall[] batch, CancellationToken ct)
        {
            if (run.Error != null) return;
            try
            {
                var rows = await Review(batch, false, ct).ConfigureAwait(false);
                foreach (var call in batch.Where(c => !rows.ContainsKey(c.Call.Id))) await Review([call], true, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (Recoverable(e, ct)) { run.Fail(e); }
        }

        async Task<Dictionary<int, ToolClassification>> Review(ClassificationCall[] batch, bool expanded, CancellationToken ct)
        {
            var rows = await RequestAsync(project, batch, expanded, ct).ConfigureAwait(false);
            if (rows.Count == 0) return rows;
            lock (cache)
            {
                cache.Store(batch, rows);
                foreach (var row in rows) found[row.Key] = row.Value;
                run.Report(rows.Count);
            }
            return rows;
        }
    }

    private sealed class ClassificationRun(IProgress<string>? progress, int total)
    {
        private int completed;
        private string? error;
        internal string? Error => Volatile.Read(ref error);
        internal void Fail(Exception e) => Interlocked.CompareExchange(ref error, e.Message, null);
        internal void Report(int count) => progress?.Report($"Classifying tool calls with Luna… {Interlocked.Add(ref completed, count):N0} of {total:N0}");
    }

    private static bool Recoverable(Exception e, CancellationToken token) =>
        e is IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException || e is OperationCanceledException && !token.IsCancellationRequested;

    private async Task<Dictionary<int, ToolClassification>> RequestAsync(string project, ClassificationCall[] batch, bool expanded, CancellationToken token)
    {
        var key = apiKey();
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Set OPENAI_API_KEY in the environment or in the .env file beside the Xcmd launcher.");
        await Requests.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var payload = ClassificationPacket.Request(project, batch, expanded);
            var reply = await LunaResponses.RequestAsync(http, payload, key, token).ConfigureAwait(false);
            return ClassificationRows.Parse(reply, batch, expanded);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException) { return []; }
        finally { Requests.Release(); }
    }
}

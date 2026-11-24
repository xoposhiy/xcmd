// @implements InspectAgentSession, ViewRecentAgentSessions
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class LunaSessionClassifier(string cacheDirectory, Func<string?> apiKey, HttpClient http)
{
    private static readonly HttpClient SharedHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(15) };
    private static readonly SemaphoreSlim Requests = new(5);
    public static LunaSessionClassifier Local() => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Xcmd", "session-classification"), LunaResponses.ReadKey, SharedHttp);

    internal async Task ApplyAsync(AgentSession session, TraceRecords trace, bool analyze, IProgress<string>? progress, CancellationToken token)
    {
        var answers = new Dictionary<(string Agent, int Id), CachedClassification>();
        var run = new ClassificationRun(progress, trace.Calls.Count);
        var project = Project(session);
        run.Report(0);
        foreach (var group in trace.Calls.GroupBy(c => c.Agent))
        {
            token.ThrowIfCancellationRequested();
            var evidence = trace.Evidence[group.Key];
            var path = CachePath(session, evidence);
            if (!analyze && !File.Exists(path)) continue;
            var calls = ClassificationPacket.Prepare(group, evidence, project);
            var cache = new ClassificationCache(path);
            Dictionary<int, CachedClassification> found;
            if (analyze) found = await ClassifyStreamAsync(project, calls, cache, run, token).ConfigureAwait(false);
            else
            {
                try { found = cache.Load(calls); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            }
            foreach (var row in found) answers[(group.Key, row.Key)] = row.Value;
        }
        for (var i = 0; i < trace.Calls.Count; i++)
        {
            var call = trace.Calls[i];
            if (answers.TryGetValue((call.Agent, call.Id), out var row)) trace.Calls[i] = call with { Classification = row.Classification };
        }
        var current = answers.Values.Count(row => row.IsCurrent);
        trace.HasOutdatedClassifications = current < answers.Count;
        if (analyze && current < trace.Calls.Count)
            trace.ClassificationError = $"Luna classified {current:N0} of {trace.Calls.Count:N0} tool calls with the current prompt. " +
                (run.Error ?? "Some replies did not contain a valid classification.");
    }

    internal ClassificationStatus StatusFor(AgentSession session) => new(Folder(session), session);

    internal bool HasAllCalls(AgentSession session, TraceRecords trace, ClassificationStatus status, CancellationToken token)
    {
        var project = Project(session);
        foreach (var group in trace.Calls.GroupBy(c => c.Agent))
        {
            token.ThrowIfCancellationRequested();
            var evidence = trace.Evidence[group.Key];
            var path = CachePath(session, evidence);
            status.Watch(path);
            var calls = ClassificationPacket.Prepare(group, evidence, project);
            if (new ClassificationCache(path).Load(calls).Count != calls.Length) return false;
        }
        return trace.Calls.Count > 0;
    }

    private string Folder(AgentSession session) => Path.Combine(cacheDirectory, session.Agent.ToString(), session.Id.ToString("D"));
    private string CachePath(AgentSession session, ClassificationEvidence evidence) => Path.Combine(Folder(session), evidence.Stream + ".json");
    private static string Project(AgentSession session) => new ClassificationText(session.ProjectDirectory).Clean(session.ProjectName).ReplaceLineEndings(" ");

    private async Task<Dictionary<int, CachedClassification>> ClassifyStreamAsync(string project, ClassificationCall[] calls,
        ClassificationCache cache, ClassificationRun run, CancellationToken token)
    {
        var found = new Dictionary<int, CachedClassification>();
        try
        {
            using var lease = await cache.LockAsync(token).ConfigureAwait(false);
            found = cache.Load(calls);
            var pending = calls.Where(c => !found.TryGetValue(c.Call.Id, out var row) || !row.IsCurrent).ToArray();
            run.Report(calls.Length - pending.Length);
            await Parallel.ForEachAsync(pending.Chunk(20), new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = token }, ReviewBatch).ConfigureAwait(false);
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

        async Task<Dictionary<int, CachedClassification>> Review(ClassificationCall[] batch, bool expanded, CancellationToken ct)
        {
            var answers = await RequestAsync(project, batch, expanded, ct).ConfigureAwait(false);
            var rows = answers.ToDictionary(row => row.Key, row => new CachedClassification(row.Value, ClassificationPacket.PromptVersion));
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

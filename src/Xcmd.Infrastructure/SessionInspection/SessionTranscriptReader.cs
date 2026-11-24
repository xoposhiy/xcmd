// @implements InspectAgentSession, OpenViewers, ViewRecentAgentSessions
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class SessionTranscriptReader(string codexHome, string claudeHome, LunaSessionClassifier? classifier = null)
{
    private bool FileCoverageStudy { get; init; }
    private readonly SemaphoreSlim statusReads = new(2);
    public static SessionTranscriptReader ForFileCoverageStudy(string home) => new(home, home) { FileCoverageStudy = true };
    public static SessionTranscriptReader Local() => new(Home("CODEX_HOME", ".codex"), Home("CLAUDE_CONFIG_DIR", ".claude"), LunaSessionClassifier.Local());

    private static string Home(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } dir ? dir
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);

    public Task<SessionTrace> ReadAsync(AgentSession session, CancellationToken token) =>
        ReadAsync(session, token, null, analyze: false);

    public Task<SessionTrace> AnalyzeAsync(AgentSession session, CancellationToken token, IProgress<string>? progress = null) =>
        ReadAsync(session, token, progress, analyze: true);

    private Task<SessionTrace> ReadAsync(AgentSession session, CancellationToken token, IProgress<string>? progress, bool analyze) => Task.Run(async () =>
    {
        var trace = ReadRecords(session, null, token);
        if (classifier != null) await classifier.ApplyAsync(session, trace, analyze, progress, token).ConfigureAwait(false);
        return trace.Finish(session);
    }, token);

    public Task<bool> IsClassificationReadyAsync(AgentSession session, CancellationToken token) => Task.Run(async () =>
    {
        if (classifier == null) return false;
        var status = classifier.StatusFor(session);
        if (status.Read() is { } ready) return ready;
        await statusReads.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (status.Read() is { } cached) return cached;
            var trace = ReadRecords(session, status, token);
            return trace.MissingAgents == 0 && status.Save(classifier.HasAllCalls(session, trace, status, token));
        }
        finally { statusReads.Release(); }
    }, token);

    private TraceRecords ReadRecords(AgentSession session, ClassificationStatus? status, CancellationToken token)
    {
        var trace = new TraceRecords(session.ProjectDirectory, FileCoverageStudy) { Status = status };
        if (session.Agent == SessionAgent.ClaudeCode) ReadClaude(session, trace, token);
        else ReadCodex(session, trace, token);
        return trace;
    }

    private void ReadCodex(AgentSession session, TraceRecords trace, CancellationToken token)
    {
        var queue = new Queue<(Guid Id, string Name)>();
        var seen = new HashSet<Guid> { session.Id };
        var names = new HashSet<string> { SessionTrace.MainAgent };
        queue.Enqueue((session.Id, SessionTrace.MainAgent));
        while (queue.TryDequeue(out var next))
        {
            token.ThrowIfCancellationRequested();
            var path = (next.Id == session.Id ? ExistingTranscript(session.TranscriptPath) : null) ?? FindCodex(next.Id, token);
            if (path == null)
            {
                if (next.Id == session.Id) throw Missing();
                trace.MissingAgents++;
                continue;
            }
            var parser = new CodexTraceReader(trace, next.Name, next.Id, next.Id == session.Id ? session.Directory : "", trace.ForAgent(next.Name, next.Id.ToString("D")));
            if (next.Id == session.Id) ReadLines(path, trace, parser.Record, token);
            else ReadRelated(trace, () => ReadLines(path, trace, parser.Record, token));
            parser.Finish();
            foreach (var child in trace.Children.Where(child => seen.Add(child.Key)))
                queue.Enqueue((child.Key, names.Add(child.Value) ? child.Value : child.Value + " · " + child.Key.ToString("N")[^8..]));
        }
    }

    private string? FindCodex(Guid id, CancellationToken token)
    {
        foreach (var folder in new[] { "sessions", "archived_sessions" })
        {
            var root = Path.Combine(codexHome, folder);
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directories(root))
            {
                token.ThrowIfCancellationRequested();
                var file = Directory.EnumerateFiles(dir, "*" + id.ToString("D") + ".jsonl*")
                    .FirstOrDefault(f => f.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jsonl.zst", StringComparison.OrdinalIgnoreCase));
                if (file != null) return file;
            }
        }
        return null;
    }

    private static string? ExistingTranscript(string? path) => File.Exists(path) ? path :
        path != null && File.Exists(path + ".zst") ? path + ".zst" : null;

    private static IEnumerable<string> Directories(string root) =>
        Directory.EnumerateDirectories(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        }).Prepend(root);

    private void ReadClaude(AgentSession session, TraceRecords trace, CancellationToken token)
    {
        var root = Path.Combine(claudeHome, "projects");
        if (!Directory.Exists(root)) throw Missing();
        var path = Directory.EnumerateDirectories(root).Select(dir => Path.Combine(dir, session.Id.ToString("D") + ".jsonl")).FirstOrDefault(File.Exists);
        if (path == null) throw Missing();
        Parse(path, SessionTrace.MainAgent);
        var children = Path.Combine(Path.GetDirectoryName(path)!, session.Id.ToString("D"), "subagents");
        trace.Status?.Watch(children, directory: true);
        if (Directory.Exists(children))
            foreach (var sub in Directory.EnumerateFiles(children, "agent-*.jsonl").Order(StringComparer.OrdinalIgnoreCase))
            {
                var id = Path.GetFileNameWithoutExtension(sub)["agent-".Length..];
                var label = "Agent " + id;
                var meta = Path.ChangeExtension(sub, ".meta.json");
                if (File.Exists(meta) && TraceJson.Object(File.ReadAllText(meta)).Str("agentType") is { Length: > 0 } type)
                    label = type + " · " + id;
                ReadRelated(trace, () => Parse(sub, label));
            }

        void Parse(string file, string agent)
        {
            var parser = new ClaudeTraceReader(trace, agent, agent == SessionTrace.MainAgent ? session.Directory : "", trace.ForAgent(agent, Path.GetFileNameWithoutExtension(file)));
            ReadLines(file, trace, parser.Record, token);
            trace.Unfinished += parser.Unfinished;
        }
    }

    private static FileNotFoundException Missing() => new("This session has no transcript in the local agent history. It may have been moved or saved on another device.");

    private static void ReadRelated(TraceRecords trace, Action read)
    {
        try { read(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { trace.MissingAgents++; }
    }

    private static void ReadLines(string path, TraceRecords trace, Action<JsonElement, long, int> record, CancellationToken token)
    {
        trace.Status?.Watch(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan);
        using var data = path.EndsWith(".zst", StringComparison.OrdinalIgnoreCase) ? ArchiveCompression.Open(stream, SharpCompress.Common.CompressionType.ZStandard) : stream;
        using var reader = new StreamReader(data);
        int number = 0;
        while (reader.ReadLine() is { } line)
        {
            number++;
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var json = JsonDocument.Parse(line);
                var root = json.RootElement;
                if (root.Field("timestamp") is { ValueKind: JsonValueKind.String } time && time.TryGetDateTimeOffset(out var at))
                    record(root, at.ToUnixTimeMilliseconds(), number);
            }
            catch (JsonException) { trace.Skipped++; }
        }
    }
}

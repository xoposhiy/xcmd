// @implements ViewRecentAgentSessions
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class SessionStatisticsReader
{
    public static Task<SessionStatistics?> ReadAsync(AgentSession session, CancellationToken token) =>
        Task.Run(() => Read(session, token), token);

    private static SessionStatistics? Read(AgentSession session, CancellationToken token)
    {
        var path = session.TranscriptPath;
        if (path == null && session.Agent == SessionAgent.ClaudeCode)
        {
            var home = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            var projects = Path.Combine(home, "projects");
            if (!Directory.Exists(projects)) return null;
            path = Directory.EnumerateDirectories(projects).Select(dir => Path.Combine(dir, session.Id + ".jsonl")).FirstOrDefault(File.Exists);
        }
        if (path == null) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream);
        TranscriptStatistics stats = session.Agent == SessionAgent.Codex ? new CodexStatistics(session.Id) : new ClaudeStatistics();
        var hasRecords = false;
        while (reader.ReadLine() is { } line)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.Field("type").ValueKind != JsonValueKind.String) continue;
                hasRecords = true;
                stats.Record(doc.RootElement);
            }
            catch (JsonException) { /* A live transcript may end with an incomplete record. */ }
        }
        return hasRecords ? stats.Result() : null;
    }
}

// @implements ViewRecentAgentSessions
using System.Diagnostics;
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class CodexSessionList(Process process, CancellationToken token)
{
    private int requestId;

    internal async Task ReadAsync(bool newProcess, SessionListScope scope, Func<IReadOnlyList<AgentSession>, Task> showPage, CancellationToken caller)
    {
        if (newProcess)
        {
            await RequestAsync("initialize", new { clientInfo = new { name = "xcmd", version = "1" } }).ConfigureAwait(false);
            await SendAsync(new { method = "initialized" }).ConfigureAwait(false);
        }
        var active = await ReadPageAsync(false, null, 20, showPage).ConfigureAwait(false);
        if (caller.IsCancellationRequested) return;
        var archived = await ReadPageAsync(true, null, 20, showPage).ConfigureAwait(false);
        if (scope == SessionListScope.Recent) return;
        while (!caller.IsCancellationRequested && (active != null || archived != null))
        {
            if (active != null) active = await ReadPageAsync(false, active, 100, showPage).ConfigureAwait(false);
            if (archived != null) archived = await ReadPageAsync(true, archived, 100, showPage).ConfigureAwait(false);
        }
    }

    private async Task<string?> ReadPageAsync(bool archived, string? cursor, int limit, Func<IReadOnlyList<AgentSession>, Task> showPage)
    {
        var page = await RequestAsync("thread/list", new { cursor, archived, limit, sortKey = "updated_at", modelProviders = Array.Empty<string>() }).ConfigureAwait(false);
        var rows = page.GetProperty("data").EnumerateArray().Select(row =>
        {
            var name = row.TryGetProperty("name", out var title) ? title.GetString() : null;
            return new AgentSession(SessionAgent.Codex, row.GetProperty("id").GetGuid(), string.IsNullOrWhiteSpace(name) ? row.GetProperty("preview").GetString() ?? "" : name,
                row.GetProperty("cwd").GetString() ?? "", DateTimeOffset.FromUnixTimeSeconds(row.GetProperty("updatedAt").GetInt64()))
            { Activity = Activity(row), TranscriptPath = row.Str("path") is { Length: > 0 } path ? path : null };
        }).ToArray();
        if (rows.Length != 0) await showPage(rows).ConfigureAwait(false);
        var next = page.GetProperty("nextCursor").GetString();
        return string.IsNullOrEmpty(next) ? null : next;
    }

    private static SessionActivity Activity(JsonElement row)
    {
        if (!row.TryGetProperty("status", out var status) || !status.TryGetProperty("type", out var type)) return SessionActivity.Unknown;
        return type.GetString() switch
        {
            "active" when status.TryGetProperty("activeFlags", out var flags) && flags.GetArrayLength() > 0 => SessionActivity.Waiting,
            "active" => SessionActivity.Working,
            "idle" => SessionActivity.Idle,
            "systemError" => SessionActivity.Failed,
            _ => SessionActivity.Unknown
        };
    }

    private async Task<JsonElement> RequestAsync(string method, object parameters)
    {
        var id = ++requestId;
        await SendAsync(new { id, method, @params = parameters }).ConfigureAwait(false);
        while (await process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false) is { } line)
        {
            using var response = JsonDocument.Parse(line);
            var root = response.RootElement;
            if (!root.TryGetProperty("id", out var reply) || !reply.TryGetInt32(out var value) || value != id) continue;
            if (root.TryGetProperty("error", out var error)) throw new IOException(error.GetProperty("message").GetString());
            return root.GetProperty("result").Clone();
        }
        throw new IOException("Codex App Server closed before returning the session list.");
    }

    private async Task SendAsync(object message)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), token).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(token).ConfigureAwait(false);
    }
}

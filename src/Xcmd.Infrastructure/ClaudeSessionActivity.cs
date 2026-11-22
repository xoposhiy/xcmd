// @implements ViewRecentAgentSessions
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class ClaudeSessionActivity
{
    private DateTimeOffset nextRead;
    private readonly Dictionary<Guid, SessionActivity> states = [];

    internal async Task RefreshAsync(CancellationToken token)
    {
        if (DateTimeOffset.UtcNow < nextRead) return;
        var text = await StatusCommand.ReadAsync("claude.exe", ["agents", "--json", "--all"], TimeSpan.FromSeconds(4), token).ConfigureAwait(false);
        states.Clear();
        nextRead = DateTimeOffset.UtcNow.AddSeconds(30);
        if (text == null) return;
        try
        {
            using var json = JsonDocument.Parse(text);
            foreach (var row in json.RootElement.EnumerateArray())
            {
                if (!row.TryGetProperty("sessionId", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var key)) continue;
                states[key] = Parse(row);
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { states.Clear(); }
    }

    internal SessionActivity For(Guid id) => states.GetValueOrDefault(id);

    private static SessionActivity Parse(JsonElement row)
    {
        var status = row.TryGetProperty("status", out var live) ? live.GetString() : null;
        var state = row.TryGetProperty("state", out var background) ? background.GetString() : null;
        return (status ?? state) switch
        {
            "busy" or "working" => SessionActivity.Working,
            "waiting" or "blocked" => SessionActivity.Waiting,
            "idle" => SessionActivity.Idle,
            "done" => SessionActivity.Completed,
            "failed" => SessionActivity.Failed,
            "stopped" => SessionActivity.Stopped,
            _ => SessionActivity.Unknown
        };
    }
}

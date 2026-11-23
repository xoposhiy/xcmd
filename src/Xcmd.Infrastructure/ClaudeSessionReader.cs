// @implements ViewRecentAgentSessions
using System.Diagnostics;
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class ClaudeSessionReader
{
    internal static async Task ReadAsync(Process worker, SessionListScope scope, Func<IReadOnlyList<AgentSession>, Task> showPage, CancellationToken token)
    {
        var command = scope == SessionListScope.Recent ? "recent" : "list";
        await worker.StandardInput.WriteLineAsync(command.AsMemory(), token).ConfigureAwait(false);
        await worker.StandardInput.FlushAsync(token).ConfigureAwait(false);
        while (await worker.StandardOutput.ReadLineAsync(token).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0) return;
            using var page = JsonDocument.Parse(line);
            var rows = page.RootElement.EnumerateArray().Select(row => new AgentSession(SessionAgent.ClaudeCode,
                row.GetProperty("id").GetGuid(), row.GetProperty("topic").GetString() ?? "", row.GetProperty("cwd").GetString() ?? "",
                DateTimeOffset.FromUnixTimeMilliseconds(row.GetProperty("updated").GetInt64()))
            {
                TranscriptBytes = row.Field("fileSize") is { ValueKind: JsonValueKind.Number } size && size.TryGetInt64(out var bytes) && bytes >= 0 ? bytes : null
            }).ToArray();
            if (rows.Length != 0) await showPage(rows).ConfigureAwait(false);
        }
        throw new EndOfStreamException("The Claude session reader closed before finishing.");
    }

    internal static ProcessStartInfo StartInfo()
    {
        var tools = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xcmd");
        var js = Path.Combine(tools, "claude-session-js");
        var start = AgentSessions.ReaderStartInfo();
        if (File.Exists(Path.Combine(js, "node_modules", "@anthropic-ai", "claude-agent-sdk", "package.json"))
            && FeedbackAgentLauncher.FindConsoleTool("node.exe") is { } node)
        {
            start.FileName = node;
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "claude_sessions.mjs"));
            start.ArgumentList.Add(js);
        }
        else
        {
            var python = Path.Combine(tools, "claude-session-sdk", "Scripts", "python.exe");
            start.FileName = File.Exists(python) ? python : FeedbackAgentLauncher.FindConsoleTool("python.exe")
                ?? throw new IOException("Install the Claude Agent SDK for Node.js or Python to list Claude Code sessions.");
            start.ArgumentList.Add("-X"); start.ArgumentList.Add("utf8");
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "claude_sessions.py"));
        }
        return start;
    }
}

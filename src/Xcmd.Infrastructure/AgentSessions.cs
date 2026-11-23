// @implements ViewRecentAgentSessions, ResumeAgentSession, ExternalLaunch, InspectAgentSession
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class AgentSessions : IAgentSessions, IAsyncDisposable
{
    private readonly SessionReaderLease claude;
    private readonly SessionReaderLease codex;
    private readonly ClaudeSessionActivity? activity;
    private readonly SessionTranscriptReader transcripts = SessionTranscriptReader.Local();

    public AgentSessions() : this(TimeSpan.FromMinutes(10), ClaudeSessionReader.StartInfo, CodexStartInfo) => activity = new();

    public AgentSessions(TimeSpan idleTimeout, Func<ProcessStartInfo> startClaude, Func<ProcessStartInfo> startCodex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);
        claude = new(startClaude, idleTimeout);
        codex = new(startCodex, idleTimeout);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(claude.DisposeAsync().AsTask(), codex.DisposeAsync().AsTask()).ConfigureAwait(false);
    }

    public static string[] Drives() => Environment.GetLogicalDrives();

    public Task<SessionStatistics?> ReadStatisticsAsync(AgentSession session, CancellationToken token) =>
        SessionStatisticsReader.ReadAsync(session, token);

    public Task<SessionTrace> InspectAsync(AgentSession session, CancellationToken token, IProgress<string>? progress = null) =>
        transcripts.ReadAsync(session, token, progress);

    public Task<bool> IsClassificationReadyAsync(AgentSession session, CancellationToken token) =>
        transcripts.IsClassificationReadyAsync(session, token);

    public ExternalLaunch Resume(AgentSession session)
    {
        if (!Directory.Exists(session.Directory)) throw new IOException("The session working folder is unavailable: " + session.Directory);
        return new(ExternalLaunchKind.Command, FindCli(session.Agent), session.Directory, session.ResumeArguments);
    }

    private static string FindCli(SessionAgent agent)
    {
        var name = agent == SessionAgent.Codex ? "codex" : "claude";
        return FeedbackAgentLauncher.FindConsoleTool(name + ".exe", name + ".cmd")
            ?? throw new IOException($"Cannot find {name}. Install its CLI and add it to PATH.");
    }

    public async Task ListAsync(SessionAgent agent, SessionListScope scope, Func<IReadOnlyList<AgentSession>, Task> showPage, CancellationToken token)
    {
        try
        {
            var lease = agent == SessionAgent.Codex ? codex : claude;
            var projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Task Publish(IReadOnlyList<AgentSession> rows) => token.IsCancellationRequested ? Task.CompletedTask : showPage(rows.Select(WithProject).ToArray());
            AgentSession WithProject(AgentSession row)
            {
                if (!projects.TryGetValue(row.Directory, out var project))
                {
                    project = Path.IsPathFullyQualified(row.Directory) ? GitStatusReader.WorkTreeRoot(row.Directory, token) : null;
                    projects[row.Directory] = project ??= row.Directory;
                }
                return row with { ProjectDirectory = project, Activity = agent == SessionAgent.ClaudeCode && activity != null ? activity.For(row.Id) : row.Activity };
            }
            await Task.Run(() => lease.UseAsync((process, firstRequest, transport) => agent == SessionAgent.Codex
                ? new CodexSessionList(process, transport).ReadAsync(firstRequest, scope, Publish, token)
                : ReadClaudeAsync(process, transport), token), token).ConfigureAwait(false);

            async Task ReadClaudeAsync(Process process, CancellationToken transport)
            {
                if (activity != null) await activity.RefreshAsync(transport).ConfigureAwait(false);
                await ClaudeSessionReader.ReadAsync(process, scope, Publish, transport).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is Win32Exception or JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        { throw new IOException("Cannot read the session list: " + error.Message, error); }
    }

    private static ProcessStartInfo CodexStartInfo()
    {
        var start = ReaderStartInfo();
        var cli = FindCli(SessionAgent.Codex);
        if (Path.GetExtension(cli).Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            start.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            start.Environment["XCMD_CODEX_CLI"] = cli;
            start.Arguments = "/d /v:off /s /c \"\"%XCMD_CODEX_CLI%\" app-server\"";
        }
        else { start.FileName = cli; start.ArgumentList.Add("app-server"); }
        return start;
    }

    internal static ProcessStartInfo ReaderStartInfo() => new()
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = AppContext.BaseDirectory,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };
}

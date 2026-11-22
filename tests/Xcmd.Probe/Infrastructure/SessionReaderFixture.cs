// @tests ViewRecentAgentSessions
using System.Diagnostics;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

internal sealed class SessionReaderFixture : IAsyncDisposable
{
    internal readonly TimeSpan Idle = TimeSpan.FromSeconds(2);
    internal readonly AgentSessions Readers;
    internal int Started => started;
    private int started;

    private readonly string directory;
    internal SessionReaderFixture(string? directory = null)
    {
        this.directory = directory ?? AppContext.BaseDirectory;
        Readers = new(Idle, () => Start("Claude"), () => Start("Codex"));
    }

    private ProcessStartInfo Start(string name)
    {
        Interlocked.Increment(ref started);
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "launch-probe", "Xcmd.LaunchProbe.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--session-reader");
        start.ArgumentList.Add(name);
        return start;
    }

    internal async Task<SessionWorker> ReadAsync(SessionAgent agent, SessionListScope scope)
    {
        var rows = new List<AgentSession>();
        await Readers.ListAsync(agent, scope, page => { rows.AddRange(page); return Task.CompletedTask; }, CancellationToken.None);
        var id = int.Parse(rows.Single().Topic, System.Globalization.CultureInfo.InvariantCulture);
        return new SessionWorker(Process.GetProcessById(id));
    }

    public ValueTask DisposeAsync() => Readers.DisposeAsync();
}

internal sealed class SessionWorker(Process process) : IDisposable
{
    internal int Id => process.Id;
    internal bool HasExited => process.HasExited;
    internal Task WaitForExitAsync() => process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
    public void Dispose() => process.Dispose();
}

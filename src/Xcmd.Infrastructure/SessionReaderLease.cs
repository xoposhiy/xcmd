// @implements ViewRecentAgentSessions
using System.Diagnostics;

namespace Xcmd.Infrastructure;

internal sealed class SessionReaderLease(Func<ProcessStartInfo> startInfo, TimeSpan idleTimeout) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource shutdown = new();
    private CancellationTokenSource? idle;
    private Process? process;
    private string? diagnostic;

    internal async Task UseAsync(Func<Process, bool, CancellationToken, Task> read, CancellationToken caller)
    {
        await gate.WaitAsync(caller).ConfigureAwait(false);
        try
        {
            shutdown.Token.ThrowIfCancellationRequested();
            caller.ThrowIfCancellationRequested();
            CancelExpiry();
            var first = process == null || process.HasExited;
            if (first)
            {
                process?.Dispose();
                process = null;
                diagnostic = null;
                process = Process.Start(startInfo()) ?? throw new IOException("Cannot start the session reader.");
                process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Volatile.Write(ref diagnostic, e.Data); };
                process.BeginErrorReadLine();
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try { await read(process!, first, timeout.Token).ConfigureAwait(false); }
            catch (Exception error)
            {
                await StopAsync().ConfigureAwait(false);
                if (error is OperationCanceledException && !shutdown.IsCancellationRequested) throw new IOException("The session reader timed out.", error);
                if (error is IOException && Volatile.Read(ref diagnostic) is { } message) throw new IOException(message, error);
                throw;
            }
            idle = new CancellationTokenSource();
            _ = ExpireAsync(idle);
            caller.ThrowIfCancellationRequested();
        }
        finally { gate.Release(); }
    }

    private void CancelExpiry()
    {
        idle?.Cancel();
        idle = null;
    }

    private async Task ExpireAsync(CancellationTokenSource lease)
    {
        using (lease)
        {
            try
            {
                await Task.Delay(idleTimeout, lease.Token).ConfigureAwait(false);
                await gate.WaitAsync(lease.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lease.IsCancellationRequested) { return; }
            try
            {
                if (idle != lease) return;
                idle = null;
                await StopAsync().ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
    }

    private async Task StopAsync()
    {
        using var worker = process;
        process = null;
        if (worker == null) return;
        try { if (!worker.HasExited) worker.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) when (worker.HasExited) { }
        await worker.WaitForExitAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await shutdown.CancelAsync().ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try { CancelExpiry(); await StopAsync().ConfigureAwait(false); }
        finally { gate.Release(); }
    }
}

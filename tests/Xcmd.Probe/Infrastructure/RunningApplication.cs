// @implements IntegrationVerification
using System.Diagnostics;

namespace Xcmd.Probe.Infrastructure;

public sealed class RunningApplication(Process process) : IDisposable
{
    public int ExitCode => process.ExitCode;
    public Task WaitForExitAsync() => process.WaitForExitAsync();
    public async Task StopAsync()
    {
        if (process.HasExited) return;
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
    public void Dispose() => process.Dispose();
}

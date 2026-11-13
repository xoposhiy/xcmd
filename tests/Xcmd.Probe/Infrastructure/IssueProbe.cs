using System.Diagnostics;
using Xcmd.Desktop;

namespace Xcmd.Probe.Infrastructure;

public static class IssueProbe
{
    public static async Task<(int Code, string Output, string Error)[]> RaceAsync(string store)
    {
        using var first = Start(store, "first");
        using var second = Start(store, "second");
        return await Task.WhenAll(CompleteAsync(first), CompleteAsync(second));
    }

    private static Process Start(string store, string id)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { typeof(CommanderWindow).Assembly.Location, "issues", "--store", store, "start", id }) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new IOException("The issue CLI could not start.");
    }

    private static async Task<(int Code, string Output, string Error)> CompleteAsync(Process process)
    {
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output, await error);
    }
}

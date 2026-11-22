// @implements ExternalLaunch, RepositoryStatus
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Xcmd.Infrastructure;

internal static class StatusCommand
{
    public static async Task<string?> ReadAsync(string executable, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        start.Environment["GH_PROMPT_DISABLED"] = "1";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        try
        {
            using var process = Process.Start(start)!;
            process.StandardInput.Close();
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
                await Task.WhenAll(output, process.StandardError.ReadToEndAsync(deadline.Token), process.WaitForExitAsync(deadline.Token)).ConfigureAwait(false);
                return process.ExitCode == 0 ? await output.ConfigureAwait(false) : null;
            }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
        catch (Exception error) when (error is IOException or Win32Exception or InvalidOperationException) { return null; }
    }
}

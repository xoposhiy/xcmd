// @implements RepositoryLocations
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Xcmd.Infrastructure;

internal static class GitHistoryProcess
{
    public static async Task<string> TextAsync(string root, string[] arguments, CancellationToken cancellationToken, bool allowExitOne = false)
    {
        using var buffer = new MemoryStream();
        await RunAsync(root, arguments, buffer, cancellationToken, allowExitOne).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static async Task RunAsync(string root, string[] arguments, Stream output, CancellationToken cancellationToken, bool allowExitOne = false)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.Ordinal)).ToArray()) start.Environment.Remove(key);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in new[] { "--no-pager", "--no-optional-locks", "-c", "core.quotepath=false", "-c", "color.ui=false" }.Concat(arguments)) start.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(start) ?? throw new IOException("Git could not be started.");
            try
            {
                var error = process.StandardError.ReadToEndAsync(timeout.Token);
                var read = process.StandardOutput.BaseStream.CopyToAsync(output, timeout.Token);
                await Task.WhenAll(read, process.WaitForExitAsync(timeout.Token)).ConfigureAwait(false);
                var message = await error.ConfigureAwait(false);
                if (process.ExitCode != 0 && !(allowExitOne && process.ExitCode == 1)) throw new IOException("Git: " + message.Trim());
            }
            finally
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
            }
        }
        catch (Win32Exception error) { throw new IOException("Git could not be started: " + error.Message, error); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new IOException("Git did not finish within 30 seconds. Try again."); }
    }
}

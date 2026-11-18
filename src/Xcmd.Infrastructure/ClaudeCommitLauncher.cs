// @implements DelegateCommit, ExternalLaunch
using System.ComponentModel;
using System.Diagnostics;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class ClaudeCommitLauncher(string? executable = null)
{
    public const string Prompt = "Commit all changes.";

    public Task<ExternalLaunch> PrepareAsync(string directory, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            var repository = await RepositoryRootAsync(directory, cancellationToken).ConfigureAwait(false);
            var claude = executable ?? FeedbackAgentLauncher.FindConsoleTool("claude.exe");
            if (claude == null || !File.Exists(claude))
                throw new IOException("Claude Code was not found. Install Claude Code and sign in, then try Commit again.");
            return new ExternalLaunch(ExternalLaunchKind.Command, claude, repository,
                "--model sonnet --effort medium " + CommandLine.FormatArgument(Prompt));
        }, cancellationToken);

    private static async Task<string> RepositoryRootAsync(string directory, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("rev-parse"); start.ArgumentList.Add("--show-toplevel");
            foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.Ordinal)).ToArray())
                start.Environment.Remove(key);
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
            using var process = Process.Start(start) ?? throw new IOException("Git could not start.");
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var error = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                var root = (await output.ConfigureAwait(false)).Trim();
                var diagnostic = (await error.ConfigureAwait(false)).Trim();
                if (process.ExitCode != 0 || root.Length == 0)
                    throw new IOException("Cannot open the Git repository. " + diagnostic);
                var fullPath = Path.GetFullPath(root);
                return fullPath.Length > 1 && fullPath[1] == ':' ? char.ToUpperInvariant(fullPath[0]) + fullPath[1..] : fullPath;
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (Win32Exception error) { throw new IOException("Git could not start: " + error.Message, error); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new IOException("Git repository lookup timed out. Try Commit again."); }
    }
}

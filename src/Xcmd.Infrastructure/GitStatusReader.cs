// @implements RepositoryStatus
using System.ComponentModel;
using System.Diagnostics;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public interface IGitStatusReader
{
    Task<string?> FindRootAsync(string directory, CancellationToken cancellationToken);
    Task<GitStatus?> ReadAsync(string directory, CancellationToken cancellationToken);
    Task<GitStatus[]> ReadChildRepositoriesAsync(string directory, CancellationToken cancellationToken);
}

public sealed record GitStatus(string Branch, int Staged, int Changed, int Untracked, int Conflicts, int Ahead, int Behind)
{
    public int Deleted { get; init; }
    public bool HasRemote { get; init; }
    public WorkflowTarget? WorkflowTarget { get; init; }
    public string? Upstream { get; init; }
    public string? Root { get; init; }
    public GitPathStatus[] Paths { get; init; } = [];
    public string[] IgnoredPaths { get; init; } = [];
    public string[] TrackedIgnoredPaths { get; init; } = [];
}

public sealed class GitStatusReader : IGitStatusReader
{
    public Task<string?> FindRootAsync(string directory, CancellationToken cancellationToken) =>
        Task.Run(() => WorkTreeRoot(directory, cancellationToken), cancellationToken);

    public Task<GitStatus[]> ReadChildRepositoriesAsync(string directory, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            var repositories = new System.Collections.Concurrent.ConcurrentBag<GitStatus>();
            try
            {
                var children = Directory.EnumerateDirectories(directory, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 })
                    .Where(path => Path.Exists(Path.Combine(path, ".git")));
                await Parallel.ForEachAsync(children, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (path, token) =>
                {
                    if (await ReadBackgroundAsync(path, token).ConfigureAwait(false) is { } status) repositories.Add(status);
                }).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException) { }
            return repositories.ToArray();
        }, cancellationToken);

    public Task<GitStatus?> ReadAsync(string directory, CancellationToken cancellationToken) =>
        Task.Run(() => ReadBackgroundAsync(directory, cancellationToken), cancellationToken);

    private async Task<GitStatus?> ReadBackgroundAsync(string directory, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var root = WorkTreeRoot(directory, timeout.Token);
            if (root == null) return null;
            var output = await RunAsync(root, ["status", "--porcelain=v2", "--branch", "-z", "--untracked-files=normal", "--ignored=matching"], timeout.Token).ConfigureAwait(false);
            if (output == null) return null;
            var remotes = await RunAsync(root, ["remote", "-v"], timeout.Token).ConfigureAwait(false);
            var records = output.Split('\0');
            var status = Parse(records, remotes ?? "") with { HasRemote = !string.IsNullOrWhiteSpace(remotes), Root = root };
            var paths = GitStatusPaths.Parse(records);
            var trackedIgnored = await RunAsync(root, ["ls-files", "--cached", "--ignored", "--exclude-standard", "-z"], timeout.Token).ConfigureAwait(false);
            return status with
            {
                Paths = paths.Changes,
                IgnoredPaths = paths.Ignored,
                TrackedIgnoredPaths = trackedIgnored?.Split('\0', StringSplitOptions.RemoveEmptyEntries) ?? []
            };
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException or OperationCanceledException)
        { return null; }
    }

    // A process's working directory cannot be deleted on Windows. Running Git in the work tree root
    // instead of the displayed directory lets the user delete or rename that directory during a read.
    internal static string? WorkTreeRoot(string directory, CancellationToken cancellationToken)
    {
        for (var current = new DirectoryInfo(directory); current != null; current = current.Parent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (current.Name.Equals(".git", StringComparison.OrdinalIgnoreCase)) return null;
            if (Path.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
        }
        return null;
    }

    private static async Task<string?> RunAsync(string directory, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--no-optional-locks");
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        // The panel location, not an inherited shell environment, selects the repository.
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(key);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var process = Process.Start(start);
        if (process == null) return null;
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await error.ConfigureAwait(false);
            return process.ExitCode == 0 ? await output.ConfigureAwait(false) : null;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
        }
    }

    private static GitStatus Parse(string[] records, string remotes)
    {
        var branch = ""; var oid = "";
        string? upstream = null;
        int staged = 0, changed = 0, untracked = 0, conflicts = 0, ahead = 0, behind = 0, deleted = 0;
        for (var i = 0; i < records.Length; i++)
        {
            var line = records[i];
            if (line.StartsWith("# branch.head ", StringComparison.Ordinal)) branch = line[14..];
            else if (line.StartsWith("# branch.oid ", StringComparison.Ordinal)) oid = line[13..];
            else if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal)) upstream = line[18..];
            else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var parts = line[12..].Split(' ');
                if (parts.Length == 2) { int.TryParse(parts[0].TrimStart('+'), out ahead); int.TryParse(parts[1].TrimStart('-'), out behind); }
            }
            else if (line.StartsWith("? ", StringComparison.Ordinal)) untracked++;
            else if (line.StartsWith("u ", StringComparison.Ordinal)) conflicts++;
            else if (line.Length > 4 && (line[0] == '1' || line[0] == '2') && line[1] == ' ')
            {
                var counts = ChangeCounts(line);
                deleted += counts.Deleted; staged += counts.Staged; changed += counts.Changed;
                if (line[0] == '2') i++; // Rename source is a separate NUL-delimited path.
            }
        }
        var detached = branch == "(detached)" ? oid : null;
        if (detached != null) branch = "detached " + oid[..Math.Min(8, oid.Length)];
        return new(branch, staged, changed, untracked, conflicts, ahead, behind)
        { Deleted = deleted, Upstream = upstream, WorkflowTarget = GitHubWorkflows.Target(remotes, branch, upstream, detached) };
    }

    private static (int Deleted, int Staged, int Changed) ChangeCounts(string line) =>
        line[2] == 'D' || line[3] == 'D' ? (1, 0, 0) : (0, line[2] != '.' ? 1 : 0, line[3] != '.' ? 1 : 0);
}

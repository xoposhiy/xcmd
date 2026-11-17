// @implements RepositoryDocuments
using System.Text;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class GitHistoryDocuments : IDisposable
{
    private readonly string cache = Path.Combine(Path.GetTempPath(), "xcmd-git-" + Guid.NewGuid().ToString("N"));
    private readonly GitHistoryReader reader = new();

    public Task<string> WorkingTreeAsync(string root, IReadOnlyList<GitWorkingChange> changes, CancellationToken token)
    {
        var files = string.Join("\n", changes.Select(change =>
            $"{change.StagedStatus}\t{change.WorkingStatus}\t" + (change.PreviousPath == null ? change.Path : change.PreviousPath + " -> " + change.Path)));
        var text = "Working tree\n" + root + "\n\nUncommitted changes in this history scope\n\nStaged\tWorking tree\tPath\n";
        return ReportAsync("working-tree.txt", text + (changes.Count == 0 ? "No uncommitted changes." : files), token);
    }

    public async Task<string> WorkingFileAsync(string root, string path, CancellationToken token)
    {
        var file = Path.Combine(root, path);
        if (Directory.Exists(file)) throw new IOException("Submodule contents belong to a separate repository.");
        if (File.Exists(file)) return file;
        var scope = await reader.ResolveAsync(file, token).ConfigureAwait(false);
        if (scope.Revision.Length == 0) throw new IOException("This file has no working or committed version to view.");
        return await FileAsync(root, scope.Revision, path, token).ConfigureAwait(false);
    }

    public async Task<string> CommitAsync(string root, GitHistoryCommit commit, IReadOnlyList<GitCommitChange> changes, CancellationToken token, string scope = "")
    {
        var message = await GitHistoryProcess.TextAsync(root, ["show", "--no-patch", "--format=fuller", commit.Id], token).ConfigureAwait(false);
        var files = string.Join("\n", changes.Select(change => change.Description + "\t" +
            (change.PreviousPath == null ? change.Path : change.PreviousPath + " -> " + change.Path)));
        return await ReportAsync(commit.ShortId + ".txt", message + "\nChanged files (first parent; empty tree for the first commit)\nScope: /" + scope + "\n\n" + files, token).ConfigureAwait(false);
    }

    public async Task<string> DiffAsync(string root, string path, string? commit, GitCommitChange? change, CancellationToken token)
    {
        string[] args;
        if (commit == null)
        {
            var scope = await reader.ResolveAsync(Path.Combine(root, path), token).ConfigureAwait(false);
            var tracked = await GitHistoryProcess.TextAsync(root, ["ls-files", "--cached", "-z", "--", ":(literal)" + path], token).ConfigureAwait(false);
            var committed = scope.Revision.Length == 0 ? "" : await GitHistoryProcess.TextAsync(root,
                ["ls-tree", "--name-only", "-z", "HEAD", "--", ":(literal)" + path], token).ConfigureAwait(false);
            args = tracked.Length == 0 && committed.Length == 0 || scope.Revision.Length == 0
                ? ["diff", "--no-index", "--no-ext-diff", "--no-textconv", "--", "/dev/null", Path.Combine(root, path)]
                : ["diff", "--no-ext-diff", "--no-textconv", "--no-color", "HEAD", "--", ":(literal)" + path];
        }
        else
        {
            var parent = await reader.ParentAsync(root, commit, token).ConfigureAwait(false);
            var paths = new[] { path, change?.PreviousPath }.OfType<string>().Distinct(StringComparer.Ordinal).Select(value => ":(literal)" + value);
            args = parent == null
                ? new[] { "diff-tree", "--root", "--no-commit-id", "-r", "-p", "--no-ext-diff", "--no-textconv", commit, "--" }.Concat(paths).ToArray()
                : new[] { "diff", "--no-ext-diff", "--no-textconv", "-M", parent, commit, "--" }.Concat(paths).ToArray();
        }
        var diff = await GitHistoryProcess.TextAsync(root, args, token, allowExitOne: args.Contains("--no-index")).ConfigureAwait(false);
        var heading = "Git diff · " + path + "\n" + (commit == null ? "HEAD -> working tree (including staged changes)" : "First parent -> " + commit) + "\n\n";
        return await ReportAsync("diff.txt", heading + (diff.Length == 0 ? "No differences from the committed version." : diff), token).ConfigureAwait(false);
    }

    public async Task<string> FileAsync(string root, string commit, string path, CancellationToken token)
    {
        var destination = NewPath(Path.GetFileName(path));
        await using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await GitHistoryProcess.RunAsync(root, ["show", commit + ":" + path], file, token).ConfigureAwait(false);
        return destination;
    }

    private async Task<string> ReportAsync(string name, string text, CancellationToken token)
    {
        var destination = NewPath(name);
        await File.WriteAllTextAsync(destination, text, new UTF8Encoding(false), token).ConfigureAwait(false);
        return destination;
    }

    private string NewPath(string name)
    {
        var directory = Path.Combine(cache, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(cache)) Directory.Delete(cache, recursive: true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}

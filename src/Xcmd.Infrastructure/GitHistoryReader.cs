// @implements RepositoryLocations
using System.Globalization;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class GitHistoryReader
{
    public const int PageSize = 200;

    public Task<string> MessageAsync(string root, string revision, CancellationToken token) =>
        GitHistoryProcess.TextAsync(root, ["show", "--no-patch", "--format=format:%B", revision], token);

    public async Task<GitWorkingChange[]> WorkingChangesAsync(string root, CancellationToken token)
    {
        var output = await GitHistoryProcess.TextAsync(root, ["status", "--porcelain=v2", "-z", "--untracked-files=all"], token).ConfigureAwait(false);
        return GitStatusPaths.Parse(output.Split('\0')).Changes.Select(change =>
            new GitWorkingChange(change.Path, StatusText(change.Staged), StatusText(change.Unstaged), change.OriginalPath))
            .OrderBy(change => change.Path, StringComparer.Ordinal).ToArray();
    }

    private static string StatusText(GitChange change) => change switch
    {
        GitChange.Modified => "Modified",
        GitChange.Added => "Added",
        GitChange.Deleted => "Deleted",
        GitChange.Renamed => "Renamed",
        GitChange.Copied => "Copied",
        GitChange.TypeChanged => "Type changed",
        GitChange.Untracked => "Untracked",
        GitChange.Conflict => "Conflict",
        _ => ""
    };

    public Task<GitHistoryScope> ResolveAsync(string path, CancellationToken token) => Task.Run(async () =>
    {
        var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
        while (!Directory.Exists(directory))
            directory = Path.GetDirectoryName(directory) ?? throw new IOException("The repository folder is unavailable.");
        var root = (await GitHistoryProcess.TextAsync(directory, ["rev-parse", "--show-toplevel"], token).ConfigureAwait(false)).TrimEnd('\r', '\n');
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (relative == ".") relative = "";
        var revision = (await GitHistoryProcess.TextAsync(root, ["rev-parse", "--verify", "--quiet", "--end-of-options", "HEAD"], token, allowExitOne: true).ConfigureAwait(false)).Trim();
        return new GitHistoryScope(root, relative, revision);
    }, token);

    public async Task<GitHistoryCommit[]> LogAsync(GitHistoryScope scope, int skip, CancellationToken token)
    {
        if (scope.Revision.Length == 0) return [];
        var output = await GitHistoryProcess.TextAsync(scope.Root,
            ["log", "--format=%H%x00%aI%x00%an%x00%s%x00", "--max-count=" + PageSize, "--skip=" + skip,
                scope.Revision, "--", ":(literal)" + (scope.Path.Length == 0 ? "." : scope.Path)], token).ConfigureAwait(false);
        var fields = output.Split('\0');
        var commits = new List<GitHistoryCommit>();
        for (var i = 0; i + 3 < fields.Length; i += 4)
            commits.Add(new(fields[i].TrimStart('\r', '\n'), fields[i + 2], DateTimeOffset.Parse(fields[i + 1], CultureInfo.InvariantCulture), fields[i + 3]));
        return commits.ToArray();
    }

    public async Task<GitCommitChange[]> ChangesAsync(string root, string commit, CancellationToken token)
    {
        var parent = await ParentAsync(root, commit, token).ConfigureAwait(false);
        var args = parent == null
            ? new[] { "diff-tree", "--root", "--no-commit-id", "--name-status", "-r", "-z", "-M", commit }
            : new[] { "diff", "--name-status", "-z", "-M", parent, commit, "--" };
        var fields = (await GitHistoryProcess.TextAsync(root, args, token).ConfigureAwait(false)).Split('\0');
        var changes = new List<GitCommitChange>();
        for (var i = 0; i + 1 < fields.Length;)
        {
            var status = fields[i++]; var path = fields[i++];
            if (status[0] is 'R' or 'C' && i < fields.Length) changes.Add(new(fields[i++], status, path));
            else changes.Add(new(path, status));
        }
        return changes.ToArray();
    }

    public async Task<GitTreeItem[]> TreeAsync(string root, string commit, string directory, CancellationToken token)
    {
        var output = await GitHistoryProcess.TextAsync(root, ["ls-tree", "-z", "-l", commit + ":" + directory], token).ConfigureAwait(false);
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(record =>
        {
            var tab = record.IndexOf('\t');
            var metadata = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var name = record[(tab + 1)..];
            long.TryParse(metadata[3], CultureInfo.InvariantCulture, out var size);
            return new GitTreeItem(directory.Length == 0 ? name : directory + "/" + name, name, metadata[1] == "tree", size, metadata[1]);
        }).OrderByDescending(item => item.IsDirectory).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<string?> ParentAsync(string root, string commit, CancellationToken token)
    {
        var line = await GitHistoryProcess.TextAsync(root, ["rev-list", "--parents", "-n", "1", commit], token).ConfigureAwait(false);
        return line.Trim().Split(' ').Skip(1).FirstOrDefault();
    }
}

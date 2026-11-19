// @implements SeeRepositoryState
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Panels.Git;

internal sealed class GitStatusProjection
{
    private readonly string? root;
    private readonly HashSet<string> ignored = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> trackedIgnored = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> untrackedTrees = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GitRowStatus> changes = new(StringComparer.OrdinalIgnoreCase);

    public GitStatusProjection(GitStatus? status)
    {
        if (status?.Root == null) return;
        root = Normalize(status.Root);
        ignored.Add(Absolute(".git"));
        foreach (var path in status.IgnoredPaths) ignored.Add(Absolute(path));
        foreach (var relative in status.TrackedIgnoredPaths)
            for (var path = Absolute(relative); UnderRoot(path); path = Path.GetDirectoryName(path)!)
            {
                trackedIgnored.Add(path);
                if (path.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
            }
        foreach (var entry in status.Paths)
        {
            if (entry.Unstaged.HasFlag(GitChange.Untracked) && entry.Path.EndsWith('/')) untrackedTrees.Add(Absolute(entry.Path));
            Add(entry.Path, entry.Staged, entry.Unstaged);
            if (entry.OriginalPath != null && (entry.Staged | entry.Unstaged).HasFlag(GitChange.Renamed)) Add(entry.OriginalPath, entry.Staged, entry.Unstaged);
        }
    }

    public bool Contains(string path)
    {
        var fullPath = Normalize(path);
        for (var current = fullPath; UnderRoot(current); current = Path.GetDirectoryName(current)!)
        {
            if (ignored.Contains(current)) return changes.ContainsKey(fullPath) || trackedIgnored.Contains(fullPath);
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public GitRowStatus For(string path)
    {
        var fullPath = Normalize(path);
        var status = (changes.GetValueOrDefault(fullPath) ?? GitRowStatus.Empty) with { TrackedIgnored = trackedIgnored.Contains(fullPath) };
        if (IsIgnored(fullPath)) return status with { Ignored = true };
        for (var current = fullPath; UnderRoot(current); current = Path.GetDirectoryName(current)!)
        {
            if (untrackedTrees.Contains(current)) return status with { Unstaged = status.Unstaged | GitChange.Untracked };
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
        return status;
    }
    private bool IsIgnored(string path)
    {
        for (var current = path; UnderRoot(current); current = Path.GetDirectoryName(current)!)
        {
            if (ignored.Contains(current)) return true;
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
        return false;
    }
    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private string Absolute(string path) => Normalize(Path.Combine(root!, path.Replace('/', Path.DirectorySeparatorChar)));
    private bool UnderRoot(string? path) => root != null && path != null && (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    private void Add(string relativePath, GitChange staged, GitChange unstaged)
    {
        for (var path = Absolute(relativePath); UnderRoot(path); path = Path.GetDirectoryName(path)!)
        {
            var prior = changes.GetValueOrDefault(path) ?? GitRowStatus.Empty;
            changes[path] = new(prior.Staged | staged, prior.Unstaged | unstaged);
            if (path.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
    }
}

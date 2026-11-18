// @implements SeeRepositoryState
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal sealed class GitPanelRows
{
    public const string LiveLegend = "Left: staged · Right: working tree\n✓ staged · ● modified or mixed changes · + added · ? untracked · → renamed/copied · red − deleted · ! conflict\nGray − ignored · ! on the right: tracked despite an ignore rule\nClean files are blank. A green ✓ on the right marks a clean repository. Folder statuses include descendants.";
    public string Legend(string directory) => GitPath.HasRevision(directory)
        ? "Changes in this commit compared with its first parent.\n● modified · + added · → renamed/copied · − deleted\nBlank cells: unchanged. Folder indicators include descendants."
        : LiveLegend;
    private GitStatus? projectedStatus;
    private GitStatusProjection projection = new(null);

    public bool Apply(IReadOnlyList<FileRow> rows, GitStatusView git, string directory, bool searchResults)
    {
        if (GitPath.IsEntry(directory))
        {
            var commit = GitPath.HasRevision(directory);
            foreach (var row in rows) row.SetGitStatus(row.Entry.GitStatus is { } status
                ? GitRowStatus.FromEntry(status, commit) : GitRowStatus.Empty);
            return true;
        }
        if (!ReferenceEquals(projectedStatus, git.Status))
        {
            projectedStatus = git.Status;
            projection = new(projectedStatus);
        }
        var showColumn = !searchResults && !ArchivePath.IsEntry(directory) && directory.Length > 0;
        var repositories = git.ChildRepositories.Where(status => status.Root != null)
            .ToDictionary(status => status.Root!, status => new GitStatusProjection(status).For(status.Root!) with { Repository = status }, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var status = GitRowStatus.Empty;
            if (showColumn && !row.Entry.IsParent)
                status = projectedStatus == null || projection.Contains(row.Entry.FullPath)
                    ? repositories.GetValueOrDefault(row.Entry.FullPath) ?? projection.For(row.Entry.FullPath)
                    : projection.For(row.Entry.FullPath);
            row.SetGitStatus(status);
        }
        return showColumn;
    }
}

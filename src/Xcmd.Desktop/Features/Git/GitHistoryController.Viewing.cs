// @implements BrowseSnapshot, InspectCommitChanges, InspectWorkingChanges, ReadDiff
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.Git;

public sealed partial class GitHistoryController
{
    public void ViewCurrent()
    {
        var panel = activePanel();
        if (!panel.HasHistory || panel.Current is not { } row) return;
        var directory = panel.Session.DirectoryPath;
        actions.Start("Opening Git view", token => ViewAsync(panel, directory, row.Entry, token), opensViewer: true);
    }

    private async Task ViewAsync(PanelView panel, string directory, FileEntry entry, CancellationToken token)
    {
        var current = GitPath.Parse(directory);
        if (entry.IsParent && current.Kind == "tree") { await OpenChangesAsync(panel, directory, token); return; }
        var target = GitPath.Parse(entry.IsParent ? directory : entry.FullPath);
        string report, caption;
        if (target.Kind == "working")
        {
            if (entry.IsParent || entry.IsDirectory)
            {
                if (!await panel.LoadAsync(directory, preserveSelection: true, cancellationToken: token)) return;
                var scope = files.Scope(directory).Label;
                report = await documents.WorkingTreeAsync(scope, files.CapturedWorkingChanges(directory), token);
                caption = "Working tree · " + scope + " · read-only";
            }
            else
            {
                report = await documents.WorkingFileAsync(target.Repository, target.RelativePath, token);
                caption = target.RelativePath + " · Working tree view · read-only";
            }
        }
        else if (target.Revision.Length > 0 && (entry.IsParent || current.Kind == "log"))
        {
            var path = entry.IsParent ? directory : entry.FullPath;
            var changes = await files.ChangesAsync(path, token);
            var scope = entry.IsParent ? current.RelativePath : "";
            changes = changes.Where(change => GitHistoryRules.InScope(scope, change.Path, change.PreviousPath)).ToArray();
            report = await documents.CommitAsync(target.Repository, files.Commit(path), changes, token, scope);
            caption = "Commit details · " + ShortId(target.Revision) + " · Scope: /" + scope + " · read-only";
        }
        else if (!entry.IsDirectory && target.Revision.Length > 0)
        {
            if (entry.IsLink) { ui.SetError("Submodule contents belong to a separate repository."); return; }
            var revision = target.Revision;
            var deleted = (await files.ChangesAsync(directory, token)).Any(change => change.Path == target.RelativePath && change.Status[0] == 'D');
            if (deleted) revision = await reader.ParentAsync(target.Repository, revision, token) ?? throw new IOException("This deleted file has no parent version.");
            report = await documents.FileAsync(target.Repository, revision, target.RelativePath, token);
            caption = target.RelativePath + " · " + ShortId(target.Revision) + (deleted ? " · deleted, parent version" : "") + " · read-only";
        }
        else { ui.SetError("Select a commit, a file, or the parent row inside a commit."); return; }
        token.ThrowIfCancellationRequested();
        viewFile(new(report, caption, IsTemporary: documents.Owns(report)));
    }

    public void DiffCurrent()
    {
        var panel = activePanel();
        if (panel.Current is not { Entry.IsDirectory: false } row) { ui.SetError("Select a changed repository file to compare."); return; }
        var directory = panel.Session.DirectoryPath; var entry = row.Entry;
        actions.Start("Preparing Git diff", token => DiffAsync(directory, entry, token), opensViewer: true);
    }

    private async Task DiffAsync(string directory, FileEntry entry, CancellationToken token)
    {
        string root, path; string? revision = null; GitCommitChange? change = null;
        if (GitPath.IsEntry(directory))
        {
            var location = GitPath.Parse(entry.FullPath);
            root = location.Repository; path = location.RelativePath;
            if (location.Revision.Length > 0)
            {
                revision = location.Revision;
                change = (await files.ChangesAsync(directory, token)).FirstOrDefault(item => item.Path == path);
                if (change == null) { ui.SetError("This file did not change in the selected commit."); return; }
            }
        }
        else
        {
            if (ArchivePath.IsEntry(entry.FullPath)) { ui.SetError("Select a changed repository file to compare."); return; }
            var scope = await reader.ResolveAsync(entry.FullPath, token);
            root = scope.Root; path = scope.Path;
        }
        var report = await documents.DiffAsync(root, path, revision, change, token);
        token.ThrowIfCancellationRequested();
        viewFile(new(report, "Diff · " + path + " · " + (revision == null ? "HEAD → working tree" : "first parent → " + ShortId(revision)) + " · read-only", IsTemporary: true));
    }

    private static string ShortId(string revision) => revision[..Math.Min(10, revision.Length)];
}

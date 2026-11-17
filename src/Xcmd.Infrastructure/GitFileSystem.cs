// @implements RepositoryLocations
using System.Collections.Concurrent;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class GitFileSystem(IFileSystem fileSystem, GitHistoryReader reader) : IFileSystem
{
    private sealed class Context(GitHistoryScope scope)
    {
        public GitHistoryScope Scope { get; set; } = scope;
        public GitHistoryCommit[] Commits { get; set; } = [];
        public bool HasMore { get; set; }
        public ConcurrentDictionary<string, GitCommitChange[]> Changes { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, string> Messages { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, GitHistoryCommit> Catalog { get; } = new(StringComparer.Ordinal);
        public GitWorkingChange[] Working { get; set; } = [];
        public DateTimeOffset RefreshedAt { get; set; }
    }

    private readonly ConcurrentDictionary<string, Context> contexts = new(StringComparer.Ordinal);

    public async Task<string> OpenLogAsync(string path, CancellationToken token)
    {
        var scope = await reader.ResolveAsync(path, token).ConfigureAwait(false);
        var commits = await reader.LogAsync(scope, 0, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var id = Guid.NewGuid().ToString("N");
        var context = new Context(scope) { Commits = commits, HasMore = commits.Length == GitHistoryReader.PageSize };
        foreach (var commit in commits) context.Catalog[commit.Id] = commit;
        contexts[id] = context;
        return GitPath.Log(scope.Root, id);
    }

    public GitHistoryScope Scope(string path) => Get(path).Scope;
    public GitWorkingChange[] CapturedWorkingChanges(string path) => Get(path).Working;
    public DateTimeOffset RefreshedAt(string path) => Get(path).RefreshedAt;
    public string? EmptyLogMessage(string path)
    {
        var context = Get(path);
        return context.Commits.Length > 0 ? null : context.Scope.Revision.Length == 0
            ? "This repository has no commits yet." : "No commits changed this path.";
    }

    public async Task<string> CommitMessageAsync(string path, CancellationToken token)
    {
        var location = GitPath.Parse(path); var context = Get(path);
        if (context.Messages.TryGetValue(location.Revision, out var message)) return message;
        message = await reader.MessageAsync(location.Repository, location.Revision, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return context.Messages.GetOrAdd(location.Revision, message);
    }
    public string Display(string path)
    {
        if (!GitPath.IsEntry(path)) return path;
        var location = GitPath.Parse(path);
        return location.Revision.Length > 0 ? GitPath.Display(path) : Scope(path).Label + " · " + (location.Kind == "log" ? "Git log" : "Working tree");
    }
    public GitHistoryCommit Commit(string path)
    {
        var location = GitPath.Parse(path);
        return Get(path).Catalog.TryGetValue(location.Revision, out var commit) ? commit : throw new IOException("The commit metadata is unavailable. Open the log again.");
    }

    private Context Get(string path) => contexts.TryGetValue(GitPath.Parse(path).Context, out var context)
        ? context : throw new IOException("This Git history is no longer available. Open the log again.");

    public async Task LoadMoreAsync(string path, CancellationToken token)
    {
        var context = Get(path);
        var page = await reader.LogAsync(context.Scope, context.Commits.Length, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        foreach (var commit in page) context.Catalog[commit.Id] = commit;
        context.Commits = context.Commits.Concat(page).ToArray(); context.HasMore = page.Length == GitHistoryReader.PageSize;
    }

    public async Task<GitWorkingChange[]> WorkingChangesAsync(string path, CancellationToken token)
    {
        var scope = Get(path).Scope;
        var changes = await reader.WorkingChangesAsync(scope.Root, token).ConfigureAwait(false);
        var filtered = changes.Where(change => GitHistoryRules.InScope(scope.Path, change.Path, change.PreviousPath)).ToArray();
        token.ThrowIfCancellationRequested();
        var context = Get(path);
        context.Working = filtered; context.RefreshedAt = DateTimeOffset.Now;
        return filtered;
    }

    public async Task<GitCommitChange[]> ChangesAsync(string path, CancellationToken token)
    {
        var location = GitPath.Parse(path); var context = Get(path);
        if (context.Changes.TryGetValue(location.Revision, out var changes)) return changes;
        changes = await reader.ChangesAsync(location.Repository, location.Revision, token).ConfigureAwait(false);
        return context.Changes.GetOrAdd(location.Revision, changes);
    }

    public async Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (!GitPath.IsEntry(path)) return await fileSystem.ReadDirectoryAsync(path, cancellationToken).ConfigureAwait(false);
        var location = GitPath.Parse(path);
        if (location.Kind == "log") return await LogEntriesAsync(path, cancellationToken).ConfigureAwait(false);
        if (location.Kind == "working") return await WorkingEntriesAsync(path, cancellationToken).ConfigureAwait(false);
        if (location.Kind == "changes") return await ChangedEntriesAsync(path, cancellationToken).ConfigureAwait(false);
        if (location.Kind != "tree") throw new IOException("This Git location cannot be opened as a folder.");
        var changes = await ChangesAsync(path, cancellationToken).ConfigureAwait(false);
        var tree = await reader.TreeAsync(location.Repository, location.Revision, location.RelativePath, cancellationToken).ConfigureAwait(false);
        return tree.Select(item => new FileEntry(GitPath.Tree(location.Repository, location.Context, location.Revision, item.Path),
            item.Name, item.IsDirectory, item.Size, DateTime.MinValue, IsLink: item.ObjectType == "commit")
        { GitStatus = new(' ', CommitStatus(item.Path, item.IsDirectory, changes)) }).ToArray();
    }

    private async Task<FileEntry[]> LogEntriesAsync(string path, CancellationToken token)
    {
        var context = Get(path); var location = GitPath.Parse(path);
        await RefreshHeadAsync(context, token).ConfigureAwait(false);
        var entries = context.Commits.Select(commit => new FileEntry(GitPath.Tree(location.Repository, location.Context, commit.Id),
            commit.ShortId + " " + commit.Subject, true, 0, commit.Date.UtcDateTime)).ToList();
        var working = await WorkingChangesAsync(path, token).ConfigureAwait(false);
        if (working.Length > 0) entries.Insert(0, new(GitPath.Working(location.Repository, location.Context), "Working tree", true, 0, DateTime.MinValue)
        { GitStatus = new(' ', 'M') });
        if (context.HasMore) entries.Add(new(GitPath.More(location.Repository, location.Context), "Load more commits", true, 0, DateTime.MinValue));
        return entries.ToArray();
    }

    private async Task RefreshHeadAsync(Context context, CancellationToken token)
    {
        var scope = await reader.ResolveAsync(context.Scope.Label, token).ConfigureAwait(false);
        if (scope.Revision == context.Scope.Revision) return;
        var commits = await reader.LogAsync(scope, 0, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        foreach (var commit in commits) context.Catalog[commit.Id] = commit;
        context.Scope = scope; context.Commits = commits; context.HasMore = commits.Length == GitHistoryReader.PageSize;
    }

    private async Task<FileEntry[]> WorkingEntriesAsync(string path, CancellationToken token)
    {
        var changes = await WorkingChangesAsync(path, token).ConfigureAwait(false);
        return await Task.Run(() => changes.Select(change => WorkingEntry(path, change)).ToArray(), token).ConfigureAwait(false);
    }

    private async Task<FileEntry[]> ChangedEntriesAsync(string path, CancellationToken token)
    {
        var location = GitPath.Parse(path);
        var changes = await ChangesAsync(path, token).ConfigureAwait(false);
        return changes.Where(change => GitHistoryRules.InScope(location.RelativePath, change.Path, change.PreviousPath))
            .Select(change => new FileEntry(GitPath.ChangedFile(location.Repository, location.Context, location.Revision, change.Path),
                change.Path, false, 0, DateTime.MinValue)
            { GitStatus = new(' ', change.Status[0]) }).ToArray();
    }

    private static FileEntry WorkingEntry(string path, GitWorkingChange change)
    {
        long size = 0; var modified = DateTime.MinValue;
        try
        {
            var file = new FileInfo(Path.Combine(GitPath.Parse(path).Repository, change.Path));
            if (file.Exists) { size = file.Length; modified = file.LastWriteTimeUtc; }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return new FileEntry(Path.Combine(path, change.Path.Replace('/', '\\')), change.Path, false, size, modified)
        { GitStatus = new(StatusCode(change.StagedStatus), StatusCode(change.WorkingStatus)) };
    }

    private static char CommitStatus(string path, bool directory, GitCommitChange[] changes)
    {
        var change = changes.FirstOrDefault(change => change.Path.Equals(path, StringComparison.Ordinal));
        if (change != null) return change.Status[0];
        return directory && changes.Any(change => GitHistoryRules.InScope(path, change.Path, change.PreviousPath)) ? 'M' : ' ';
    }

    private static char StatusCode(string status) => status switch
    {
        "Modified" => 'M',
        "Added" => 'A',
        "Deleted" => 'D',
        "Renamed" => 'R',
        "Copied" => 'C',
        "Type changed" => 'T',
        "Untracked" => '?',
        "Conflict" => 'U',
        _ => ' '
    };

    public Task RenameAsync(FileEntry entry, string newName)
    { ArchivePath.RequireWritable(entry.FullPath); return fileSystem.RenameAsync(entry, newName); }
    public Task CreateDirectoryAsync(string parent, string name)
    { ArchivePath.RequireWritable(parent); return fileSystem.CreateDirectoryAsync(parent, name); }
}

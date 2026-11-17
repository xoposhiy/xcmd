// @implements NavigatePanels, SortEntries, RecentLocations, RestoreWorkspace
namespace Xcmd.Core;

public sealed class PanelSession(IFileSystem fileSystem)
{
    private CancellationTokenSource? loading;
    private readonly Dictionary<string, string> cursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly DirectoryHistory history = new();
    public string DirectoryPath { get; private set; } = "";
    public IReadOnlyList<FileEntry> Entries { get; private set; } = [];
    public SortOrder SortOrder { get; private set; } = new();
    public string? Error { get; private set; }
    public bool IsLoading { get; private set; }
    public long Revision { get; private set; }
    public IFileSystem FileSystem => fileSystem;
    public IReadOnlyList<string> PreviousDirectories => history.PreviousDirectories;
    public Dictionary<string, string> CaptureCursors() => new(cursors, StringComparer.OrdinalIgnoreCase);

    public void RestoreNavigation(PanelState state)
    {
        SortOrder = state.Sort;
        if (!GitPath.IsLog(DirectoryPath)) Entries = PanelRules.Sort(Entries, SortOrder);
        cursors.Clear();
        foreach (var cursor in state.Cursors) cursors[cursor.Key] = cursor.Value;
        history.Restore(state.History, DirectoryPath);
        Revision++;
    }

    public void CancelLoad() => loading?.Cancel();

    public void RememberCursor(string? name)
    {
        if (name != null && DirectoryPath.Length > 0) cursors[DirectoryPath] = name;
    }

    public string? RememberedCursor => cursors.GetValueOrDefault(DirectoryPath);

    public async Task<bool> LoadAsync(string path, CancellationToken cancellationToken = default, bool nearestExistingParent = false)
    {
        loading?.Cancel();
        var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        loading = request;
        IsLoading = true;
        Error = null;
        try
        {
            var (fullPath, entries) = await ReadDirectoryAsync(path, nearestExistingParent, request.Token);
            if (request.IsCancellationRequested) return false;
            DirectoryPath = fullPath;
            var parent = PanelRules.ParentEntry(DirectoryPath);
            var navigationEntries = parent == null ? entries : entries.Prepend(parent);
            Entries = GitPath.IsLog(fullPath) ? navigationEntries.ToArray() : PanelRules.Sort(navigationEntries, SortOrder);
            history.Visit(DirectoryPath);
            Revision++;
            return true;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { return false; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (!request.IsCancellationRequested) Error = error.Message;
            return false;
        }
        finally
        {
            if (ReferenceEquals(loading, request)) IsLoading = false;
            request.Dispose();
            if (ReferenceEquals(loading, request)) loading = null;
        }
    }

    private async Task<(string Path, IReadOnlyList<FileEntry> Entries)> ReadDirectoryAsync(string path, bool nearestExistingParent, CancellationToken token)
    {
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return (current, await fileSystem.ReadDirectoryAsync(current, token)); }
            catch (DirectoryNotFoundException) when (nearestExistingParent && ArchivePath.Parent(current) is { })
            { current = ArchivePath.Parent(current)!; }
        }
    }

    public void SortBy(SortField field)
    {
        if (GitPath.IsLog(DirectoryPath)) return;
        SortOrder = new(field, SortOrder.Field == field && !SortOrder.Descending);
        if (!GitPath.IsLog(DirectoryPath)) Entries = PanelRules.Sort(Entries, SortOrder);
        Revision++;
    }
}

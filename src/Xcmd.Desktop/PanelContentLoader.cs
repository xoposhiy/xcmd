// @implements NavigatePanels, RefreshEntries, BrowseHistory
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class PanelContentLoader(PanelSession session, PanelFileTable table, PanelRename rename, GitStatusView git,
    PanelReturnLocations returnLocations, Func<PanelReturnPoint> captureReturnPoint, Func<bool> isSearchOpen)
{
    private int? navigation;
    private int revision;

    public event Action? LoadingRequested;
    public event Action<string, string[], string?>? DirectoryLoaded;
    public event Action? StateChanged;

    public void InvalidateNavigation()
    {
        revision++;
        navigation = null;
    }

    public void PrepareSearch()
    {
        LoadingRequested?.Invoke();
        session.CancelLoad();
        rename.Cancel();
    }

    // Navigation owns publication until the directory header is ready.
    public int? BeginRefresh() => navigation == null ? ++revision : null;
    public bool CanRefresh(int request) => request == revision && navigation == null;

    public async Task<bool> LoadAsync(string directory, bool preserveSelection = false, string? selectName = null,
        CancellationToken cancellationToken = default, bool nearestExistingParent = false, PanelReturnPoint? historyReturn = null)
    {
        var request = ++revision;
        navigation = request;
        try
        {
            historyReturn ??= GitPath.IsEntry(directory) && !GitPath.IsEntry(session.DirectoryPath) ? captureReturnPoint() : null;
            PrepareDirectoryLoad(preserveSelection);
            if (request != revision) return false;
            var selected = preserveSelection ? table.Selection.SelectedNames() : [];
            var previousPath = session.DirectoryPath;
            if (!await session.LoadAsync(directory, cancellationToken, nearestExistingParent))
            {
                if (request == revision) StateChanged?.Invoke();
                return false;
            }
            if (!CanPublish(request, cancellationToken)) return false;
            git.SetDirectory(session.DirectoryPath);
            await git.DirectoryReady;
            if (!CanPublish(request, cancellationToken)) return false;
            selectName ??= preserveSelection ? table.Selection.Current?.Name : session.RememberedCursor;
            returnLocations.ApplyDirectory(session.DirectoryPath, historyReturn);
            DirectoryLoaded?.Invoke(previousPath, selected, selectName);
            return true;
        }
        finally
        {
            if (navigation == request) navigation = null;
        }
    }

    private void PrepareDirectoryLoad(bool preserveSelection)
    {
        LoadingRequested?.Invoke();
        rename.Cancel();
        if (!preserveSelection) table.Selection.SetKeyboardMarks(false);
        if (!isSearchOpen()) session.RememberCursor(table.Selection.Current?.Name);
    }

    public void ShowRows(string[] selected, string? current) =>
        table.ReplaceRows(session, CreateRows(), showMeasurements: true, selected, current);

    private List<FileRow> CreateRows() => session.Entries.Select(entry => new FileRow(entry)).ToList();

    private bool CanPublish(int request, CancellationToken token) => request == revision && !token.IsCancellationRequested;

    public async Task<bool> LoadAccessibleAsync(string directory)
    {
        while (true)
        {
            var request = revision + 1;
            if (await LoadAsync(directory)) return true;
            var parent = ArchivePath.Parent(directory);
            if (request != revision || parent == null || session.Error == null) return false;
            directory = parent;
        }
    }

    public async Task<bool> RefreshDirectoryAsync(Func<bool> canApply, Func<bool> canFocus)
    {
        if (BeginRefresh() is not { } request) return false;
        var directory = session.DirectoryPath;
        var hadFocus = table.Table.IsKeyboardFocusWithin;
        if (!await session.LoadAsync(directory))
            return await RecoverDirectoryAsync(request, directory, hadFocus, canApply, canFocus);
        if (!CanRefresh(request) || !canApply() || rename.EditingRow != null || session.DirectoryPath != directory) return false;
        if (table.Rows.Select(row => row.Entry).SequenceEqual(session.Entries)) StateChanged?.Invoke();
        else table.RefreshRows(session, CreateRows(), showMeasurements: true);
        return true;
    }

    private async Task<bool> RecoverDirectoryAsync(int request, string directory, bool hadFocus, Func<bool> canApply, Func<bool> canFocus)
    {
        if (!CanRefresh(request) || session.Error == null || session.DirectoryPath != directory || !canApply()) return false;
        var loaded = await LoadAccessibleAsync(directory);
        if (loaded && hadFocus && canApply() && canFocus()) table.Selection.FocusCurrent();
        return loaded;
    }
}

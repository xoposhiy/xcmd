// @implements NavigatePanels, RefreshEntries, BrowseHistory, AsyncCoordination
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class PanelContentLoader(PanelView panel)
{
    private readonly PanelSession session = panel.Session;
    private int? navigation;
    private int revision;

    public void InvalidateNavigation()
    {
        revision++;
        navigation = null;
    }

    // Navigation owns publication until the directory header is ready.
    public int? BeginRefresh() => navigation == null ? ++revision : null;
    public bool CanRefresh(int request) => request == revision && navigation == null;

    public async Task<bool> LoadAsync(string directory, bool preserveSelection = false, string? selectName = null,
        CancellationToken cancellationToken = default, bool nearestExistingParent = false)
    {
        var request = ++revision;
        navigation = request;
        try
        {
            var gitReturn = GitPath.IsEntry(directory) && !panel.HasHistory ? panel.CaptureReturnPoint() : null;
            panel.PrepareDirectoryLoad(preserveSelection);
            if (request != revision) return false;
            var selected = preserveSelection ? panel.SelectedNames() : [];
            var previousPath = session.DirectoryPath;
            if (!await session.LoadAsync(directory, cancellationToken, nearestExistingParent))
            {
                if (request == revision) panel.UpdateSummary();
                return false;
            }
            if (!CanPublish(request, cancellationToken)) return false;
            panel.Git.SetDirectory(session.DirectoryPath);
            await panel.Git.DirectoryReady;
            if (!CanPublish(request, cancellationToken)) return false;
            selectName ??= preserveSelection ? panel.Current?.Name : session.RememberedCursor;
            panel.ApplyDirectoryLoad(previousPath, gitReturn, selected, selectName);
            return true;
        }
        finally
        {
            if (navigation == request) navigation = null;
        }
    }

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
        var hadFocus = panel.Table.IsKeyboardFocusWithin;
        if (!await session.LoadAsync(directory))
            return await RecoverDirectoryAsync(request, directory, hadFocus, canApply, canFocus);
        if (!CanRefresh(request) || !canApply() || panel.EditingRow != null || session.DirectoryPath != directory) return false;
        panel.ApplyDirectoryRefresh();
        return true;
    }

    private async Task<bool> RecoverDirectoryAsync(int request, string directory, bool hadFocus, Func<bool> canApply, Func<bool> canFocus)
    {
        if (!CanRefresh(request) || session.Error == null || session.DirectoryPath != directory || !canApply()) return false;
        var loaded = await LoadAccessibleAsync(directory);
        if (loaded && hadFocus && canApply() && canFocus()) panel.FocusCurrent();
        return loaded;
    }
}

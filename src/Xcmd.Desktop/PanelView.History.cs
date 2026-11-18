// @implements BrowseHistory
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private (PanelState State, double Width)? gitReturnState;
    public bool HasHistory => GitPath.IsEntry(Session.DirectoryPath);

    public async Task<bool> OpenHistoryAsync(string location, CancellationToken cancellationToken = default)
    {
        var saved = gitReturnState ?? (CaptureState(), Table.Bounds.Width);
        if (!await LoadAsync(location, cancellationToken: cancellationToken)) return false;
        gitReturnState = saved;
        if (Rows.FirstOrDefault(row => !row.Entry.IsParent) is { } first) SelectRow(first);
        return true;
    }

    public async Task ExitHistoryAsync()
    {
        if (gitReturnState is not { } saved)
        {
            if (HasHistory) await LoadAccessibleAsync(GitPath.Parse(Session.DirectoryPath).Repository);
            FocusCurrent(); return;
        }
        gitReturnState = null;
        await RestoreTemporaryViewAsync(saved);
    }

    public async Task BackHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (GitPath.IsLog(Session.DirectoryPath)) { await ExitHistoryAsync(); return; }
        var child = Session.DirectoryPath;
        if (!await LoadAsync(GitPath.Parent(child), cancellationToken: cancellationToken)) return;
        if (Rows.FirstOrDefault(row => row.Entry.FullPath == child) is { } exited) SelectRow(exited);
        FocusCurrent();
    }
}

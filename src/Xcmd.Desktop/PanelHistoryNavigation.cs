// @implements BrowseHistory
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class PanelHistoryNavigation(PanelSession session, PanelContentLoader loader, PanelFileTable table,
    PanelReturnLocations returnLocations, PanelStateRestore state)
{
    public bool IsOpen => GitPath.IsEntry(session.DirectoryPath);

    public async Task<bool> OpenAsync(string location, CancellationToken cancellationToken)
    {
        var historyReturn = returnLocations.History ?? state.CaptureReturnPoint();
        if (!await loader.LoadAsync(location, cancellationToken: cancellationToken, historyReturn: historyReturn)) return false;
        if (table.Rows.FirstOrDefault(row => !row.Entry.IsParent) is { } first) table.Selection.SelectRow(first);
        return true;
    }

    public async Task ExitAsync()
    {
        if (returnLocations.History is not { } saved)
        {
            if (IsOpen && !await loader.LoadAccessibleAsync(GitPath.Parse(session.DirectoryPath).Repository)) return;
            table.Selection.FocusCurrent();
            return;
        }
        returnLocations.ClearHistory();
        await state.RestoreReturnPointAsync(saved);
    }

    public async Task BackAsync(CancellationToken cancellationToken)
    {
        if (GitPath.IsLog(session.DirectoryPath))
        {
            await ExitAsync();
            return;
        }
        var child = session.DirectoryPath;
        if (!await loader.LoadAsync(GitPath.Parent(child), cancellationToken: cancellationToken)) return;
        if (table.Rows.FirstOrDefault(row => row.Entry.FullPath == child) is { } exited) table.Selection.SelectRow(exited);
        table.Selection.FocusCurrent();
    }
}

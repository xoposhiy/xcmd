// @implements SortEntries, RefreshEntries, RenameEntry, SearchResultPanel, RestoreWorkspace
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class PanelSearchView(PanelSession session, PanelContentLoader loader, PanelReturnLocations returnLocations,
    PanelFileTable table, PanelLocationHeader header, Func<PanelReturnPoint> captureReturnPoint)
{
    private PanelSearchResults? results;
    public bool IsOpen => results != null;
    public string OperationDirectory => IsOpen && table.Selection.Current is { } row
        ? ArchivePath.Parent(row.Entry.FullPath) ?? session.DirectoryPath : session.DirectoryPath;

    public void Show(string root, IReadOnlyList<FileSearchResult> items, string? selectedPath = null)
    {
        loader.InvalidateNavigation();
        returnLocations.ClearHistory();
        loader.PrepareSearch();
        if (returnLocations.Search == null) returnLocations.SaveSearch(captureReturnPoint());
        results = new PanelSearchResults(root, items);
        table.Selection.SetKeyboardMarks(false);
        ShowRows([], results.LabelFor(selectedPath));
        header.ShowSearch(root);
        table.SetAccessibleName("Search results in " + ArchivePath.Display(root));
    }

    public void Close() => results = null;

    public Task ExitAsync(Func<PanelReturnPoint, Task> restore) => returnLocations.Search is { } saved ? restore(saved) : Task.CompletedTask;

    public PanelState CaptureState()
    {
        var offset = table.ScrollOffset;
        var search = results!;
        var selection = table.Selection;
        var saved = new SearchPanelState(search.Root, search.Capture(), selection.Current?.Name, selection.CaptureRowSelection(),
            selection.HasKeyboardMarks, session.SortOrder, offset.X, offset.Y);
        return returnLocations.Search!.State with { Columns = table.Columns.Capture(), SearchResults = saved };
    }

    public void Restore(SearchPanelState saved)
    {
        Show(saved.Root, saved.Results);
        session.RestoreNavigation(returnLocations.Search!.State with { Sort = saved.Sort });
        table.Selection.SetKeyboardMarks(saved.HasMarks);
        ShowRows(saved.Selected, saved.Cursor);
        table.Selection.RestoreEmptySelection(saved.Selected);
        table.UpdateSort(session.SortOrder);
    }

    public void ShowRows(string[] selected, string? current) =>
        table.ReplaceRows(session, CreateRows(), showMeasurements: false, selected, current);

    private List<FileRow> CreateRows() => results!.Sort(session.SortOrder).Select(row => new FileRow(row.Entry, row.Label)).ToList();

    public void Rename(FileEntry entry, string name)
    {
        if (results == null) return;
        var (oldLabel, newLabel) = results.Rename(entry, name);
        var selection = table.Selection;
        var selected = selection.CaptureRowSelection().Select(label => label == oldLabel ? newLabel : label).ToArray();
        var cursor = selection.Current?.Name == oldLabel ? newLabel : selection.Current?.Name;
        ShowRows(selected, cursor);
    }

    public async Task<bool> RefreshAsync(Func<bool> canApply)
    {
        var search = results!;
        if (loader.BeginRefresh() is not { } request) return false;
        bool StillCurrent() => results == search && loader.CanRefresh(request);
        if (!await search.RefreshAsync(session.FileSystem, () => StillCurrent() && canApply()) || !StillCurrent()) return false;
        table.RefreshRows(session, CreateRows(), showMeasurements: false);
        return true;
    }
}

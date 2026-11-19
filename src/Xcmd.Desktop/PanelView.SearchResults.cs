// @implements SortEntries, RefreshEntries, RenameEntry, SearchResultPanel, RestoreWorkspace
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private PanelSearchResults? searchResults;
    public bool IsSearchResults => searchResults != null;
    public string OperationDirectory => IsSearchResults && Current is { } row ? ArchivePath.Parent(row.Entry.FullPath) ?? Session.DirectoryPath : Session.DirectoryPath;

    public void ShowSearchResults(string root, IReadOnlyList<FileSearchResult> results, string? selectedPath = null)
    {
        loader.InvalidateNavigation();
        returnLocations.ClearHistory();
        LoadingRequested?.Invoke(this);
        Session.CancelLoad();
        CancelRename();
        if (returnLocations.Search == null) returnLocations.SaveSearch(CaptureReturnPoint());
        searchResults = new PanelSearchResults(root, results);
        selection.SetKeyboardMarks(false);
        ApplyEntries([], searchResults.LabelFor(selectedPath));
        path.Text = "Search results · " + PathText.Display(root);
        Git.SetDirectory("");
        ToolTip.SetTip(path, path.Text + "\nEscape returns to the previous folder. Ctrl+Left/Right locates the current item.");
        AutomationProperties.SetName(Table, "Search results in " + ArchivePath.Display(root));
    }

    public async Task ExitSearchResultsAsync()
    {
        if (returnLocations.Search is not { } saved) return;
        await RestoreTemporaryViewAsync(saved);
    }

    private PanelState CaptureSearchState()
    {
        var offset = Scroll?.Offset ?? default;
        var search = searchResults!;
        var results = new SearchPanelState(search.Root, search.Capture(), Current?.Name, CaptureRowSelection(),
            selection.HasKeyboardMarks, Session.SortOrder, offset.X, offset.Y);
        return returnLocations.Search!.State with
        {
            Columns = columns.Capture(),
            SearchResults = results
        };
    }

    private void RestoreSearchState(SearchPanelState saved)
    {
        ShowSearchResults(saved.Root, saved.Results);
        Session.RestoreNavigation(returnLocations.Search!.State with { Sort = saved.Sort });
        selection.SetKeyboardMarks(saved.HasMarks);
        ApplyEntries(saved.Selected, saved.Cursor);
        selection.RestoreEmptySelection(saved.Selected);
        UpdateHeaders();
    }

    private List<FileRow> ResultRows()
    {
        if (searchResults == null) return Session.Entries.Select(entry => new FileRow(entry)).ToList();
        return searchResults.Sort(Session.SortOrder).Select(row => new FileRow(row.Entry, row.Label)).ToList();
    }

    private void RenameSearchResult(FileEntry entry, string name)
    {
        if (searchResults == null) return;
        var (oldLabel, newLabel) = searchResults.Rename(entry, name);
        var selected = CaptureRowSelection().Select(label => label == oldLabel ? newLabel : label).ToArray();
        var cursor = Current?.Name == oldLabel ? newLabel : Current?.Name;
        ApplyEntries(selected, cursor);
    }

    private async Task<bool> RefreshSearchResultsAsync(Func<bool> canApply)
    {
        var search = searchResults!;
        if (loader.BeginRefresh() is not { } request) return false;
        bool StillCurrent() => searchResults == search && loader.CanRefresh(request);
        if (!await search.RefreshAsync(Session.FileSystem, () => StillCurrent() && canApply()) || !StillCurrent()) return false;
        ApplyRefreshedEntries();
        return true;
    }
}

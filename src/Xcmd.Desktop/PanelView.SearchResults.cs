// @implements SortEntries, RefreshEntries, RenameEntry, SearchResultPanel, RestoreWorkspace
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private FileSearchResult[]? searchResults;
    private PanelState? searchReturnState;
    private string searchRoot = "";
    public bool IsSearchResults => searchResults != null;
    public string OperationDirectory => IsSearchResults && Current is { } row ? ArchivePath.Parent(row.Entry.FullPath) ?? Session.DirectoryPath : Session.DirectoryPath;

    public void ShowSearchResults(string root, IReadOnlyList<FileSearchResult> results, string? selectedPath = null)
    {
        loadVersion++;
        gitReturnState = null;
        LoadingRequested?.Invoke(this);
        Session.CancelLoad();
        CancelRename();
        searchReturnState ??= CaptureState();
        searchRoot = root;
        searchResults = results.DistinctBy(result => result.Entry.FullPath, StringComparer.OrdinalIgnoreCase).ToArray();
        keyboardMarks = false;
        var selected = searchResults.FirstOrDefault(result => result.Entry.FullPath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
        ApplyEntries([], selected?.RelativePath);
        path.Text = "Search results · " + PathText.Display(root);
        Git.SetDirectory("");
        ToolTip.SetTip(path, path.Text + "\nEscape returns to the previous folder. Ctrl+Left/Right locates the current item.");
        AutomationProperties.SetName(Table, "Search results in " + ArchivePath.Display(root));
    }

    public async Task ExitSearchResultsAsync()
    {
        if (searchReturnState is not { } saved) return;
        await RestoreStateAsync(saved);
        RestoreScroll(saved);
        FocusCurrent(scrollIntoView: false);
    }

    private PanelState CaptureSearchState()
    {
        var offset = Scroll?.Offset ?? default;
        var results = new SearchPanelState(searchRoot, searchResults!, Current?.Name, CaptureRowSelection(),
            keyboardMarks, Session.SortOrder, offset.X, offset.Y);
        return searchReturnState! with
        {
            Columns = Table.Columns.Select(column => new ColumnState(column.Width.Value, column.Width.IsStar)).ToArray(),
            SearchResults = results
        };
    }

    private void RestoreSearchState(SearchPanelState saved)
    {
        ShowSearchResults(saved.Root, saved.Results);
        Session.RestoreNavigation(searchReturnState! with { Sort = saved.Sort });
        keyboardMarks = saved.HasMarks;
        ApplyEntries(saved.Selected, saved.Cursor);
        if (saved.Selected.Length == 0) Table.SelectedItems?.Clear();
        UpdateHeaders();
    }

    private List<FileRow> ResultRows()
    {
        if (searchResults == null)
            return Session.Entries.Select(entry =>
            {
                var row = new FileRow(entry); ApplyFileColor(row);
                row.SetMeasurement(sizes.GetValueOrDefault(entry.Name)); return row;
            }).ToList();
        var labels = searchResults.ToDictionary(result => result.Entry.FullPath, result => result.RelativePath, StringComparer.OrdinalIgnoreCase);
        return PanelRules.Sort(searchResults.Select(result => result.Entry), Session.SortOrder)
            .Select(entry => { var row = new FileRow(entry, labels[entry.FullPath]); ApplyFileColor(row); return row; }).ToList();
    }

    private void RenameSearchResult(FileEntry entry, string name)
    {
        if (searchResults == null) return;
        var oldLabel = searchResults.First(result => result.Entry.FullPath.Equals(entry.FullPath, StringComparison.OrdinalIgnoreCase)).RelativePath;
        var newLabel = Path.Combine(Path.GetDirectoryName(oldLabel) ?? "", name);
        var selected = CaptureRowSelection().Select(label => label == oldLabel ? newLabel : label).ToArray();
        var cursor = Current?.Name == oldLabel ? newLabel : Current?.Name;
        searchResults = searchResults.Select(result => result.Entry.FullPath.Equals(entry.FullPath, StringComparison.OrdinalIgnoreCase)
            ? result with
            {
                Entry = entry with { Name = name, FullPath = Path.Combine(ArchivePath.Parent(entry.FullPath)!, name) },
                RelativePath = newLabel
            } : result).ToArray();
        ApplyEntries(selected, cursor);
    }

    private async Task<bool> RefreshSearchResultsAsync(Func<bool> canApply)
    {
        var snapshot = searchResults!;
        var updated = new List<FileSearchResult>();
        foreach (var group in snapshot.GroupBy(result => ArchivePath.Parent(result.Entry.FullPath), StringComparer.OrdinalIgnoreCase))
        {
            if (group.Key == null) { updated.AddRange(group); continue; }
            try
            {
                var entries = await Session.FileSystem.ReadDirectoryAsync(group.Key, CancellationToken.None);
                var byPath = entries.ToDictionary(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase);
                foreach (var result in group)
                    if (byPath.TryGetValue(result.Entry.FullPath, out var entry)) updated.Add(result with { Entry = entry });
            }
            catch (DirectoryNotFoundException) { }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { updated.AddRange(group); }
        }
        if (searchResults != snapshot || !canApply()) return false;
        searchResults = updated.ToArray();
        ApplyRefreshedEntries();
        return true;
    }
}

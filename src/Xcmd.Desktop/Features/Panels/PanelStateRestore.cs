// @implements RestoreWorkspace, PanelReturnState
using Avalonia;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Panels;

internal sealed class PanelStateRestore(PanelSession session, PanelFileTable table, PanelRows rows, PanelContentLoader loader,
    PanelSearchView search, PanelReturnLocations returnLocations)
{
    public event Action? StateChanged;

    public PanelState CaptureState()
    {
        if (GitPath.IsEntry(session.DirectoryPath) && returnLocations.History is { } saved) return saved.State;
        if (search.IsOpen) return search.CaptureState();
        var selection = table.Selection;
        session.RememberCursor(selection.Current?.Name);
        var offset = table.ScrollOffset;
        return new(session.DirectoryPath, session.SortOrder, selection.Current?.Name, selection.CaptureRowSelection(),
            session.PreviousDirectories.ToArray(), session.CaptureCursors(),
            table.Columns.Capture(), offset.X, offset.Y, rows.CaptureMeasurements(), selection.HasMarks);
    }

    public PanelReturnPoint CaptureReturnPoint() => new(CaptureState(), table.Table.Bounds.Width);

    public async Task<bool> RestoreStateAsync(PanelState state)
    {
        if (!await loader.LoadAccessibleAsync(state.DirectoryPath)) return false;
        session.RestoreNavigation(state);
        if (session.DirectoryPath.Equals(state.DirectoryPath, StringComparison.OrdinalIgnoreCase)) rows.RestoreMeasurements(state.Sizes);
        table.Selection.RestoreMarkMode(state);
        loader.ShowRows(state.Selected, state.Cursor);
        table.Selection.RestoreEmptySelection(state.Selected);
        table.Columns.Restore(state.Columns);
        table.UpdateSort(session.SortOrder);
        StateChanged?.Invoke();
        if (state.SearchResults is { } results) search.Restore(results);
        return true;
    }

    public void RestoreScroll(PanelState state) => table.SetScroll(state.SearchResults is { } results
        ? new Vector(results.ScrollX, results.ScrollY) : new Vector(state.ScrollX, state.ScrollY));

    public async Task RestoreReturnPointAsync(PanelReturnPoint saved)
    {
        if (!await RestoreStateAsync(saved.State)) return;
        table.Columns.FitNameAfterResize(saved.Width);
        RestoreScroll(saved.State);
        table.Selection.FocusCurrent(scrollIntoView: false);
    }
}

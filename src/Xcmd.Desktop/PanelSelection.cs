// @implements NavigatePanels, MarkEntries, SortEntries, RefreshEntries, RestoreWorkspace, DragFiles
using Avalonia.Controls;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class PanelSelection(TableView table, Func<List<FileRow>> rows, Action updateView)
{
    private string? cursorName;
    private string? rangeAnchor;
    private FileRow? highlightedRow;
    private HashSet<FileRow> highlightedMarks = [];
    private bool active;
    private List<FileRow> Rows => rows();
    public bool HasKeyboardMarks { get; private set; }
    public FileRow? Current => Rows.FirstOrDefault(row => row.Name == cursorName) ?? table.SelectedItem as FileRow;

    public string[] SelectedNames() => table.SelectedItems?.OfType<FileRow>().Where(row => !row.Entry.IsParent).Select(row => row.Name).ToArray() ?? [];
    public string[] CaptureRowSelection() => table.SelectedItems?.OfType<FileRow>().Select(row => row.Name).ToArray() ?? [];
    public void SetCursor(FileRow row) => cursorName = row.Name;
    public void ResetRange() => rangeAnchor = null;
    public void SetKeyboardMarks(bool value) => HasKeyboardMarks = value;
    public bool HasMarks => HasKeyboardMarks || SelectedNames().Length > 1;

    public void SetActive(bool value)
    {
        active = value;
        highlightedRow?.SetCurrent(true, active);
    }

    public FileEntry? UpdateHighlights()
    {
        var selected = table.SelectedItems?.OfType<FileRow>().ToHashSet() ?? [];
        var current = Current;
        var previous = highlightedRow?.Entry;
        if (highlightedRow != current) highlightedRow?.SetCurrent(false, active);
        highlightedRow = current;
        highlightedRow?.SetCurrent(true, active);
        var marked = HasKeyboardMarks || selected.Count > 1 ? selected.Where(row => !row.Entry.IsParent).ToHashSet() : [];
        foreach (var row in highlightedMarks.Except(marked)) row.SetMarked(false);
        foreach (var row in marked.Except(highlightedMarks)) row.SetMarked(true);
        highlightedMarks = marked;
        return previous;
    }

    public bool CurrentEntryChanged(FileEntry? previous)
    {
        var current = highlightedRow?.Entry;
        return previous?.IsDirectory != current?.IsDirectory || !string.Equals(previous?.FullPath, current?.FullPath, StringComparison.Ordinal);
    }

    public void BeginPointerSelection(bool extend)
    {
        ResetRange();
        SetKeyboardMarks(extend);
    }

    public void RestoreMarkMode(PanelState state) => SetKeyboardMarks(state.HasMarks || state.Selected.Length > 1 ||
        state.Selected.Length > 0 && !state.Selected.Contains(state.Cursor, StringComparer.OrdinalIgnoreCase));

    public void ApplyRows(string[] selected, string? current)
    {
        table.SelectedItems?.Clear();
        foreach (var row in Rows.Where(row => selected.Contains(row.Name, StringComparer.OrdinalIgnoreCase)))
            table.SelectedItems?.Add(row);
        var cursor = Rows.FirstOrDefault(row => row.Name.Equals(current, StringComparison.OrdinalIgnoreCase));
        if (!HasKeyboardMarks && table.SelectedItems?.Count == 0) table.SelectedItem = cursor ?? Rows.FirstOrDefault();
        cursorName = (cursor ?? table.SelectedItem as FileRow ?? Rows.FirstOrDefault())?.Name;
    }

    public void RestoreRefreshCursor(string? current, int index, bool hadSelection)
    {
        if (!Rows.Any(row => row.Name == current) && Rows.Count > 0)
        {
            var next = Rows[Math.Clamp(index, 0, Rows.Count - 1)];
            SetCursor(next);
            if (!HasKeyboardMarks && table.SelectedItems?.Count == 0 && hadSelection) table.SelectedItem = next;
        }
        if (!hadSelection) table.SelectedItems?.Clear();
    }

    public void RestoreEmptySelection(string[] selected)
    {
        if (selected.Length == 0) table.SelectedItems?.Clear();
    }

    public void FocusCurrent(bool scrollIntoView = true)
    {
        updateView();
        table.UpdateLayout();
        var index = Current is { } current ? Rows.IndexOf(current) : -1;
        if (index >= 0)
        {
            if (scrollIntoView) table.ScrollIntoView(index);
            table.UpdateLayout();
            if (table.ContainerFromIndex(index) is Control container) { container.Focus(); return; }
        }
        table.Focus();
    }

    public void SelectBoundary(bool last, bool extend)
    {
        if (Rows.Count == 0) return;
        MoveTo(last ? Rows.Count - 1 : 0, extend);
    }

    public void SelectRow(FileRow row)
    {
        var index = Rows.IndexOf(row);
        SelectIndex(index);
        SetCursor(row);
        FocusCurrent();
    }

    public void MovePage(bool down, bool extend)
    {
        if (Rows.Count == 0) return;
        var rowHeight = table.GetRealizedContainers().FirstOrDefault()?.Bounds.Height ?? 36;
        var page = Math.Max(1, (int)(table.Bounds.Height / Math.Max(1, rowHeight)) - 2);
        var current = Current is { } row ? Rows.IndexOf(row) : 0;
        MoveTo(Math.Clamp(current + (down ? page : -page), 0, Rows.Count - 1), extend);
    }

    private void MoveTo(int index, bool extend)
    {
        if (extend) ToggleRange(index);
        else SelectIndex(index);
        SetCursor(Rows[index]);
        FocusCurrent();
    }

    private void SelectIndex(int index)
    {
        ResetRange();
        if (!HasKeyboardMarks && SelectedNames().Length <= 1) table.SelectedIndex = index;
    }

    private void ToggleRange(int index)
    {
        rangeAnchor ??= Current?.Name;
        var anchor = Math.Max(0, Rows.FindIndex(row => row.Name == rangeAnchor));
        var range = Rows.Skip(Math.Min(anchor, index)).Take(Math.Abs(index - anchor) + 1)
            .Where(row => !row.Entry.IsParent).Select(row => row.Name).ToArray();
        var selected = HasMarks ? CaptureRowSelection() : [];
        var marked = PanelRangeMarks.Toggle(range, selected);
        SetKeyboardMarks(true);
        ReplaceSelection(Rows.Where(row => !row.Entry.IsParent && marked.Contains(row.Name)));
        updateView();
    }

    public void MoveCursor(bool down, bool mark)
    {
        ResetRange();
        if (Current is not { } current || Rows.Count == 0) return;
        var index = Rows.IndexOf(current);
        var next = Math.Clamp(index + (down ? 1 : -1), 0, Rows.Count - 1);
        var hadMarks = HasMarks;
        if (mark || hadMarks) SetKeyboardMarks(true);
        if (HasKeyboardMarks)
        {
            var selected = hadMarks ? CaptureRowSelection().Where(name => name != "..").ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (mark && !current.Entry.IsParent && !selected.Add(current.Name)) selected.Remove(current.Name);
            SetCursor(Rows[next]);
            FocusCurrent();
            ReplaceSelection(Rows.Where(row => selected.Contains(row.Name)));
            updateView();
        }
        else { table.SelectedIndex = next; SetCursor(Rows[next]); FocusCurrent(); }
    }

    public void ClearMarks()
    {
        BeginPointerSelection(extend: false);
        table.SelectedItems?.Clear();
        updateView();
        FocusCurrent(scrollIntoView: false);
    }

    public void FocusPointerRow(FileRow row)
    {
        SetCursor(row);
        FocusCurrent(scrollIntoView: false);
    }

    public void SelectClickedRow(FileRow row, bool toggle)
    {
        BeginPointerSelection(toggle);
        if (toggle) table.SelectedItems?.Remove(row);
        else { table.SelectedItems?.Clear(); table.SelectedItem = row; }
        FocusPointerRow(row);
    }

    private void ReplaceSelection(IEnumerable<FileRow> selected)
    {
        table.SelectedItems?.Clear();
        foreach (var row in selected) table.SelectedItems?.Add(row);
    }
}

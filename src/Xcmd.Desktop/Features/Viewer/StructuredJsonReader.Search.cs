// @implements ChooseViewMode, FindViewerText
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed partial class StructuredJsonReader
{
    private (int Start, int End)? structuredMatch;

    public async Task FindAsync(string query, int direction, CancellationToken token)
    {
        if (structured is not { } source) return;
        var cursor = CaptureStructuredCursor();
        var from = direction == 0 ? 0 : direction < 0 ? structuredMatch?.Start ?? cursor.Caret : structuredMatch?.End ?? cursor.Caret;
        var (start, wrapped) = await Task.Run(() => FindStructured(source.Text, query, from, direction < 0, token), token);
        token.ThrowIfCancellationRequested();
        if (structured != source) return;
        if (start < 0)
        {
            ClearSearch();
            searchStatus.Text = "No matches in this file";
            return;
        }

        var end = start + query.Length;
        var id = source.Tree.ElementAt(start);
        CancelStructuredCommand();
        structuredMatch = (start, end);
        structuredFolds = structuredFolds.RevealSearch(SearchContainers(source, start).Concat(SearchContainers(source, end - 1)));
        PlaceStructured(new(start, start, end, id), start, StructuredScreenY(cursor.Caret));
        ScrollStructuredMatchIntoView();
        searchStatus.Text = $"Match at byte {source.Bytes.At(start):N0}{(wrapped ? " · Wrapped" : "")}";
    }

    private static IEnumerable<int> SearchContainers(StructuredJsonSource source, int position)
    {
        var id = source.Tree.ElementAt(position);
        var element = source.Tree[id];
        if (element.CanFold && position > element.ValueStart && position < element.End - 1) yield return id;
        foreach (var ancestor in source.Tree.Ancestors(id)) yield return ancestor;
    }

    private void ScrollStructuredMatchIntoView()
    {
        if (presenter == null || textScroll == null) return;
        var bounds = presenter.TextLayout.HitTestTextPosition(textContent.CaretIndex);
        if (bounds.X < textScroll.Offset.X || bounds.Right > textScroll.Offset.X + textScroll.Viewport.Width)
            textScroll.Offset = new(Math.Max(0, bounds.X - 20), textScroll.Offset.Y);
    }

    public void ClearSearch()
    {
        var previous = structuredMatch;
        structuredMatch = null;
        if (structured is not { } source || previous == null) return;
        CancelStructuredCommand();
        var cursor = CaptureStructuredCursor();
        var hadFolds = !structuredFolds.SearchExpanded.IsEmpty;
        var matchSelected = cursor.Start == previous.Value.Start && cursor.End == previous.Value.End;
        structuredFolds = structuredFolds.ClearSearch();
        if (!hadFolds && !matchSelected) return;
        var row = new StructuredJsonRows(source, structuredFolds).At(cursor.Caret);
        cursor = matchSelected ? StructuredCursor.At(cursor.Caret, row.Element) : cursor with { Element = row.Element };
        PlaceStructured(cursor, cursor.Caret, StructuredScreenY(cursor.Caret));
    }

    private static (int Start, bool Wrapped) FindStructured(string text, string query, int from, bool backward, CancellationToken token)
    {
        var position = ScanStructured(text, query, backward ? 0 : from, backward ? from : text.Length, backward, token);
        if (position >= 0 || from == (backward ? text.Length : 0)) return (position, false);
        return (ScanStructured(text, query, 0, text.Length, backward, token), true);
    }

    private static int ScanStructured(string text, string query, int start, int end, bool backward, CancellationToken token)
    {
        const int window = 32 * 1024;
        while (end - start >= query.Length)
        {
            token.ThrowIfCancellationRequested();
            var length = Math.Min(end - start, window + query.Length - 1);
            var offset = backward ? end - length : start;
            var portion = text.AsSpan(offset, length);
            var index = backward ? portion.LastIndexOf(query, StringComparison.OrdinalIgnoreCase)
                : portion.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return offset + index;
            if (backward) end -= window;
            else start += window;
        }
        return -1;
    }
}

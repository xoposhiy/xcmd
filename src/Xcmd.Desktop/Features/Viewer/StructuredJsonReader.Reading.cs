// @implements ChooseViewMode, ReadLargeFiles, FindViewerText
using Avalonia;
using Avalonia.Input;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed partial class StructuredJsonReader
{
    private LocalCursor ReadLocalCursor() => new(textContent.CaretIndex, textContent.SelectionStart, textContent.SelectionEnd);

    private StructuredCursor CaptureStructuredCursor()
    {
        if (structuredPage is not { } page || structured is not { } source) return structuredCursor;
        var local = ReadLocalCursor();
        if (local == structuredProjection) return structuredCursor;
        var caret = page.ToSource(local.Caret);
        var id = page.FoldAt(local.Caret) ?? source.Tree.ElementAt(caret);
        var row = page.LineAt(local.Caret).Row;
        if (source.Tree.Ancestors(row.Element).Contains(id)) id = row.Element;
        var previous = structuredProjection;
        var saved = structuredCursor;
        structuredProjection = local;
        if (local == new LocalCursor(local.Caret, local.Caret, local.Caret))
            return structuredCursor = StructuredCursor.At(caret, id);

        var start = ProjectSourceBoundary(page, local.Start, previous.Start, saved.Start, local.Start > local.End);
        var end = ProjectSourceBoundary(page, local.End, previous.End, saved.End, local.End > local.Start);
        return structuredCursor = new(caret, start, end, id);
    }

    private static int ProjectSourceBoundary(StructuredJsonPage page, int position, int previous, int saved, bool trailing) =>
        position == previous && (saved < page.Start || saved > page.End) ? saved : page.ToSource(position, trailing);

    private void PlaceStructured(StructuredCursor cursor, int anchor, double y)
    {
        if (structured is not { } source) return;
        placingStructured = true;
        try
        {
            var screenRows = Math.Max(16, (int)Math.Ceiling(textContent.Bounds.Height / textContent.FontSize));
            var page = new StructuredJsonLayout(source, structuredFolds).Render(anchor, screenRows);
            structuredPage = page;
            textContent.Text = page.Syntax.Text;
            textContent.SetSyntax(page.Syntax);
            SetStructuredCursor(cursor);
            textContent.UpdateLayout();
            ScrollToStructuredAnchor(page.ToDisplay(anchor), y);
        }
        finally { placingStructured = false; }
        UpdateStructuredCue();
        ReadingPositionChanged?.Invoke();
    }

    private void SetStructuredCursor(StructuredCursor cursor)
    {
        if (structuredPage is not { } page) return;
        textContent.CaretIndex = page.ToDisplay(cursor.Caret);
        textContent.SelectionStart = page.ToDisplay(cursor.Start, trailing: cursor.Start > cursor.End);
        textContent.SelectionEnd = page.ToDisplay(cursor.End, trailing: cursor.End > cursor.Start);
        structuredCursor = cursor;
        structuredProjection = ReadLocalCursor();
    }

    private void ScrollToStructuredAnchor(int position, double y)
    {
        if (textScroll == null || presenter == null) return;
        var bounds = presenter.TextLayout.HitTestTextPosition(position);
        var maximum = Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height);
        textScroll.Offset = new Vector(textScroll.Offset.X, Math.Clamp(bounds.Y - y, 0, maximum));
    }

    private double StructuredScreenY(int position)
    {
        if (structuredPage == null || textScroll == null || presenter == null) return 0;
        var rectangle = presenter.TextLayout.HitTestTextPosition(structuredPage.ToDisplay(position));
        return Math.Clamp(rectangle.Y - textScroll.Offset.Y, 0, Math.Max(0, textScroll.Viewport.Height - rectangle.Height));
    }

    public bool TryReadPosition(out long first, out long end, out bool previous, out bool next)
    {
        first = end = 0;
        previous = next = false;
        if (placingStructured || structured is not { } source || structuredPage is not { } page || textScroll == null) return false;
        var visible = VisibleCharacters();
        first = source.Bytes.At(visible.First == 0 ? page.Start : page.ToSource(visible.First));
        var atBottom = textScroll.Offset.Y >= Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height) - 1;
        end = !page.HasNext && atBottom ? source.Bytes.Length : source.Bytes.At(page.ToSource(visible.End, trailing: true));
        previous = page.HasPrevious || visible.First > 0;
        next = page.HasNext || !atBottom;
        highlight.InvalidateVisual();
        return true;
    }

    private (int First, int End) VisibleCharacters() => textScroll == null ? (0, 0) :
        textContent.VisibleCharacters(textScroll.Offset.Y, textScroll.Offset.Y + textScroll.Viewport.Height - textContent.Padding.Bottom);

    public long ByteAtCharacter(int position) => structured is { } source && structuredPage is { } page
        ? source.Bytes.At(page.ToSource(position)) : 0;

    public void Seek(long offset)
    {
        if (structured is not { } source) return;
        CancelStructuredCommand();
        var position = source.Bytes.CharacterAt(offset);
        var row = new StructuredJsonRows(source, structuredFolds).At(position);
        PlaceStructured(StructuredCursor.At(position, row.Element), position, 0);
    }

    public void MoveViewport(double delta)
    {
        if (structuredPage is not { } page || textScroll == null) return;
        var target = textScroll.Offset.Y + delta;
        var maximum = Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height);
        if (target > maximum && page.HasNext || target < 0 && page.HasPrevious)
        {
            var position = page.ToSource(VisibleCharacters().First);
            PlaceStructured(CaptureStructuredCursor(), position, 0);
            target = textScroll.Offset.Y + delta;
            maximum = Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height);
        }
        textScroll.Offset = new Vector(textScroll.Offset.X, Math.Clamp(target, 0, maximum));
        ReadingPositionChanged?.Invoke();
    }

    private void ContinueStructuredCaret(Key direction)
    {
        if (structuredPage is not { } page || presenter == null) return;
        var lines = presenter.TextLayout.TextLines;
        var caret = textContent.CaretIndex;
        if (direction == Key.Down && page.HasNext && caret >= lines[^1].FirstTextSourceIndex ||
            direction == Key.Up && page.HasPrevious && caret < lines[0].Length ||
            direction == Key.Right && page.HasNext && caret == page.Syntax.Text.Length ||
            direction == Key.Left && page.HasPrevious && caret == 0)
        {
            var cursor = CaptureStructuredCursor();
            PlaceStructured(cursor, cursor.Caret, StructuredScreenY(cursor.Caret));
        }
    }
}

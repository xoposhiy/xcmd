// @implements ReadWorkingFileDiff
using Avalonia;
using Avalonia.Controls;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

public sealed partial class FileViewer
{
    private int DiffTop => diff == null ? 0 : diffPageStart + VisibleCharacters().First;

    private void PlaceDiff(int position, int selection = 0, bool force = false, int? pageStart = null)
    {
        if (diff == null) return;
        position = Math.Clamp(position, 0, diff.Text.Length);
        if (force || position < diffPageStart || position + selection >= diffPageStart + diffPageLength)
            RenderDiffPage(diff.Text, pageStart ?? Math.Max(0, position - FileSlice.PageSize / 4), position + selection);

        BindScrolling();
        var local = position - diffPageStart;
        TextContent.CaretIndex = local + selection;
        if (selection > 0)
        {
            TextContent.SelectionStart = local;
            TextContent.SelectionEnd = local + selection;
        }
        TextContent.UpdateLayout();
        if (textScroll != null && presenter != null)
            textScroll.Offset = new Vector(textScroll.Offset.X, presenter.TextLayout.HitTestTextPosition(local).Y);
        UpdateReadingPosition();
    }

    private void RenderDiffPage(string text, int start, int requiredEnd)
    {
        var end = Math.Min(text.Length, Math.Max(start + FileSlice.PageSize, requiredEnd));
        if (start > 0 && start < text.Length && char.IsLowSurrogate(text[start])) start--;
        if (end < text.Length && char.IsLowSurrogate(text[end])) end++;
        diffPageStart = start;
        diffPageLength = end - start;
        TextContent.Text = text[start..end];
        TextContent.SetSyntax(diff?.SyntaxPage(start, diffPageLength));
        ClearDiffHint();
    }

    private void UpdateDiffReadingPosition()
    {
        if (diff == null || textScroll == null || presenter == null) return;
        var (first, end) = VisibleCharacters();
        var top = diffPageStart + first;
        var bottom = diffPageStart + end;
        var lastPage = diffPageStart + diffPageLength == diff.Text.Length;
        var atBottom = textScroll.Offset.Y >= Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height) - 1;
        if (lastPage && atBottom) bottom = diff.Text.Length;
        FirstVisibleByte = diff.WorkingOffset(top);
        VisibleEnd = lastPage && atBottom ? diff.WorkingLength : diff.WorkingOffset(bottom);
        previousPage.IsEnabled = top > 0;
        nextPage.IsEnabled = bottom < diff.Text.Length;
        readingStatus.Text = diff.Changes.Length == 0 ? $"Diff · No changes · {SyntaxStatus}" : $"Diff · {SyntaxStatus} · Wrap {(wrapLines ? "on" : "off")}";
        ToolTip.SetTip(readingStatus, readingStatus.Text);
        Progress.Text = diff.Text.Length == 0 ? "100%" : $"{100d * bottom / diff.Text.Length:0.0}%";
        UpdateDiffScrollbar(top, Math.Max(1, bottom - top), diff.Text.Length);
        diffOverlay.Update(diff, presenter, diffPageStart, diffPageLength);
    }

    private void UpdateDiffScrollbar(int top, int visible, int length)
    {
        updatingScroll = true;
        FileScroll.Maximum = Math.Max(0, length - visible);
        FileScroll.ViewportSize = visible;
        FileScroll.LargeChange = visible;
        FileScroll.SmallChange = Math.Max(1, visible / 30);
        FileScroll.Value = Math.Min(FileScroll.Maximum, top);
        updatingScroll = false;
    }

    private void MoveDiffViewport(double target)
    {
        if (diff == null || textScroll == null) return;
        diffNavigationPosition = null;
        ClearDiffHint();
        var maximum = Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height);
        if (target > maximum && diffPageStart + diffPageLength < diff.Text.Length)
        {
            var delta = target - textScroll.Offset.Y;
            PlaceDiff(DiffTop, force: true);
            target = textScroll.Offset.Y + delta;
        }
        else if (target < 0 && diffPageStart > 0)
        {
            var delta = target - textScroll.Offset.Y;
            PlaceDiff(DiffTop, force: true, pageStart: Math.Max(0, diffPageStart - FileSlice.PageSize / 2));
            target = textScroll.Offset.Y + delta;
        }
        textScroll.Offset = new Vector(textScroll.Offset.X, Math.Clamp(target, 0, Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height)));
        UpdateReadingPosition();
    }

    private void SearchDiff(int direction, string query)
    {
        if (diff == null) return;
        var text = diff.Text;
        var backward = direction < 0;
        var origin = diffMatchPosition ?? DiffTop;
        var start = direction == 0 ? 0 : backward ? origin : Math.Min(text.Length, origin + (diffMatchPosition.HasValue ? query.Length : 0));
        var found = backward ? text.AsSpan(0, start).LastIndexOf(query, StringComparison.OrdinalIgnoreCase)
            : text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
        var wrapped = found < 0 && start != (backward ? text.Length : 0);
        if (wrapped)
            found = backward ? text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase) : text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (found < 0) { searchStatus.Text = "No matches in this diff"; return; }
        diffMatchPosition = found;
        diffNavigationPosition = null;
        PlaceDiff(found, query.Length);
        searchStatus.Text = $"Match on row {diff.RowAt(found) + 1:N0}{(wrapped ? " · Wrapped" : "")}";
    }
}

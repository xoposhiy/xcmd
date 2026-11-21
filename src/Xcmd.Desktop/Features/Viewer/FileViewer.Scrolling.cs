// @implements OpenViewers, ReadLargeFiles, FollowLog, ReadWorkingFileDiff
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

public sealed partial class FileViewer
{
    private ScrollViewer? textScroll;
    private TextPresenter? presenter;
    private bool updatingScroll;
    public long FirstVisibleByte { get; private set; }

    private void BindScrolling()
    {
        TextContent.UpdateLayout();
        if (textScroll == null)
        {
            textScroll = TextContent.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (textScroll != null) textScroll.ScrollChanged += (_, _) => UpdateReadingPosition();
        }
        presenter ??= TextContent.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
    }

    private (int First, int End) VisibleCharacters()
    {
        if (textScroll == null || presenter == null) return (0, 0);
        var top = textScroll.Offset.Y;
        var bottom = top + textScroll.Viewport.Height - TextContent.Padding.Bottom;
        return TextContent.VisibleCharacters(top, bottom);
    }

    private long ByteAtCharacter(int position)
    {
        if (slice == null) return 0;
        if (structuredReader.IsActive) return structuredReader.ByteAtCharacter(position);
        if (diff != null) return diff.WorkingOffset(diffPageStart + position);
        if (Mode == FileViewMode.Text)
            return ViewerText.ByteOffset(slice, CurrentEncoding(), position);
        var text = TextContent.Text ?? "";
        var rows = text.AsSpan(0, Math.Clamp(position, 0, text.Length)).Count('\n');
        return slice.Offset + Math.Min(slice.Count, rows * (Mode == FileViewMode.Hex ? 16 : 64));
    }

    private System.Text.Encoding CurrentEncoding() => selectedEncoding.CodePage > 0 ? ViewerText.GetEncoding(selectedEncoding.CodePage) : detected.Encoding;

    private void UpdateReadingPosition()
    {
        if (slice == null || Mode == FileViewMode.Image || textScroll == null || presenter == null) return;
        if (structuredReader.IsActive) { UpdateStructuredProgress(); return; }
        if (diff != null) { UpdateDiffReadingPosition(); return; }
        var visible = VisibleCharacters();
        FirstVisibleByte = ByteAtCharacter(visible.First);
        VisibleEnd = Math.Clamp(ByteAtCharacter(visible.End), 0, FileLength);
        var atBottom = textScroll.Offset.Y >= Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height) - 1;
        if (!slice.HasNext && atBottom) VisibleEnd = FileLength;
        previousPage.IsEnabled = FirstVisibleByte > 0;
        nextPage.IsEnabled = VisibleEnd < FileLength;
        var encoding = Mode == FileViewMode.Text ? selectedEncoding.CodePage > 0 ? selectedEncoding.Label + " (Manual)" : detected.Description : "ASCII bytes";
        readingStatus.Text = $"{(IsArchiveReport ? "Archive report" : Mode.ToString())}{(automaticMode ? " (Auto)" : "")} · {encoding} · Wrap {(Mode == FileViewMode.Text && wrapLines ? "on" : "off")} · {VisibleEnd:N0}/{FileLength:N0} bytes";
        if (IsFollowing) readingStatus.Text += " · Following";
        if (Mode == FileViewMode.Text && !IsArchiveReport) readingStatus.Text += " · " + SyntaxStatus;
        if (indexingStructured) readingStatus.Text += " · Indexing JSON…";
        ToolTip.SetTip(readingStatus, readingStatus.Text);
        Progress.Text = $"{ViewedPercent:0.0}%";
        UpdateFileScrollbar(FirstVisibleByte, Math.Max(1, VisibleEnd - FirstVisibleByte), FileLength);
    }

    private void UpdateFileScrollbar(long firstByte, long visibleBytes, long length)
    {
        updatingScroll = true;
        FileScroll.Maximum = Math.Max(0, length - visibleBytes);
        FileScroll.ViewportSize = visibleBytes;
        FileScroll.LargeChange = visibleBytes;
        FileScroll.SmallChange = Math.Max(1, visibleBytes / 30);
        FileScroll.Value = Math.Min(FileScroll.Maximum, firstByte);
        updatingScroll = false;
    }

    private void UpdateStructuredProgress()
    {
        if (!structuredReader.TryReadPosition(out var first, out var end, out var previous, out var next)) return;
        FirstVisibleByte = first;
        VisibleEnd = end;
        previousPage.IsEnabled = previous;
        nextPage.IsEnabled = next;
        Progress.Text = $"{ViewedPercent:0.0}%";
        UpdateFileScrollbar(FirstVisibleByte, Math.Max(1, VisibleEnd - FirstVisibleByte), FileLength);
    }

    private async Task SeekBoundaryAsync(bool end, CancellationToken token)
    {
        PauseFollowing();
        if (structuredReader.IsActive) { structuredReader.Seek(end ? FileLength : 0); return; }
        if (diff != null)
        {
            diffNavigationPosition = null;
            PlaceDiff(end ? diff.Text.Length : 0);
            return;
        }
        await LoadPageAsync(end ? Math.Max(0, FileLength - FileSlice.PageSize) / 4 * 4 : 0, token);
        token.ThrowIfCancellationRequested();
        BindScrolling();
        TextContent.CaretIndex = end ? TextContent.Text?.Length ?? 0 : 0;
        TextContent.UpdateLayout();
        if (textScroll != null) textScroll.Offset = new Vector(0, end ? Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height) : 0);
        UpdateReadingPosition();
    }

    // PauseFollowing resets the scroll bar to the current position, so the requested offset is read first.
    private void SeekFromScrollBar(long offset)
    {
        if (structuredReader.IsActive) { structuredReader.Seek(offset >= FileScroll.Maximum ? FileLength : offset); return; }
        if (diff != null)
        {
            diffNavigationPosition = null;
            PlaceDiff(offset >= FileScroll.Maximum ? diff.Text.Length : (int)offset);
            return;
        }
        PauseFollowing();
        Run(token => SeekByteAsync(offset, token));
    }

    private async Task SeekByteAsync(long offset, CancellationToken token)
    {
        if (offset >= FileScroll.Maximum) { await SeekBoundaryAsync(true, token); return; }
        await LoadPageAsync(Math.Max(0, offset - FileSlice.PageSize / 4) / 4 * 4, token);
        token.ThrowIfCancellationRequested();
        RestoreTopByte(offset);
    }

    private void RestoreTopByte(long offset)
    {
        if (slice == null) return;
        if (structuredReader.IsActive) { structuredReader.Seek(offset); return; }
        BindScrolling();
        var relative = (int)Math.Clamp(offset - slice.Offset, 0, slice.Count);
        var position = Mode == FileViewMode.Text ? ViewerText.Decode(new(slice.Offset, slice.Offset + relative, slice.Bytes[..relative]), CurrentEncoding()).Length
            : relative / (Mode == FileViewMode.Hex ? 16 : 64) * ((TextContent.Text ?? "").IndexOf('\n') + 1);
        TextContent.CaretIndex = Math.Min(position, TextContent.Text?.Length ?? 0);
        TextContent.UpdateLayout();
        if (textScroll != null && presenter != null)
            textScroll.Offset = new Vector(textScroll.Offset.X, presenter.TextLayout.HitTestTextPosition(TextContent.CaretIndex).Y);
        UpdateReadingPosition();
    }

    private void MoveViewport(double delta)
    {
        if (delta < 0) PauseFollowing();
        if (Mode == FileViewMode.Image || slice == null) return;
        if (textScroll == null || presenter == null) BindScrolling();
        if (textScroll == null) return;
        if (structuredReader.IsActive) { structuredReader.MoveViewport(delta); return; }
        var target = textScroll.Offset.Y + delta;
        if (diff != null) { MoveDiffViewport(target); return; }
        var maximum = Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height);
        if ((target > maximum && slice.HasNext) || (target < 0 && slice.Offset > 0))
        {
            Run(token => LoadAdjacentPageAsync(delta, token));
            return;
        }
        textScroll.Offset = new Vector(textScroll.Offset.X, Math.Clamp(target, 0, Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height)));
        UpdateReadingPosition();
    }

    private async Task LoadAdjacentPageAsync(double delta, CancellationToken token)
    {
        if (slice == null) return;
        var anchor = FirstVisibleByte;
        var offset = Math.Max(0, slice.Offset + Math.Sign(delta) * FileSlice.PageSize / 2);
        await LoadPageAsync(offset, token);
        token.ThrowIfCancellationRequested();
        RestoreTopByte(Math.Max(offset, anchor));
        if (textScroll != null)
            textScroll.Offset = new Vector(textScroll.Offset.X, Math.Clamp(textScroll.Offset.Y + delta, 0, Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height)));
        UpdateReadingPosition();
    }

    private bool HandleReadingKey(KeyEventArgs e)
    {
        if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.None) { ToggleWrapping(); return true; }
        var textHasFocus = Mode is FileViewMode.Text or FileViewMode.Structured && TextContent.IsKeyboardFocusWithin;
        if (e.Key is Key.Home or Key.End &&
            (e.KeyModifiers == KeyModifiers.Control || !textHasFocus && e.KeyModifiers == KeyModifiers.None))
        { Run(token => SeekBoundaryAsync(e.Key == Key.End, token)); return true; }
        if (e.KeyModifiers != KeyModifiers.None) return false;
        if (textHasFocus && e.Key is Key.Up or Key.Down)
        {
            if (e.Key == Key.Up && IsFollowing) PauseFollowing();
            return false;
        }
        var delta = e.Key switch
        {
            Key.Down => 20,
            Key.Up => -20,
            Key.PageDown or Key.Space => Math.Max(20, (textScroll?.Viewport.Height ?? 500) - 20),
            Key.PageUp => -Math.Max(20, (textScroll?.Viewport.Height ?? 500) - 20),
            _ => 0
        };
        if (delta == 0) return false;
        MoveViewport(delta); return true;
    }

    private void OnViewerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (feedback.IsActive || Commands.IsOpen || Search.IsPointerOver || Mode == FileViewMode.Image || e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.Delta.Y == 0) return;
        MoveViewport(-e.Delta.Y * 60);
        e.Handled = true;
    }
}

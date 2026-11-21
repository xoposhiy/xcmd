// @implements ChooseViewMode, ReadLargeFiles
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

public sealed partial class FileViewer
{
    private readonly StructuredJsonReader structuredReader;
    private bool indexingStructured;
    private bool jsonCandidate;
    private bool CanUseStructured => slice != null && !binaryFile && !IsArchiveReport;

    private void CaptureByteCursor(out long caret, out long start, out long end)
    {
        if (structuredReader.IsActive) structuredReader.CapturePosition(out caret, out start, out end);
        else if (Mode == FileViewMode.Text)
        {
            caret = ByteAtCharacter(TextContent.CaretIndex);
            start = ByteAtCharacter(TextContent.SelectionStart);
            end = ByteAtCharacter(TextContent.SelectionEnd);
        }
        else caret = start = end = FirstVisibleByte;

        if (start == end && (caret < FirstVisibleByte || caret > VisibleEnd))
            caret = start = end = FirstVisibleContentByte();
    }

    private long FirstVisibleContentByte()
    {
        if (Mode != FileViewMode.Text || TextContent.Text is not { } text) return FirstVisibleByte;
        var (first, end) = VisibleCharacters();
        while (first < end && text[first] is ' ' or '\t' or '\r' or '\n') first++;
        return first < end ? ByteAtCharacter(first) : FirstVisibleByte;
    }

    private async Task EnterStructuredAsync(long caret, long start, long end, CancellationToken token)
    {
        if (!CanUseStructured || structuredReader.IsActive) return;
        indexingStructured = true;
        UpdateReadingPosition();
        try
        {
            var encoding = CurrentEncoding();
            BindScrolling();
            structuredReader.AttachViewport(textScroll, presenter);
            await structuredReader.OpenAsync(CurrentPath, encoding, caret, start, end, token);
            if (structuredReader.IsActive) information.Text = $"{FileLength:N0} bytes · {encoding.WebName} · Structured JSON";
            UpdateControls();
        }
        finally
        {
            if (!token.IsCancellationRequested) { indexingStructured = false; UpdateReadingPosition(); }
        }
    }

    private void SetStructuredMode()
    {
        if (diff != null) ClearDiff();
        Mode = FileViewMode.Structured;
        automaticMode = false;
        UpdateControls();
    }

    private async Task LeaveStructuredAsync(FileViewMode mode, long caret, long start, long end, CancellationToken token)
    {
        var offset = Math.Max(0, caret - FileSlice.PageSize / 4) / 4 * 4;
        var page = await files.ReadAsync(CurrentPath, offset, token);
        token.ThrowIfCancellationRequested();
        structuredReader.Reset();
        slice = page;
        Mode = mode;
        await RenderPageAsync(token);
        token.ThrowIfCancellationRequested();
        RestoreTopByte(caret);
        if (mode == FileViewMode.Text && start != end)
        {
            TextContent.SelectionStart = CharacterInPage(page, start);
            TextContent.SelectionEnd = CharacterInPage(page, end);
        }
    }

    private int CharacterInPage(FileSlice page, long offset)
    {
        var count = (int)Math.Clamp(offset - page.Offset, 0, page.Count);
        return ViewerText.Decode(new(page.Offset, offset, page.Bytes[..count]), CurrentEncoding()).Length;
    }

    private async Task RefreshStructuredAsync(CancellationToken token)
    {
        if (!structuredReader.IsActive) return;
        if (!await structuredReader.HasChangedAsync(CurrentPath, token)) return;
        CaptureByteCursor(out var caret, out var start, out var end);
        ClearSearch(focus: false);
        await LeaveStructuredAsync(FileViewMode.Text, caret, start, end, token);
        await EnterStructuredAsync(caret, start, end, token);
    }
}

// @implements OpenViewers, QuickViewFile, ChooseViewMode, ReadLargeFiles, HighlightSource, FollowLog, InspectArchives, ReadWorkingFileDiff
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class FileViewer
{
    public Task OpenAsync() => RunAsync(LoadFileAsync);

    internal async void Preview(string? path)
    {
        previewPath = path;
        if (HasFile) { await OpenAsync(); return; }
        loading?.Cancel(); loading = null; IsLoading = false;
        ResetFile();
        error.IsVisible = false;
        information.Text = "";
        readingStatus.Text = "Select a file to preview.";
        Progress.Text = "";
    }

    private async void Run(Func<CancellationToken, Task> action) => await RunAsync(action);

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        loading?.Cancel();
        using var request = new CancellationTokenSource();
        loading = request; IsLoading = true; error.IsVisible = false;
        try { await action(request.Token); }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            if (!closed && loading == request) ShowReadFailure(failure.Message);
        }
        finally
        {
            if (loading == request) { loading = null; IsLoading = false; if (!closed) UpdateControls(); }
        }
    }

    internal void ShowReadFailure(string message)
    {
        ShowError(message);
        information.Text = "Could not load this view. Choose another mode or file.";
        if (slice != null) return;
        readingStatus.Text = IsEmbedded ? "Could not load file" : "Could not load file · Alt+Left/Right changes file";
        Progress.Text = "—";
    }

    private async Task LoadFileAsync(CancellationToken token)
    {
        BeginFileLoad();
        if (!IsEmbedded) FocusReader();
        await ReadFileAsync(token);
        if (!IsEmbedded || IsKeyboardFocusWithin) FocusReader();
        await RefreshGitAsync(wantDiff: false, reloadText: false, token);
    }

    private void BeginFileLoad()
    {
        ResetFile();
        information.Text = "Loading…";
        readingStatus.Text = "Loading…"; Progress.Text = "…";
    }

    private async Task ReadFileAsync(CancellationToken token)
    {
        var path = CurrentPath;
        var page = await files.ReadAsync(path, 0, token);
        var encoding = await Task.Run(() => ViewerText.Detect(page.Bytes), token);
        token.ThrowIfCancellationRequested();
        IsArchiveReport = files.IsArchiveReport(path);
        if (IsArchiveReport) UpdateDocumentTitle();
        slice = page; detected = encoding;
        imageSignature = ViewerText.IsImage(page.Bytes);
        if (!imageSignature) imageUnavailable = "This file is not a supported image.";
        else if (page.Length > ViewerFiles.MaximumImageBytes) imageUnavailable = "Image preview is limited to 32 MiB. Use Open externally for this image.";
        var imageReady = CanUseImage && await PrepareImageAsync(token);
        token.ThrowIfCancellationRequested();
        binaryFile = imageSignature || ViewerText.IsBinary(page.Bytes, encoding.Encoding);
        Mode = imageReady ? FileViewMode.Image : binaryFile ? FileViewMode.Hex : FileViewMode.Text;
        automaticMode = true;
        await RenderPageAsync(token);
        token.ThrowIfCancellationRequested();
    }

    private void ResetFile()
    {
        PauseFollowing();
        ClearSearch(focus: false);
        ResetGitView();
        slice = null; TextContent.Text = "";
        ReleaseImage();
        TextContent.SetSyntax(null); syntaxLanguage = null;
        FirstVisibleByte = 0; VisibleEnd = 0;
        imageUnavailable = null; imageSignature = false; binaryFile = false;
        IsArchiveReport = false;
        Mode = FileViewMode.Text;
        selectedEncoding = EncodingChoice.All[0];
        UpdateControls();
        UpdateDocumentTitle();
    }

    private void ReleaseImage()
    {
        image.Source = null;
        decodedImage?.Bitmap.Dispose(); decodedImage = null;
    }

    private Task NavigateAsync(int delta)
    {
        if (index + delta < 0 || index + delta >= paths.Length) return Task.CompletedTask;
        index += delta;
        return OpenAsync();
    }

    private async Task ChangeModeAsync(FileViewMode mode, CancellationToken token)
    {
        PauseFollowing();
        if (IsArchiveReport && mode != FileViewMode.Text) return;
        if (mode == FileViewMode.Image && (!CanUseImage || !await PrepareImageAsync(token))) return;
        ClearSearch();
        var anchor = FirstVisibleByte;
        if (diff != null) await ReloadTextAsync(anchor, token);
        Mode = mode;
        automaticMode = false;
        if (slice == null) slice = await files.ReadAsync(CurrentPath, 0, token);
        await RenderPageAsync(token);
        FocusReader();
    }

    private async Task LoadPageAsync(long offset, CancellationToken token)
    {
        if (Mode == FileViewMode.Image) return;
        var page = await files.ReadAsync(CurrentPath, Math.Max(0, offset), token);
        token.ThrowIfCancellationRequested(); slice = page;
        await RenderPageAsync(token);
    }

    private async Task RenderPageAsync(CancellationToken token)
    {
        if (slice == null) return;
        UpdateControls();
        if (Mode == FileViewMode.Image) { await RenderImageAsync(token); return; }
        var page = slice;
        var mode = Mode;
        var choice = selectedEncoding;
        var encoding = choice is { CodePage: > 0 } ? ViewerText.GetEncoding(choice.CodePage) : detected.Encoding;
        var text = await Task.Run(() => mode == FileViewMode.Text ? ViewerText.Decode(page, encoding) : ViewerText.FormatBytes(page, mode == FileViewMode.Hex), token);
        token.ThrowIfCancellationRequested();
        TextContent.Text = text; TextContent.CaretIndex = 0;
        await HighlightPageAsync(token);
        token.ThrowIfCancellationRequested();
        var format = mode == FileViewMode.Text ? (choice is { CodePage: > 0 } ? choice.Label + " (manual)" : detected.Description) : mode + " · Byte offsets in hex";
        information.Text = $"{page.Length:N0} bytes · {format}\n{(page.Length == 0 ? "Empty file" : $"Bytes {page.Offset:N0}–{page.Offset + page.Count - 1:N0}")} · {PageDescription(page)}";
        BindScrolling();
        UpdateReadingPosition();
    }

    private async Task RenderImageAsync(CancellationToken token)
    {
        if (decodedImage == null && !await PrepareImageAsync(token)) return;
        if (decodedImage == null) return;
        image.Source = decodedImage.Bitmap;
        information.Text = $"{FileLength:N0} bytes · {decodedImage.Description}";
        VisibleEnd = FileLength;
        readingStatus.Text = $"Image{(automaticMode ? " (Auto)" : "")} · {decodedImage.Description}";
        Progress.Text = "100%";
    }

    private bool CanUseImage => imageSignature && imageUnavailable == null && slice?.Length <= ViewerFiles.MaximumImageBytes;

    private async Task<bool> PrepareImageAsync(CancellationToken token)
    {
        if (decodedImage != null) return true;
        try
        {
            var bytes = await files.ReadImageAsync(CurrentPath, token);
            var decoded = await Task.Run(() => ViewerImage.Decode(bytes), token);
            if (token.IsCancellationRequested) { decoded.Bitmap.Dispose(); token.ThrowIfCancellationRequested(); }
            decodedImage = decoded;
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        { imageUnavailable = failure.Message; return false; }
    }

    private static string PageDescription(FileSlice page) => page.Length <= FileSlice.PageSize ? "Whole file"
        : $"Page {page.Offset / FileSlice.PageSize + 1:N0}/{(page.Length - 1) / FileSlice.PageSize + 1:N0} · 16 KiB per page";
}

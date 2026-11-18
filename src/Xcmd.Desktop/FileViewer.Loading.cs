// @implements OpenViewers, QuickViewFile, ChooseViewMode, ReadLargeFiles, HighlightSource, FollowLog, OpenViewedFile, InspectArchives, ReportHighlighting
using Avalonia.Input;
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
            if (!closed && loading == request)
            {
                ShowError(failure.Message); information.Text = "Could not load this view. Choose another mode or file.";
                if (slice == null)
                {
                    readingStatus.Text = IsEmbedded ? "Could not load file" : "Could not load file · Alt+Left/Right changes file";
                    Progress.Text = "—";
                }
            }
        }
        finally
        {
            if (loading == request) { loading = null; IsLoading = false; if (!closed) UpdateControls(); }
        }
    }

    private async Task LoadFileAsync(CancellationToken token)
    {
        ResetFile();
        information.Text = "Loading…";
        readingStatus.Text = "Loading…"; Progress.Text = "…";
        if (!IsEmbedded) FocusReader();
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
        Mode = imageReady ? FileViewMode.Image : imageSignature || ViewerText.IsBinary(page.Bytes, encoding.Encoding) ? FileViewMode.Hex : FileViewMode.Text;
        automaticMode = true;
        await RenderPageAsync(token);
        token.ThrowIfCancellationRequested();
        if (!IsEmbedded || IsKeyboardFocusWithin) FocusReader();
    }

    private void ResetFile()
    {
        PauseFollowing();
        ClearSearch(focus: false);
        slice = null; TextContent.Text = "";
        ReleaseImage();
        TextContent.SetSyntax(null); syntaxLanguage = null;
        FirstVisibleByte = 0; VisibleEnd = 0;
        imageUnavailable = null; imageSignature = false;
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

    private void UpdateControls()
    {
        UpdateModeItems();
        UpdateEncodingItems();
        UpdateSyntaxControls();
        var text = Mode != FileViewMode.Image;
        UpdateTextItems(text);
        previousPage.IsEnabled = text && FirstVisibleByte > 0;
        nextPage.IsEnabled = text && VisibleEnd < FileLength;
        if (previousFile != null) previousFile.IsEnabled = index > 0;
        if (nextFile != null) nextFile.IsEnabled = index + 1 < paths.Length;
        UpdateWrapping();
    }

    private void UpdateModeItems()
    {
        foreach (var pair in modeItems)
        {
            pair.Value.IsChecked = pair.Key == Mode;
            pair.Value.IsEnabled = slice != null && (!IsArchiveReport || pair.Key == FileViewMode.Text);
            Avalonia.Controls.ToolTip.SetTip(pair.Value, IsArchiveReport ? "This view shows an archive report." : null);
        }
        modeItems[FileViewMode.Image].IsEnabled = CanUseImage;
        Avalonia.Controls.ToolTip.SetTip(modeItems[FileViewMode.Image], imageUnavailable);
    }

    private void UpdateEncodingItems()
    {
        var enabled = Mode == FileViewMode.Text && slice != null && !IsArchiveReport;
        foreach (var pair in encodingItems) { pair.Value.IsChecked = pair.Key == selectedEncoding.CodePage; pair.Value.IsEnabled = enabled; }
    }

    private void UpdateTextItems(bool text)
    {
        foreach (var item in textCommands) item.IsEnabled = text && slice != null;
        TextContent.IsVisible = text; image.IsVisible = !text;
        FileScroll.IsVisible = text;
        wrapItem.IsEnabled = Mode == FileViewMode.Text && slice != null;
        wrapItem.IsChecked = wrapLines;
        informationItem.IsChecked = information.IsVisible;
        followItem.IsEnabled = slice != null && text && !IsArchiveReport && !IsDocumentReadOnly && !ArchivePath.IsEntry(CurrentPath);
        Search.IsEnabled = text && slice != null;
        UpdateExternalItems();
    }

    private void OnViewerKey(object? sender, KeyEventArgs e)
    {
        if (feedback.IsActive) return;
        if (e.Key == Key.F12 && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        { ReportSyntaxProblem(); e.Handled = true; return; }
        if (e.Key == Key.F && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        { ToggleFollowing(); e.Handled = true; return; }
        if (HandleSearchStart(e)) { e.Handled = true; return; }
        if (Commands.IsOpen) return;
        if (e.Key == Key.Escape) { if (IsSearchVisible) ClearSearch(); else close(); e.Handled = true; return; }
        if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control) { close(); e.Handled = true; return; }
        if (HandleFileKey(e)) { e.Handled = true; return; }
        if (slice == null) return;
        if (HandleSearchKey(e)) { e.Handled = true; return; }
        if (Search.IsKeyboardFocusWithin) return;
        if (HandleViewKey(e)) { e.Handled = true; return; }
        if (HandleReadingKey(e)) { e.Handled = true; return; }
        if (HandleNavigationKey(e)) e.Handled = true;
    }

    private bool HandleFileKey(KeyEventArgs e)
    {
        if (!HasFile) return false;
        if (e.Key == Key.F4 && e.KeyModifiers == KeyModifiers.None)
        { if (AllowExternalAction() && !editKeyHeld) { editKeyHeld = true; RunExternal(() => edit(CurrentPath)); } return true; }
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        { if (AllowExternalAction()) RunExternal(() => open(CurrentPath)); return true; }
        if (!IsEmbedded && e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.Left or Key.Right)
        { _ = NavigateAsync(e.Key == Key.Left ? -1 : 1); return true; }
        return false;
    }

    private bool HandleNavigationKey(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.PageDown or Key.PageUp)
        { Run(token => LoadPageAsync(ByteOffset + (e.Key == Key.PageDown ? FileSlice.PageSize : -FileSlice.PageSize), token)); return true; }
        if (e.KeyModifiers == KeyModifiers.None && e.Key is >= Key.D1 and <= Key.D4)
        { Run(token => ChangeModeAsync((FileViewMode)(e.Key - Key.D1), token)); return true; }
        return false;
    }

    private bool HandleViewKey(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control) return false;
        if (e.Key == Key.R) ToggleWrapping();
        else if (e.Key == Key.I) ToggleInformation();
        else return false;
        return true;
    }
}

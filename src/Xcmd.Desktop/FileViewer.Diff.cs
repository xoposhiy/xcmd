// @implements ReadWorkingFileDiff
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class FileViewer
{
    private readonly ViewerGitDiff gitDiff = new();
    private readonly ViewerDiffOverlay diffOverlay = new ViewerDiffOverlay().WithFeedbackId("FileViewer.diffOverlay");
    private readonly ViewerGitStatusLabel gitStatus = new ViewerGitStatusLabel().WithFeedbackId("FileViewer.gitStatus");
    private ViewerGitFile? gitFile;
    private ViewerDiffDisplay? diff;
    private int diffPageStart;
    private int diffPageLength;
    private int? diffNavigationPosition;
    private int? diffMatchPosition;
    private bool diffKeyHeld;
    private bool pendingDiffKey;
    private MenuItem diffItem = null!;
    private MenuItem nextChange = null!;
    private MenuItem previousChange = null!;
    private bool CanReadGitStatus => HasFile && document == null && !ArchivePath.IsEntry(CurrentPath);
    private bool DiffAvailable => CanShowDiff(gitFile);

    private bool CanShowDiff(ViewerGitFile? file) =>
        file?.IsTracked == true && slice != null && Mode == FileViewMode.Text && !IsArchiveReport && !binaryFile;

    private Grid BuildStatusBar()
    {
        var status = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") }.WithFeedbackId("FileViewerWindow.BuildContent.2");
        Grid.SetColumn(readingStatus, 1);
        Grid.SetColumn(feedbackStatus, 1);
        status.Children.Add(gitStatus);
        status.Children.Add(readingStatus);
        status.Children.Add(feedbackStatus);
        gitStatus.ToggleRequested += ToggleDiff;
        return status;
    }

    private void AddDiffCommands(List<MenuItem> view)
    {
        diffItem = MenuCommand("Show Diff", "viewer.diff", ToggleDiff, new(Key.D, KeyModifiers.Control));
        nextChange = MenuCommand("Next Change", "viewer.next-change", () => MoveToChange(false), new(Key.PageDown, KeyModifiers.Control));
        previousChange = MenuCommand("Previous Change", "viewer.previous-change", () => MoveToChange(true), new(Key.PageUp, KeyModifiers.Control));
        view.Add(diffItem);
    }

    private void UpdateDiffCommands()
    {
        diffItem.IsVisible = DiffAvailable || diff != null;
        var action = diff == null ? "Show Diff" : "Show Text";
        diffItem.Header = action;
        gitStatus.SetAction(diffItem.IsVisible ? action : null);
        nextChange.IsVisible = previousChange.IsVisible = diff != null;
        nextChange.IsEnabled = previousChange.IsEnabled = diff?.Changes.Length > 0;
    }

    private void ResetGitView()
    {
        ClearDiff();
        gitFile = null;
        pendingDiffKey = false;
        ShowGitStatus(!HasFile ? null : document != null ? "Snapshot" : ArchivePath.IsEntry(CurrentPath) ? "Archive entry" : "Checking status…");
    }

    private void ShowGitStatus(string? status)
    {
        gitStatus.Text = status == null ? null : "Git: " + status;
        gitStatus.IsVisible = status != null;
    }

    private static string StatusText(ViewerGitStatus status) => status switch
    {
        ViewerGitStatus.OutsideRepository => "Outside repository",
        ViewerGitStatus.NotTracked => "Not tracked",
        ViewerGitStatus.PartiallyStaged => "Partially staged",
        ViewerGitStatus.Unavailable => "Status unavailable",
        _ => status.ToString()
    };

    internal void RefreshAfterActivation()
    {
        if (CanReadGitStatus && slice != null && !IsLoading && !IsSearching && !closed && !feedback.IsActive)
            Run(token => RefreshGitAsync(diff != null, reloadText: !IsFollowing, token));
    }

    private async Task RefreshGitAsync(bool wantDiff, bool reloadText, CancellationToken token)
    {
        if (!CanReadGitStatus) return;
        var anchor = FirstVisibleByte;
        var status = await ReadGitStatusAsync(token);
        token.ThrowIfCancellationRequested();
        wantDiff |= pendingDiffKey;
        pendingDiffKey = false;
        if (wantDiff && CanShowDiff(status) && await TryShowDiffAsync(status, anchor, token)) return;
        if (diff != null || reloadText) await ReloadTextAsync(anchor, token);
        token.ThrowIfCancellationRequested();
        SetGitState(status);
        UpdateDiffCommands();
    }

    private void SetGitState(ViewerGitFile status)
    {
        gitFile = status;
        ShowGitStatus(StatusText(status.Status));
    }

    private async Task<ViewerGitFile> ReadGitStatusAsync(CancellationToken token)
    {
        try { return await gitDiff.StatusAsync(CurrentPath, token); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        { return new(ViewerGitStatus.Unavailable); }
    }

    private async Task<bool> TryShowDiffAsync(ViewerGitFile status, long anchor, CancellationToken token)
    {
        ViewerDiffDisplay display;
        try
        {
            var comparison = await gitDiff.CompareAsync(status, CurrentPath, selectedEncoding.CodePage, token);
            display = await Task.Run(() => new ViewerDiffDisplay(comparison), token);
            await display.HighlightAsync(highlighter, CurrentPath, syntaxLanguage, token);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            ShowError("Could not show diff. " + failure.Message);
            return false;
        }
        token.ThrowIfCancellationRequested();

        var previous = diff;
        var unchanged = previous != null && previous.Text == display.Text && previous.Changes.SequenceEqual(display.Changes);
        diff = display;
        SetGitState(status);
        ClearDiffHint();
        if (unchanged)
        {
            TextContent.SetSyntax(display.SyntaxPage(diffPageStart, diffPageLength));
            UpdateReadingPosition();
        }
        else
        {
            diffNavigationPosition = diffMatchPosition = null;
            ApplyDiffAppearance();
            PlaceDiff(display.PositionAtWorkingOffset(anchor), force: true);
        }
        UpdateControls();
        return true;
    }

    private async Task ReloadTextAsync(long anchor, CancellationToken token)
    {
        var wasDiff = diff != null;
        if (wasDiff) { ClearSearch(focus: false); ClearDiff(); }
        var offset = wasDiff ? Math.Max(0, anchor - FileSlice.PageSize / 4) / 4 * 4 : ByteOffset;
        var page = await files.ReadAsync(CurrentPath, offset, token);
        token.ThrowIfCancellationRequested();
        if (!wasDiff && slice is { } previous && previous.Length == page.Length && previous.Bytes.AsSpan().SequenceEqual(page.Bytes)) return;
        slice = page;
        if (Mode == FileViewMode.Image) ReleaseImage();
        await RenderPageAsync(token);
        token.ThrowIfCancellationRequested();
        if (Mode != FileViewMode.Image) RestoreTopByte(anchor);
    }

    private void ToggleDiff()
    {
        if (!DiffAvailable && diff == null) return;
        PauseFollowing();
        var anchor = FirstVisibleByte;
        ClearSearch();
        if (diff == null) Run(token => RefreshGitAsync(wantDiff: true, reloadText: false, token));
        else Run(token => ReloadTextAsync(anchor, token));
    }

    internal void HandleDiffKey(object? sender, KeyEventArgs e)
    {
        if (IsDiffKey(e))
        {
            var repeat = diffKeyHeld;
            diffKeyHeld = true;
            if (e.KeyModifiers == KeyModifiers.Control && TryToggleDiffFromKey(repeat)) e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.Control || feedback.IsActive || Commands.IsOpen || diff == null
            || e.Key is not (Key.PageUp or Key.PageDown)) return;
        MoveToChange(e.Key == Key.PageUp);
        e.Handled = true;
    }

    internal void ReleaseDiffKey(object? sender, KeyEventArgs e)
    {
        if (!IsDiffKey(e)) return;
        var sawPress = diffKeyHeld;
        diffKeyHeld = false;
        // A system shortcut can consume the press while Windows still sends the release.
        if (!sawPress && e.KeyModifiers == KeyModifiers.Control && TryToggleDiffFromKey(repeat: false)) e.Handled = true;
    }

    private bool TryToggleDiffFromKey(bool repeat)
    {
        if (feedback.IsActive || Commands.IsOpen) return false;
        var waitingForStatus = gitFile == null && IsLoading && CanReadGitStatus;
        if (!DiffAvailable && diff == null && !waitingForStatus) return false;
        if (!repeat)
        {
            if (waitingForStatus) pendingDiffKey = true;
            else ToggleDiff();
        }
        return true;
    }

    private static bool IsDiffKey(KeyEventArgs e) => e.Key == Key.D || e.PhysicalKey == PhysicalKey.D;

    private void ClearDiff()
    {
        if (diff == null) return;
        diff = null;
        diffPageStart = diffPageLength = 0;
        diffNavigationPosition = diffMatchPosition = null;
        ClearDiffHint();
        ApplyDiffAppearance();
    }

    private void ApplyDiffAppearance()
    {
        diffOverlay.IsVisible = diff != null;
        diffOverlay.ApplyTheme(activeTheme);
        TextContent.SetSyntaxTheme(activeTheme, diff == null ? null : diffOverlay.HighlightBackgrounds);
        TextContent.Padding = new Thickness(diff == null ? 10 : ViewerDiffOverlay.GutterWidth, 6);
        var brush = diff == null ? Brush.Parse(activeTheme.Background) : Brushes.Transparent;
        TextContent.Background = brush;
        TextContent.Foreground = diff == null ? Brush.Parse(activeTheme.Foreground) : diffOverlay.Foreground;
        TextContent.CaretBrush = TextContent.Foreground;
        foreach (var state in new[] { "", "Focused", "PointerOver", "Disabled" }) TextContent.Resources["TextControlBackground" + state] = brush;
    }

    private void MoveToChange(bool backward)
    {
        if (diff == null) return;
        var origin = diffNavigationPosition ?? DiffTop;
        var changes = diff.Changes;
        var index = Array.BinarySearch(changes, origin);
        index = index < 0 ? ~index - (backward ? 1 : 0) : index + (backward ? -1 : diffNavigationPosition.HasValue ? 1 : 0);
        if (index < 0 || index >= changes.Length) return;
        diffNavigationPosition = changes[index];
        FocusReader();
        PlaceDiff(changes[index]);
    }

    private void UpdateDiffHint(object? sender, PointerEventArgs e) => diffOverlay.UpdateHint(TextContent, e);

    private void ClearDiffHint() => diffOverlay.ClearHint(TextContent);
}

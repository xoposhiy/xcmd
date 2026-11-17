// @implements FollowLog
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class FileViewerWindow
{
    private readonly DispatcherTimer followTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private MenuItem followItem = null!;
    public bool IsFollowing { get; private set; }

    private void ConfigureFollowing()
    {
        followTimer.Tick += async (_, _) =>
        {
            if (!IsFollowing || IsLoading || IsSearching || closed || feedback.IsActive || Commands.IsOpen) return;
            await RunAsync(RefreshTailAsync);
        };
        Closed += (_, _) => { IsFollowing = false; followTimer.Stop(); };
    }

    private void ToggleFollowing()
    {
        if (IsFollowing) { PauseFollowing(); return; }
        if (slice == null || Mode == FileViewMode.Image || ArchivePath.IsEntry(CurrentPath) || IsArchiveReport || IsDocumentReadOnly) return;
        ClearSearch();
        IsFollowing = true; followItem.IsChecked = true; followTimer.Start();
        Run(RefreshTailAsync);
    }

    private void PauseFollowing()
    {
        IsFollowing = false; followTimer.Stop(); followItem.IsChecked = false;
        UpdateReadingPosition();
    }

    private async Task RefreshTailAsync(CancellationToken token)
    {
        var page = await files.ReadAsync(CurrentPath, long.MaxValue, token);
        token.ThrowIfCancellationRequested();
        if (!IsFollowing || closed) return;
        // Comparing the bounded tail also detects truncate/rewrite with the same final length.
        if (VisibleEnd == FileLength && slice != null && slice.Offset == page.Offset && slice.Length == page.Length && slice.Bytes.AsSpan().SequenceEqual(page.Bytes)) return;
        slice = page;
        await RenderPageAsync(token);
        TextContent.CaretIndex = TextContent.Text?.Length ?? 0;
        TextContent.UpdateLayout(); BindScrolling();
        if (textScroll != null) textScroll.Offset = new Vector(0, Math.Max(0, textScroll.Extent.Height - textScroll.Viewport.Height));
        UpdateReadingPosition();
    }
}

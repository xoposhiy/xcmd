// @implements OpenViewers, RestoreWorkspace, ReleaseHandoff
using Avalonia;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

public sealed partial class FileViewer
{
    internal int CurrentIndex => index;
    internal ViewerReadingState CaptureReadingState() => new(automaticMode ? null : Mode, FirstVisibleByte,
        textScroll?.Offset.X ?? 0, selectedEncoding.CodePage, wrapLines, diff != null, IsFollowing, IsInformationVisible)
    {
        DiffPosition = diff == null ? null : DiffTop
    };

    internal async Task PrepareHandoffAsync(ViewerReadingState state)
    {
        var token = CancellationToken.None;
        BeginFileLoad();
        await ReadFileAsync(token);
        await RefreshGitAsync(wantDiff: false, reloadText: false, token);
        var restoreStructured = state.Mode == FileViewMode.Structured;
        Mode = state.Mode switch
        {
            FileViewMode.Structured => Mode,
            FileViewMode.Image when !HasImage => Mode,
            { } mode when !IsArchiveReport => mode,
            _ => Mode
        };
        automaticMode = state.Mode == null || state.Mode != Mode;
        wrapLines = state.Wrap;
        selectedEncoding = EncodingChoice.All.FirstOrDefault(choice => choice.CodePage == state.CodePage) ?? EncodingChoice.All[0];
        information.IsVisible = state.InformationVisible;
        var anchor = Math.Clamp(state.FirstByte, 0, FileLength);
        if (restoreStructured)
        {
            await EnterStructuredAsync(anchor, anchor, anchor, token);
            if (structuredReader.IsActive) return;
        }
        if (state.Diff && CanShowDiff(gitFile) && await TryShowDiffAsync(gitFile!, anchor, token)) return;
        if (Mode == FileViewMode.Image) { await RenderPageAsync(token); return; }
        var offset = Math.Max(0, anchor - FileSlice.PageSize / 4) / 4 * 4;
        await LoadPageAsync(offset, token);
    }

    internal void RestoreReadingPosition(ViewerReadingState state)
    {
        if (Mode == FileViewMode.Image) return;
        if (diff != null) PlaceDiff(diff.RestorePosition(state.FirstByte, state.DiffPosition), force: true);
        else RestoreTopByte(state.FirstByte);
        if (textScroll != null) textScroll.Offset = new Vector(state.ScrollX, textScroll.Offset.Y);
        if (state.Follow) ToggleFollowing();
    }
}

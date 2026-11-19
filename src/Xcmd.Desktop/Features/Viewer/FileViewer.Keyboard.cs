// @implements OpenViewers, QuickViewFile, ChooseViewMode, ReadLargeFiles, FindViewerText, FollowLog, OpenViewedFile, ReadWorkingFileDiff, ReportHighlighting
using Avalonia.Input;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

public sealed partial class FileViewer
{
    private bool editKeyHeld;

    private void OnViewerKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F4) editKeyHeld = false;
        if (e.Key is not (Key.LeftAlt or Key.RightAlt)) return;
        altSearchArmed = false;
        e.Handled = true;
    }

    internal void ResetKeys() { editKeyHeld = false; diffKeyHeld = false; altSearchArmed = false; suppressSearchText = false; }

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
        {
            var delta = e.Key == Key.PageDown ? FileSlice.PageSize : -FileSlice.PageSize;
            if (diff == null) Run(token => LoadPageAsync(ByteOffset + delta, token));
            else PlaceDiff(DiffTop + delta);
            return true;
        }
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

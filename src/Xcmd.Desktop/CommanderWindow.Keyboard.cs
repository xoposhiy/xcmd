// @implements NavigatePanels, OpenOtherPanel, SortEntries, RecentLocations, FavoriteLocations, RefreshEntries, MarkEntries, RenameEntry, CreateFolder, CreateFile, TransferEntries, DuplicateEntry, ClipboardTransfer, DeleteEntries, SearchResultPanel, OpenViewers, QuickViewFile, BrowseArchives, ExtractEntries, OpenExternalTool, RunCommand, ShellActions, ComparePanelFiles, BrowseHistory
using Avalonia.Input;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    // Enter can move focus to the file panel before the key is released.
    private readonly HashSet<Key> launchKeysHeld = [];
    private bool HasGitActionsFocus => Left.Git.ActionsButton.IsKeyboardFocusWithin || Right.Git.ActionsButton.IsKeyboardFocusWithin;

    private bool HandlePanelSwitch(KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift) || ActivePanel.EditingRow != null) return false;
        ResetQuickJump();
        CommandLine.Completions.Close();
        Activate(OppositePanel);
        ActivePanel.FocusCurrent();
        e.Handled = true;
        return true;
    }

    private bool HandleFavoritesShortcut(KeyEventArgs e)
    {
        if (e.Key != Key.D || e.KeyModifiers != KeyModifiers.Control) return false;
        OpenNavigation(ActivePanel, DirectoryList.Favorites);
        return true;
    }

    private void EscapeCommandLine()
    {
        if (ActivePanel.HasHistory && string.IsNullOrEmpty(CommandLine.Editor.Text))
        { Activate(ActivePanel); Execute(ActivePanel.ExitHistoryAsync); }
        else if (ActivePanel.IsSearchResults && string.IsNullOrEmpty(CommandLine.Editor.Text))
        { Activate(ActivePanel); Execute(ActivePanel.ExitSearchResultsAsync); }
        else if (!CancelOpeningArchives()) { CommandLine.Clear(); SetError(null); Activate(ActivePanel); ActivePanel.ClearMarks(); }
    }

    private void HandleRenameKey(KeyEventArgs e)
    {
        if (ActivePanel.IsCommitting) { e.Handled = true; return; }
        if (e.Key == Key.Escape)
        {
            ActivePanel.CancelRename(); ActivePanel.FocusCurrent(); SetError(null); e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Tab)
        {
            e.Handled = true;
            var panel = ActivePanel;
            var switchPanel = e.Key == Key.Tab;
            Execute(() => CommitRenameAsync(panel, switchPanel));
        }
    }

    private async Task CommitRenameAsync(PanelView panel, bool switchPanel)
    {
        var failure = await panel.CommitRenameAsync();
        SetError(null);
        if (failure != null)
            decisions.ShowRenameError(failure, panel, CompleteRename);
        else CompleteRename();

        void CompleteRename()
        {
            if (ActivePanel != panel) return;
            if (switchPanel) Activate(OppositePanel);
            ActivePanel.FocusCurrent();
        }
    }

    private bool HandlePanelShortcut(KeyEventArgs e) => HandleDirectoryDestinationKey(e) ||
        commandLineController.HandlePanelShortcut(e) || HandlePanelEscape(e);

    private bool HandlePanelEscape(KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None || directorySizes != null || nameJump.Active) return false;
        if (ActivePanel.IsSearchResults) Execute(ActivePanel.ExitSearchResultsAsync);
        else if (!CancelOpeningArchives()) { CommandLine.Clear(); SetError(null); ActivePanel.ClearMarks(); }
        return true;
    }

    private bool HandleDirectoryDestinationKey(KeyEventArgs e)
    {
        if (e.KeyModifiers is not (KeyModifiers.Control or (KeyModifiers.Control | KeyModifiers.Shift))) return false;
        string? destination = null;
        if (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.OemBackslash || e.PhysicalKey == PhysicalKey.Backslash))
            destination = Path.GetPathRoot(ArchivePath.PhysicalFile(ActivePanel.Session.DirectoryPath));
        else if (e.Key == Key.OemTilde || e.PhysicalKey == PhysicalKey.Backquote || e.KeySymbol is "`" or "~" or "ё" or "Ё")
            destination = PanelView.HomeDirectory;
        return OpenDirectoryDestination(destination);
    }

    private bool OpenDirectoryDestination(string? destination)
    {
        if (string.IsNullOrEmpty(destination)) return false;
        var panel = ActivePanel;
        ResetQuickJump();
        Execute(async () => { if (await panel.LoadAsync(destination)) panel.FocusCurrent(); else SetError(panel.Session.Error); });
        return true;
    }

    private bool HandlePanelCommand(KeyEventArgs e, bool ctrl, bool shift)
    {
        if (e.Key == Key.F4 && e.KeyModifiers == KeyModifiers.Shift) { OpenCreateFileDialog(); return true; }
        if (HandleComparisonKey(e) || HandleClipboardKey(e) || HandleShellKey(e) || HandleViewOrEditKey(e) || HandleDirectorySizeShortcut(e, shift)) return true;
        if (e.Key == Key.Tab)
        {
            Activate(OppositePanel);
            ActivePanel.FocusCurrent();
            return true;
        }
        if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Down)
        {
            OpenNavigation(ActivePanel, DirectoryList.History);
            return true;
        }
        if (shift && e.Key == Key.F6)
        {
            RenameCurrent();
            return true;
        }
        return ctrl && HandleControlPanelCommand(e.Key);
    }

    private bool HandleShellKey(KeyEventArgs e)
    {
        var menuKey = e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift || e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Control;
        if (menuKey && ActivePanel.Table.IsKeyboardFocusWithin)
        {
            if (launchKeysHeld.Add(e.Key)) shell.OpenCurrentMenu();
            return true;
        }
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.Alt) return false;
        if (launchKeysHeld.Add(e.Key)) shell.OpenFileProperties();
        return true;
    }

    private bool HandleComparisonKey(KeyEventArgs e)
    {
        if (!ActivePanel.Table.IsKeyboardFocusWithin || e.Key != Key.C || e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Shift)) return false;
        if (launchKeysHeld.Add(e.Key)) Execute(viewing.CompareCurrentFilesAsync);
        ResetQuickJump();
        return true;
    }

    private bool HandleViewOrEditKey(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.F3 or Key.F4)) return false;
        if (e.Key == Key.F3) viewing.ViewCurrent();
        else if (launchKeysHeld.Add(e.Key)) Execute(viewing.EditCurrentAsync);
        return true;
    }

    private bool HandleQuickViewKey(KeyEventArgs e)
    {
        if (HandleQuickViewToggleKey(e)) return true;
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None || nameJump.Active || directorySizes != null || !viewing.IsQuickViewOpen) return false;
        return CancelOpeningArchives() || viewing.CloseQuickView();
    }

    private bool HandleQuickViewToggleKey(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control || e.Key != Key.Q && e.PhysicalKey != PhysicalKey.Q) return false;
        ResetQuickJump();
        if (launchKeysHeld.Add(e.Key)) viewing.ToggleQuickView();
        return true;
    }

    private bool HandleQuickViewFocusKey(KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.KeyModifiers != KeyModifiers.Control || !viewing.IsQuickViewOpen || ActivePanel.EditingRow != null) return false;
        ResetQuickJump();
        CommandLine.Completions.Close();
        if (launchKeysHeld.Add(e.Key)) viewing.SwitchQuickViewFocus();
        return true;
    }

    private bool RouteQuickViewInput(KeyEventArgs e)
    {
        if (HandleQuickViewFocusKey(e)) { e.Handled = true; return true; }
        if (!viewing.IsQuickViewFocused) return false;
        e.Handled = HandleQuickViewToggleKey(e);
        return true;
    }

    private bool HandleClipboardKey(KeyEventArgs e)
    {
        if (!ActivePanel.Table.IsKeyboardFocusWithin) return false;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.C or Key.X or Key.V)
        {
            if (e.Key == Key.V) panelClipboard.Paste();
            else panelClipboard.Copy(e.Key == Key.X);
        }
        else return false;
        ResetQuickJump();
        return true;
    }

    private bool HandleControlPanelCommand(Key key)
    {
        switch (key)
        {
            case Key.D:
                OpenNavigation(ActivePanel, DirectoryList.Favorites);
                break;
            case Key.Left or Key.Right:
                OpenInPanel(key == Key.Left ? Left : Right);
                break;
            case Key.R:
                ReloadActivePanel();
                break;
            case Key.PageDown:
                Execute(() => OpenArchiveAsync(ActivePanel));
                break;
            default:
                return false;
        }
        return true;
    }

    private void OpenInPanel(PanelView destination)
    {
        var source = ActivePanel;
        if (source == destination) return;
        Execute(async () =>
        {
            var entry = source.Current?.Entry;
            if (entry is { IsDirectory: false } && ArchivePath.OpensOnEnter(entry.FullPath))
            {
                await archives.OpenAsync(destination, entry);
                return;
            }
            var locate = source.IsSearchResults && entry != null;
            var directory = locate ? ArchivePath.Parent(entry!.FullPath)! : entry?.IsDirectory == true ? entry.FullPath : source.Session.DirectoryPath;
            if (!await destination.LoadAsync(directory, selectName: locate ? entry!.Name : null)) SetError(destination.Session.Error);
            source.FocusCurrent();
        });
    }

    private void ReloadActivePanel()
    {
        var panel = ActivePanel;
        Execute(async () =>
        {
            if (panel.IsSearchResults) { await panel.RefreshAsync(() => true, () => ActivePanel == panel); panel.FocusCurrent(); return; }
            await panel.LoadAsync(panel.Session.DirectoryPath, preserveSelection: true);
            if (ActivePanel == panel) panel.FocusCurrent();
        });
    }

    private bool HandlePanelNavigation(KeyEventArgs e, bool ctrl, bool shift)
    {
        if (!ctrl && e.Key is Key.Up or Key.Down) ActivePanel.MoveCursor(e.Key == Key.Down, shift);
        else if (e.Key is Key.Home or Key.End) ActivePanel.SelectBoundary(e.Key == Key.End, shift);
        else if (!ctrl && e.Key is (Key.PageUp or Key.PageDown)) ActivePanel.MovePage(e.Key == Key.PageDown, shift);
        else if (ctrl && e.Key is Key.F3 or Key.F4 or Key.F5 or Key.F6)
        {
            ActivePanel.SortBy(e.Key switch { Key.F3 => SortField.Name, Key.F4 => SortField.Extension, Key.F5 => SortField.Modified, _ => SortField.Size });
            ActivePanel.FocusCurrent();
        }
        else if (e.Key == Key.Enter) { if (launchKeysHeld.Add(e.Key)) Execute(() => OpenCurrentAsync(ActivePanel)); }
        else if (e.Key == Key.Back || (ctrl && e.Key == Key.PageUp)) OpenParent();
        else if (e.Key == Key.F7) OpenCreateDirectoryDialog();
        else return HandleFileOperationKey(e);
        return true;
    }

    private bool HandleFileOperationKey(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.F5 or Key.F6 or Key.F8 or Key.Delete)
        {
            if (launchKeysHeld.Add(e.Key))
            {
                if (e.Key == Key.F5) CopySelected();
                else if (e.Key == Key.F6) TransferSelected(FileOperationKind.Move);
                else DeleteSelected(false);
            }
        }
        else if (e.Key is Key.Delete or Key.F8 && e.KeyModifiers == KeyModifiers.Shift)
        { if (launchKeysHeld.Add(e.Key)) DeleteSelected(true); }
        else if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.Shift)
        { if (launchKeysHeld.Add(e.Key)) TransferSelected(FileOperationKind.Copy, samePanel: true); }
        else return false;
        return true;
    }
}

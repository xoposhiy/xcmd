using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    private Menu BuildMenu()
    {
        MenuItem Item(string title, Action action, KeyGesture? gesture = null, bool historyAllowed = false)
        {
            var item = new MenuItem { Header = title, InputGesture = gesture }.WithFeedbackId("CommanderWindow.Menu.item");
            item.Click += (_, _) =>
            {
                ResetQuickJump();
                if (ActivePanel.HasHistory && !historyAllowed) { SetError("Git history is read-only. Close the log to use this command."); return; }
                action();
            };
            GitHistory.RegisterMenuItem(item, historyAllowed);
            return item;
        }
        MenuItem Sort(string title, SortField field)
        {
            var item = Item(title, () => { ActivePanel.SortBy(field); ActivePanel.FocusCurrent(); }, historyAllowed: true);
            GitHistory.MarkSort(item); return item;
        }
        MenuItem Changes()
        {
            var item = Item("Changed files in current commit", GitHistory.ShowChanges, historyAllowed: true);
            GitHistory.MarkChanges(item); return item;
        }
        return new Menu
        {
            Name = "MainMenu",
            ItemsSource = new MenuItem[]
            {
                new MenuItem() { Header = "File", ItemsSource = new MenuItem[]
                {
                    Item("View file or commit", viewing.ViewCurrent, new(Key.F3), historyAllowed: true),
                    Item("Find files", FileSearch.Open, new(Key.F7, KeyModifiers.Alt)),
                    Item("Edit in Notepad++", () => Execute(viewing.EditCurrentAsync), new(Key.F4)),
                    Item("New file and edit", OpenCreateFileDialog, new(Key.F4, KeyModifiers.Shift)),
                    Item("New folder", OpenCreateDirectoryDialog, new(Key.F7)),
                    Item("Rename", RenameCurrent, new(Key.F6, KeyModifiers.Shift)),
                    Item("Copy", () => panelClipboard.Copy(false), new(Key.C, KeyModifiers.Control)),
                    Item("Cut", () => panelClipboard.Copy(true), new(Key.X, KeyModifiers.Control)),
                    Item("Paste into current folder", panelClipboard.Paste, new(Key.V, KeyModifiers.Control)),
                    Item("Copy full path", panelClipboard.CopyPath, new(Key.C, KeyModifiers.Control | KeyModifiers.Shift)),
                    Item("Copy to other panel", CopySelected, new(Key.F5)),
                    Item("Copy in this panel", () => TransferSelected(FileOperationKind.Copy, samePanel: true), new(Key.F5, KeyModifiers.Shift)),
                    Item("Move to other panel", () => TransferSelected(FileOperationKind.Move), new(Key.F6)),
                    Item("Move to Recycle Bin", () => DeleteSelected(false), new(Key.F8)),
                    Item("Delete permanently", () => DeleteSelected(true), new(Key.Delete, KeyModifiers.Shift)),
                    Item("Open", () => Execute(() => OpenCurrentAsync(ActivePanel)), new(Key.Enter), historyAllowed: true),
                    Item("Open with Windows", OpenWithWindows), Item("Exit", Close, historyAllowed: true)
                } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu"),
                new MenuItem() { Header = "Navigate", ItemsSource = new MenuItem[]
                {
                    Item("Parent directory", OpenParent, new(Key.PageUp, KeyModifiers.Control), historyAllowed: true),
                    Item("Home folder", () => OpenDirectoryDestination(PanelView.HomeDirectory), new(Key.OemTilde, KeyModifiers.Control)),
                    Item("Drive root", () => OpenDirectoryDestination(Path.GetPathRoot(ArchivePath.PhysicalFile(ActivePanel.Session.DirectoryPath))), new(Key.OemBackslash, KeyModifiers.Control)),
                    Item("Open as archive", () => Execute(() => OpenArchiveAsync(ActivePanel)), new(Key.PageDown, KeyModifiers.Control)),
                    Item("Go to directory", () => OpenNavigation(ActivePanel, DirectoryList.History), new(Key.Down, KeyModifiers.Alt)),
                    Item("Favorite directories", () => OpenNavigation(ActivePanel, DirectoryList.Favorites), new(Key.D, KeyModifiers.Control)),
                    Item("Switch panel", () => { Activate(OppositePanel); ActivePanel.FocusCurrent(); }, new(Key.Tab), historyAllowed: true),
                    Item("Focus command line", () => CommandLine.FocusCommand(), new(Key.L, KeyModifiers.Control), historyAllowed: true)
                } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu.2"),
                new MenuItem() { Header = "Sort", ItemsSource = new[] { Sort("Name", SortField.Name), Sort("Type", SortField.Extension), Sort("Size", SortField.Size), Sort("Modified", SortField.Modified) } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu.3"),
                new MenuItem() { Header = "View", ItemsSource = new[]
                {
                    Item("Reset column widths", () => ActivePanel.ResetColumns(), historyAllowed: true), Item("Reload file colors", () => Execute(ReloadFileTypesAsync)),
                    Item("Calculate folder sizes", () => Execute(CalculateDirectorySizesAsync), new(Key.Enter, KeyModifiers.Alt | KeyModifiers.Shift)),
                    Item("File operations", () => OperationsView.ToggleDetails())
                } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu.4"),
                new MenuItem() { Header = "Git", ItemsSource = new MenuItem[]
                {
                    Item("Log of selected file or folder", () => GitHistory.Open(selectedItem: true), new(Key.L, KeyModifiers.Control | KeyModifiers.Alt), historyAllowed: true),
                    Item("Log of current folder", () => GitHistory.Open(selectedItem: false), historyAllowed: true),
                    Changes(),
                    Item("Diff selected file", GitHistory.DiffCurrent, new(Key.D, KeyModifiers.Control | KeyModifiers.Alt), historyAllowed: true)
                } }.WithFeedbackId("CommanderWindow.Menu.git"),
                new MenuItem() { Header = "Help", ItemsSource = new MenuItem[]
                {
                    Item("Comment on an element", () => Feedback.Begin(), new(Key.F12), historyAllowed: true),
                    Item("Refinement list", () => Feedback.ShowNotes(), historyAllowed: true)
                } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu.5")
            }
        }.WithFeedbackId("CommanderWindow.Menu.BuildMenu.6");
    }

    private void OpenParent()
    {
        var panel = ActivePanel;
        Execute(() => OpenParentAsync(panel));
    }

    private static async Task OpenParentAsync(PanelView panel)
    {
        if (panel.HasHistory) { await panel.BackHistoryAsync(); return; }
        if (panel.IsSearchResults) { await panel.ExitSearchResultsAsync(); return; }
        var directory = panel.Session.DirectoryPath;
        var parent = PanelRules.ParentEntry(directory);
        if (parent == null) return;
        await panel.LoadAsync(parent.FullPath, selectName: PanelRules.ParentCursorName(directory));
        panel.FocusCurrent();
    }

    private void CopySelected()
    {
        TransferSelected(FileOperationKind.Copy);
    }
}

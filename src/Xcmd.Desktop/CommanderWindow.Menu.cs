using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    private Menu BuildMenu()
    {
        MenuItem Item(string title, Action action, KeyGesture? gesture = null)
        {
            var item = new MenuItem { Header = title, InputGesture = gesture }.WithFeedbackId("CommanderWindow.Menu.item");
            item.Click += (_, _) => { ResetQuickJump(); action(); };
            return item;
        }
        MenuItem Sort(string title, SortField field) => Item(title, () => { ActivePanel.SortBy(field); ActivePanel.FocusCurrent(); });
        return new Menu
        {
            Name = "MainMenu",
            ItemsSource = new MenuItem[]
            {
                new MenuItem() { Header = "File", ItemsSource = new MenuItem[]
                {
                    Item("View file", viewing.ViewCurrent, new(Key.F3)),
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
                    Item("Open", () => Execute(() => OpenCurrentAsync(ActivePanel)), new(Key.Enter)),
                    Item("Open with Windows", OpenWithWindows), Item("Exit", Close)
                } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu"),
                new MenuItem() { Header = "Navigate", ItemsSource = new MenuItem[]
                {
                    Item("Parent directory", OpenParent, new(Key.PageUp, KeyModifiers.Control)),
                    Item("Home folder", () => OpenDirectoryDestination(PanelView.HomeDirectory), new(Key.OemTilde, KeyModifiers.Control)),
                    Item("Drive root", () => OpenDirectoryDestination(Path.GetPathRoot(ArchivePath.PhysicalFile(ActivePanel.Session.DirectoryPath))), new(Key.OemBackslash, KeyModifiers.Control)),
                    Item("Open as archive", () => Execute(() => OpenArchiveAsync(ActivePanel)), new(Key.PageDown, KeyModifiers.Control)),
                    Item("Go to directory", () => OpenNavigation(ActivePanel, DirectoryList.History), new(Key.Down, KeyModifiers.Alt)),
                    Item("Favorite directories", () => OpenNavigation(ActivePanel, DirectoryList.Favorites), new(Key.D, KeyModifiers.Control)),
                    Item("Switch panel", () => { Activate(OppositePanel); ActivePanel.FocusCurrent(); }, new(Key.Tab)),
                    Item("Focus command line", () => CommandLine.FocusCommand(), new(Key.L, KeyModifiers.Control))
                } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu.2"),
                new MenuItem() { Header = "Sort", ItemsSource = new[] { Sort("Name", SortField.Name), Sort("Type", SortField.Extension), Sort("Size", SortField.Size), Sort("Modified", SortField.Modified) } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu.3"),
                new MenuItem() { Header = "View", ItemsSource = new[]
                {
                    Item("Reset column widths", () => ActivePanel.ResetColumns()), Item("Reload file colors", () => Execute(ReloadFileTypesAsync)),
                    Item("Calculate folder sizes", () => Execute(CalculateDirectorySizesAsync), new(Key.Enter, KeyModifiers.Alt | KeyModifiers.Shift)),
                    Item("File operations", () => OperationsView.ToggleDetails())
                } }.WithFeedbackId("CommanderWindow.Menu.BuildMenu.4"),
                new MenuItem() { Header = "Help", ItemsSource = new MenuItem[]
                {
                    Item("Comment on an element", () => Feedback.Begin(), new(Key.F12)),
                    Item("Refinement list", () => Feedback.ShowNotes())
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

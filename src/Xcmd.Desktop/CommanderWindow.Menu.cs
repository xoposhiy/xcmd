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
            var item = new MenuItem { Header = title, InputGesture = gesture };
            item.Click += (_, _) => { ResetQuickJump(); action(); };
            return item;
        }
        MenuItem Sort(string title, SortField field) => Item(title, () => { ActivePanel.SortBy(field); ActivePanel.FocusCurrent(); });
        return new Menu
        {
            Name = "MainMenu",
            ItemsSource = new MenuItem[]
            {
                new() { Header = "File", ItemsSource = new MenuItem[]
                {
                    Item("View file", viewing.ViewCurrent, new(Key.F3)),
                    Item("Edit in Notepad++", () => Execute(viewing.EditCurrentAsync), new(Key.F4)),
                    Item("New folder", OpenCreateDirectoryDialog, new(Key.F7)),
                    Item("Rename", RenameCurrent, new(Key.F6, KeyModifiers.Shift)),
                    Item("Copy to other panel", CopySelected, new(Key.F5)),
                    Item("Copy in this panel", () => TransferSelected(FileOperationKind.Copy, samePanel: true), new(Key.F5, KeyModifiers.Shift)),
                    Item("Move to other panel", () => TransferSelected(FileOperationKind.Move), new(Key.F6)),
                    Item("Move to Recycle Bin", () => DeleteSelected(false), new(Key.F8)),
                    Item("Delete permanently", () => DeleteSelected(true), new(Key.Delete, KeyModifiers.Shift)),
                    Item("Open", () => Execute(() => OpenCurrentAsync(ActivePanel)), new(Key.Enter)),
                    Item("Open with Windows", OpenWithWindows), Item("Exit", Close)
                } },
                new() { Header = "Navigate", ItemsSource = new MenuItem[]
                {
                    Item("Parent directory", OpenParent, new(Key.PageUp, KeyModifiers.Control)),
                    Item("Open as archive", () => Execute(() => OpenArchiveAsync(ActivePanel)), new(Key.PageDown, KeyModifiers.Control)),
                    Item("Go to directory", () => OpenNavigation(ActivePanel, DirectoryList.History), new(Key.Down, KeyModifiers.Alt)),
                    Item("Favorite directories", () => OpenNavigation(ActivePanel, DirectoryList.Favorites), new(Key.D, KeyModifiers.Control)),
                    Item("Switch panel", () => { Activate(OppositePanel); ActivePanel.FocusCurrent(); }, new(Key.Tab)),
                    Item("Focus command line", () => CommandLine.FocusCommand(), new(Key.L, KeyModifiers.Control))
                } },
                new() { Header = "Sort", ItemsSource = new[] { Sort("Name", SortField.Name), Sort("Type", SortField.Extension), Sort("Size", SortField.Size), Sort("Modified", SortField.Modified) } },
                new() { Header = "View", ItemsSource = new[]
                {
                    Item("Reset column widths", () => ActivePanel.ResetColumns()), Item("Reload file colors", () => Execute(ReloadFileTypesAsync)),
                    Item("Calculate folder sizes", () => Execute(CalculateDirectorySizesAsync), new(Key.Enter, KeyModifiers.Alt | KeyModifiers.Shift)),
                    Item("File operations", () => OperationsView.ToggleDetails())
                } },
                new() { Header = "Help", ItemsSource = new MenuItem[]
                {
                    Item("Comment on an element", () => Feedback.Begin(), new(Key.F12)),
                    Item("Refinement list", () => Feedback.ShowNotes())
                } }
            }
        };
    }

    private void OpenParent()
    {
        var panel = ActivePanel;
        var parent = PanelRules.ParentEntry(panel.Session.DirectoryPath);
        if (parent != null) Execute(async () => { await panel.LoadAsync(parent.FullPath); panel.FocusCurrent(); });
    }

    private void CopySelected()
    {
        TransferSelected(FileOperationKind.Copy);
    }
}

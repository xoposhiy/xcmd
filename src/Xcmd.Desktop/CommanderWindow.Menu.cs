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
                    Item("New folder", OpenCreateDirectoryDialog, new(Key.F7)),
                    Item("Rename", () => ActivePanel.BeginRename(), new(Key.F6, KeyModifiers.Shift)),
                    Item("Copy to other panel", CopySelected, new(Key.F5)), Item("Open", () => Execute(() => OpenCurrentAsync(ActivePanel)), new(Key.Enter)), Item("Exit", Close)
                } },
                new() { Header = "Navigate", ItemsSource = new MenuItem[]
                {
                    Item("Parent directory", OpenParent, new(Key.PageUp, KeyModifiers.Control)),
                    Item("Go to directory", () => OpenNavigation(ActivePanel, DirectoryList.History), new(Key.Down, KeyModifiers.Alt)),
                    Item("Favorite directories", () => OpenNavigation(ActivePanel, DirectoryList.Favorites), new(Key.D, KeyModifiers.Control)),
                    Item("Switch panel", () => { Activate(OppositePanel); ActivePanel.FocusCurrent(); }, new(Key.Tab)),
                    Item("Focus command line", () => CommandLine.FocusCommand(), new(Key.L, KeyModifiers.Control))
                } },
                new() { Header = "Sort", ItemsSource = new[] { Sort("Name", SortField.Name), Sort("Type", SortField.Extension), Sort("Size", SortField.Size), Sort("Modified", SortField.Modified) } },
                new() { Header = "View", ItemsSource = new[]
                {
                    Item("Reset column widths", () => ActivePanel.ResetColumns()), Item("Reload file colors", () => Execute(ReloadFileTypesAsync)),
                    Item("Calculate folder sizes", () => Execute(CalculateDirectorySizesAsync), new(Key.Enter, KeyModifiers.Alt | KeyModifiers.Shift))
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
        var source = ActivePanel;
        var destination = OppositePanel;
        var destinationPath = destination.Session.DirectoryPath;
        var selected = source.Table.SelectedItems!.OfType<FileRow>().Where(row => !row.Entry.IsParent).Select(row => row.Entry).ToArray();
        Execute(async () =>
        {
            foreach (var entry in selected) await source.Session.FileSystem.CopyFileAsync(entry, destinationPath);
            if (destination.Session.DirectoryPath == destinationPath) await destination.LoadAsync(destinationPath, preserveSelection: true);
            if (ActivePanel == source) source.FocusCurrent();
        });
    }
}

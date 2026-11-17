// @tests NavigatePanels, RecentLocations, RenameEntry, AsyncCoordination
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class HistoryScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("history-parent-selects-exited-folder", "UC15; Parent navigation selects the exited folder after opening it from history", async c =>
        {
            foreach (var key in new[] { Key.PageUp, Key.Back, Key.Enter })
            {
                await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "folder1"));
                await c.SelectAsync("inside.txt");
                await c.Window.Left.LoadAsync(c.LeftPath);
                await c.SelectAsync("folder2");
                await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
                var dialog = c.Window.ActiveNavigationDialog!;
                Equal(Path.Combine(c.LeftPath, "folder1"), dialog.Directories.SelectedItem, "History offers the folder with its own remembered cursor");
                await c.KeyAsync(Key.Enter, target: dialog);
                Equal("inside.txt", c.Window.Left.Current?.Name, "History restores the child folder's cursor");
                if (key == Key.Enter) await c.SelectAsync("..");
                await c.KeyAsync(key, key == Key.PageUp ? RawInputModifiers.Control : RawInputModifiers.None);
                Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "The parent command leaves the child folder");
                Equal("folder1", c.Window.Left.Current?.Name, "Parent navigation selects the exited folder instead of the parent's old cursor");
                await c.KeyAsync(Key.Enter);
                Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "Enter reopens the same folder after going up");
                Check(c.Window.Left.Table.IsKeyboardFocusWithin, "The round trip keeps keyboard focus in the panel");
            }
        }),
        new("history-recent-unique-and-bounded", "UC15", c =>
        {
            var history = new DirectoryHistory();
            history.Visit(@"C:\work");
            history.Visit(@"C:\work\project");
            history.Visit(@"c:\WORK");
            Equal(1, history.PreviousDirectories.Count, "Revisiting a path does not create a duplicate");
            Equal(@"C:\work\project", history.PreviousDirectories[0], "The current path is excluded");
            for (var i = 0; i < 120; i++) history.Visit(@"C:\work\project-" + i);
            Equal(99, history.PreviousDirectories.Count, "History keeps 100 locations including the current one");
            Equal(@"C:\work\project-118", history.PreviousDirectories[0], "Previous locations start with the most recent");
            return Task.CompletedTask;
        }),
        new("history-keyboard-restores-cursor", "UC15; UC20", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Tab);
            await c.SelectAsync("destination.txt");
            await c.KeyAsync(Key.Tab);
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            Equal(c.LeftPath, dialog.Directories.SelectedItem, "The most recent previous directory is selected");
            Check(dialog.Directories.IsKeyboardFocusWithin, "History is ready for keyboard navigation.");
            Equal("..", c.Window.Left.Current?.Name, "Alt+Down does not move the file cursor");
            await c.ScreenshotAsync("history", dialog);
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Enter returns to the selected directory");
            Equal("folder1", c.Window.Left.Current?.Name, "Returning restores the last directory cursor");
            Check(c.Window.ActiveNavigationDialog == null && c.Window.Left.Table.IsKeyboardFocusWithin, "History closes and restores panel focus.");
            Equal("destination.txt", c.Window.Right.Current?.Name, "The other panel keeps its cursor");
            await c.ScreenshotAsync("returned-to-directory");
        }),
        new("history-select-older-directory", "UC15", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            await c.SelectAsync("inside.txt");
            await c.KeyAsync(Key.Back);
            await c.SelectAsync("folder2");
            await c.KeyAsync(Key.Enter);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            await c.KeyAsync(Key.Down, target: dialog);
            Equal(Path.Combine(c.LeftPath, "folder1"), dialog.Directories.SelectedItem, "Down selects an older directory");
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "The older directory opens");
            Equal("inside.txt", c.Window.Left.Current?.Name, "Each directory retains its own cursor");
        }),
        new("history-cancel-preserves-selection", "UC15; UC20", async c =>
        {
            await c.ClickAsync(c.Row("alpha.txt"));
            await c.ClickAsync(c.Row("file2.log"), RawInputModifiers.Control);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            await c.KeyAsync(Key.Escape, target: c.Window.ActiveNavigationDialog);
            Equal("file2.log", c.Window.Left.Current?.Name, "Cancel preserves the file cursor");
            Check(c.Window.Left.SelectedNames().Order().SequenceEqual(new[] { "alpha.txt", "file2.log" }), "Cancel preserves multiple selected files.");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Cancel restores panel focus.");
        }),
        new("history-cancel-button-keyboard", "UC15", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            await c.KeyAsync(Key.Tab, target: dialog);
            Check(dialog.Favorites.IsKeyboardFocusWithin, "Tab reaches the combined favorites and frequent list.");
            var cancel = dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "NavigationCancel");
            for (var i = 0; i < 8 && !cancel.IsKeyboardFocusWithin; i++) await c.KeyAsync(Key.Tab, target: dialog);
            Check(cancel.IsKeyboardFocusWithin, "Tab reaches the Cancel button.");
            await c.KeyAsync(Key.Enter, target: dialog);
            Check(c.Window.ActiveNavigationDialog == null, "Enter on Cancel closes history.");
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "Cancel never opens the selected history directory");
        }),
        new("history-empty-and-refresh", "UC15", async c =>
        {
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            await c.KeyAsync(Key.F5, RawInputModifiers.Control);
            Equal(0, c.Window.Left.Session.PreviousDirectories.Count, "Refresh and sorting do not create previous directories");
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            Equal(0, dialog.Directories.ItemCount, "The initial history is empty");
            await c.KeyAsync(Key.Enter, target: dialog);
            Check(c.Window.ActiveNavigationDialog == dialog, "Enter on empty history is harmless.");
            await c.ScreenshotAsync("empty-history", dialog);
            await c.KeyAsync(Key.Escape, target: dialog);
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Empty history can be dismissed.");
        }),
        new("history-mouse-uses-source-panel", "UC15; UC20", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            var button = c.Window.Right.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "rightHistory");
            await c.ClickAsync(button);
            var dialog = c.Window.ActiveNavigationDialog!;
            Equal("right", c.Window.ActivePanel.Id, "The history button activates its panel");
            Equal(c.RightPath, dialog.Directories.SelectedItem, "Each panel has separate history");
            var row = (Control)dialog.Directories.ContainerFromIndex(0)!;
            await c.ClickAsync(row, target: dialog);
            await c.ClickAsync(row, target: dialog);
            Equal(c.RightPath, c.Window.Right.Session.DirectoryPath, "Double-click opens history in the source panel");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "The other panel keeps its directory");
            Check(c.Window.Right.Table.IsKeyboardFocusWithin, "The source panel regains focus.");
        }),
        new("history-missing-directory-and-retry", "UC15; missing directories open the nearest existing parent", async c =>
        {
            await c.SelectAsync("folder2");
            await c.KeyAsync(Key.Enter);
            await c.KeyAsync(Key.Back);
            Directory.Delete(Path.Combine(c.LeftPath, "folder2"));
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            await c.KeyAsync(Key.Enter, target: dialog);
            Check(c.Window.ActiveNavigationDialog == null && dialog.Error == null, "A missing directory opens its existing parent without an error.");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "The nearest existing parent opens");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "The source panel receives focus.");
            Directory.CreateDirectory(Path.Combine(c.LeftPath, "folder2"));
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            dialog = c.Window.ActiveNavigationDialog!;
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal(Path.Combine(c.LeftPath, "folder2"), c.Window.Left.Session.DirectoryPath, "History keeps the original path for a later retry");
            Check(c.Window.ActiveNavigationDialog == null, "Successful retry closes history.");
        }),
        new("history-failed-and-obsolete-reads-excluded", "UC15; development requirements", async c =>
        {
            var panel = c.Window.Left;
            await panel.LoadAsync(Path.Combine(c.LeftPath, "missing"));
            Equal(0, panel.Session.PreviousDirectories.Count, "A failed read does not enter history");
            var gate = c.FileSystem.PauseNextRead(Path.Combine(c.LeftPath, "folder1"));
            var obsolete = panel.LoadAsync(gate.Path);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await panel.LoadAsync(Path.Combine(c.LeftPath, "folder2"));
            await obsolete;
            Check(gate.WasCancelled, "A new navigation cancels the old read.");
            Check(panel.Session.PreviousDirectories.SequenceEqual(new[] { c.LeftPath }), "Only successful navigation enters history.");
        }),
        new("history-cancel-pending-navigation", "UC15; development requirements", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            var gate = c.FileSystem.PauseNextRead(c.LeftPath);
            c.PressKey(Key.Enter, target: dialog);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(dialog.IsOpening && !dialog.Directories.IsEnabled, "Pending navigation has visible state and prevents repeated submission.");
            c.PressKey(Key.Enter, target: dialog);
            c.PressKey(Key.Escape, target: dialog);
            await c.SettleAsync();
            Check(gate.WasCancelled && c.Window.ActiveNavigationDialog == null, "Escape closes history and cancels its pending read.");
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "Cancelled history navigation cannot change the directory later");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Cancelled pending navigation restores panel focus.");
        }),
        new("history-pending-navigation-completes-once", "UC15; development requirements", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            var gate = c.FileSystem.PauseNextRead(c.LeftPath);
            c.PressKey(Key.Enter, target: dialog);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            c.PressKey(Key.Enter, target: dialog);
            gate.Release.TrySetResult();
            await c.SettleAsync();
            Check(!gate.WasCancelled && c.Window.ActiveNavigationDialog == null, "Repeated Enter keeps the original navigation and closes history on success.");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "The selected directory opens after its read completes");
            Equal("folder1", c.Window.Left.Current?.Name, "The cursor is restored after a background read");
            Equal(1, c.Window.Left.Session.PreviousDirectories.Count, "Repeated Enter does not add history entries");
        }),
        new("history-rename-editor-keeps-alt-down", "UC15; UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            Check(c.Window.ActiveNavigationDialog == null && c.Window.Left.EditingRow != null, "History does not take keys from inline rename.");
            await c.KeyAsync(Key.Escape);
        }),
        new("history-scaled-long-unicode-path", "UC15; UC20", async c =>
        {
            var directory = Path.Combine(c.LeftPath, "Документы 日本語 " + new string('a', 80));
            Directory.CreateDirectory(directory);
            await c.Window.Left.LoadAsync(directory);
            await c.KeyAsync(Key.Back);
            c.Window.SetScaleForTest(1.5);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            dialog.SetScaleForTest(1.5);
            Equal(directory, dialog.Directories.SelectedItem, "History preserves long Unicode paths");
            await c.ScreenshotAsync("scaled-history", dialog);
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal(directory, c.Window.Left.Session.DirectoryPath, "A long Unicode history entry opens");
        })
    ];
}

// @tests RecentLocations, FavoriteLocations, FindSavedLocation, DeleteEntries, ReadLargeFiles, BrowseHistory, BrowseSnapshot, WindowsFileActions, SharedControls
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class NavigationFeedbackScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("feedback-navigation-command-focus", "Ctrl+D opens favorites from the command editor and keeps the draft", async c =>
        {
            c.Window.Favorites.Add(c.RightPath);
            c.Window.CommandLine.Editor.Text = "unfinished command";
            c.Window.CommandLine.FocusCommand();
            await c.KeyAsync(Key.D, RawInputModifiers.Control);
            var dialog = c.Window.ActiveNavigationDialog;
            Check(dialog != null && dialog.Favorites.IsKeyboardFocusWithin, "Ctrl+D focuses favorites from the command editor");
            await c.KeyAsync(Key.Escape, target: dialog);
            Equal("unfinished command", c.Window.CommandLine.Editor.Text, "The command draft stays intact");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Closing navigation returns to the panel");
        }),
        new("feedback-navigation-pages-and-colors", "Directory lists move the cursor by page and use the shared blue selection without hover fill", async c =>
        {
            NavigationFeedbackFixture.AddHistory(c);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            foreach (var scale in new[] { 1d, 1.5d })
            {
                dialog.SetScaleForTest(scale);
                await c.KeyAsync(Key.Home, target: dialog);
                await c.KeyAsync(Key.PageDown, target: dialog);
                var index = dialog.Directories.SelectedIndex;
                Check(index > 1 && index < dialog.Directories.ItemCount - 1, "PageDown moves the selected directory by a visible page");
                await c.KeyAsync(Key.PageUp, target: dialog);
                Equal(0, dialog.Directories.SelectedIndex, "PageUp returns to the first row");
                await c.KeyAsync(Key.End, RawInputModifiers.Control, dialog);
                Equal(dialog.Directories.ItemCount - 1, dialog.Directories.SelectedIndex, "Ctrl+End selects the final directory");
                await c.KeyAsync(Key.Home, target: dialog);
                var selected = (ListBoxItem)dialog.Directories.ContainerFromIndex(0)!;
                var other = (ListBoxItem)dialog.Directories.ContainerFromIndex(1)!;
                await NavigationFeedbackFixture.HoverAsync(c, other, dialog);
                Equal(Color.Parse("#15558A"), NavigationFeedbackFixture.RowColor(selected), "The current row uses the shared blue cursor");
                Equal(Colors.Transparent, NavigationFeedbackFixture.RowColor(other), "Hover does not create a second row highlight");
                await c.ScreenshotAsync("navigation-selection-" + scale * 100, dialog);
            }
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("feedback-navigation-missing-parent", "Missing history directories are marked and open their nearest existing parent", async c =>
        {
            var missing = Path.Combine(c.LeftPath, "removed", "nested");
            var state = c.Window.Left.CaptureState();
            c.Window.Left.Session.RestoreNavigation(state with { History = [missing] });
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var dialog = c.Window.ActiveNavigationDialog!;
            var label = dialog.Directories.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text?.Contains("removed", StringComparison.Ordinal) == true);
            Check(label.Text!.EndsWith(" [missing]", StringComparison.Ordinal), "A text marker identifies a missing directory");
            Check((ToolTip.GetTip(label) as string)?.Contains("nearest existing parent", StringComparison.Ordinal) == true, "The tooltip explains the fallback");
            dialog.Directories.SelectedItem = missing; dialog.FocusSelection();
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Enter opens the nearest existing ancestor");
            Check(c.Window.ActiveNavigationDialog == null && c.Window.LastError == null, "Successful fallback closes the chooser without an error");
            Check(c.Window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Directory no longer exists. Opened ", StringComparison.Ordinal) == true), "The result explains which directory was opened");
            var replaced = Path.Combine(c.LeftPath, "replaced"); await File.WriteAllTextAsync(replaced, "A file replaced this directory");
            Check(await c.Window.Left.LoadAsync(Path.Combine(replaced, "nested"), nearestExistingParent: true), "A directory replaced by a file also falls back to its parent");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "A replacement file is never entered as a directory");
            var denied = Path.Combine(c.LeftPath, "folder1"); c.FileSystem.DenyRead(denied);
            Check(!await c.Window.Left.LoadAsync(denied, nearestExistingParent: true), "Access errors do not silently navigate elsewhere");
        }),
        new("feedback-git-viewer-cursor-and-paths", "F3, PageDown and Escape keep the selected commit and use consistent Windows separators", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            await fixture.AddDetailedCommitAsync();
            await c.SelectAsync("..");
            await c.KeyAsync(Key.L, RawInputModifiers.Control | RawInputModifiers.Alt);
            var panel = c.Window.Right;
            Check(!panel.DisplayPath.Contains('/'), "The Git scope header uses Windows path separators");
            var current = panel.Current!.Name;
            c.PressKey(Key.F3); c.PressKey(Key.PageDown);
            await c.SettleAsync();
            Equal(current, panel.Current?.Name, "PageDown during report preparation cannot move the commit cursor");
            var viewer = c.Window.Viewer!;
            Check(viewer.Reader.TextContent.IsKeyboardFocusWithin, "The viewer receives keyboard focus after Git report creation");
            using (var updates = new DirectoryUpdates(c.Window)) await updates.PollAsync();
            await c.SettleAsync();
            Equal(current, panel.Current?.Name, "Background refresh preserves the viewed commit");
            Check(viewer.Reader.TextContent.IsKeyboardFocusWithin, "Background refresh keeps keyboard focus in the viewer");
            var visibleEnd = viewer.Reader.VisibleEnd;
            await c.KeyAsync(Key.PageDown, target: viewer);
            Check(viewer.Reader.VisibleEnd > visibleEnd, "PageDown scrolls the commit report");
            Equal(current, panel.Current?.Name, "Paging the viewer leaves the commit cursor intact");
            await c.KeyAsync(Key.Escape, target: viewer);
            Equal(current, panel.Current?.Name, "Closing the viewer leaves the same commit selected");
            Check(panel.Table.IsKeyboardFocusWithin, "Closing the viewer restores panel focus");
            await c.KeyAsync(Key.Enter);
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Enter);
            viewer = c.Window.Viewer!;
            Check(viewer.Reader.TextContent.IsKeyboardFocusWithin, "Opening a snapshot file keeps focus in its viewer");
            await c.KeyAsync(Key.Escape, target: viewer);
        }),
        new("feedback-delete-readonly-failure", "A failed permanent deletion restores file attributes and still permits deleting an absent file", async c =>
        {
            using var file = new LockedReadOnlyFile(c.LeftPath);
            var operations = new LocalFileOperations(new LocalFileSystem());
            var entry = (await operations.InspectAsync(file.Path, CancellationToken.None))!;
            var failed = false;
            try { await operations.RemoveAsync(entry, recycle: false, CancellationToken.None); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failed = true; }
            Check(failed && File.Exists(file.Path), "A locked file remains present after a failed deletion");
            Equal(FileAttributes.ReadOnly | FileAttributes.Hidden, File.GetAttributes(file.Path) & (FileAttributes.ReadOnly | FileAttributes.Hidden), "Failed deletion restores read-only and keeps hidden attributes");
            await operations.RemoveAsync(entry with { FullPath = Path.Combine(c.LeftPath, "already-removed.txt") }, recycle: false, CancellationToken.None);
        }),
        new("feedback-delete-readonly-git-objects", "Permanent deletion removes read-only Git object files and preserves adjacent files", async c =>
        {
            await NavigationFeedbackFixture.AddReadOnlyRepositoryAsync(c);
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync("readonly-repository");
            await c.KeyAsync(Key.Delete, RawInputModifiers.Shift); await ConfirmDeleteAsync(c);
            Check(!Directory.Exists(Path.Combine(c.LeftPath, "readonly-repository")), "Permanent deletion removes the complete read-only Git object tree");
            Check(!c.Window.Operations.IsWaitingForDecision, "Read-only attributes do not open a file-error dialog");
            Equal("abc", await File.ReadAllTextAsync(Path.Combine(c.LeftPath, "alpha.txt")), "Adjacent files stay intact");
        })
    ];
}

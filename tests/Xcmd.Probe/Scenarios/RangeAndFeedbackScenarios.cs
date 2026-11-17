// @tests MarkEntries, ReportControl, KeepFeedback
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FeedbackProbe;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class RangeAndFeedbackScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("range-toggle-boundaries", "UC20; Repeat a Shift boundary range to remove marks", async c =>
        {
            foreach (var key in new[] { Key.End, Key.Home })
            {
                await c.KeyAsync(Key.Escape); await c.SelectAsync("alpha.txt");
                await c.KeyAsync(key, RawInputModifiers.Shift);
                Check(c.Window.Left.SelectedNames().Length > 1, "Shift boundary navigation marks a range.");
                var cursor = c.Window.Left.Current?.Name;
                await c.KeyAsync(key, RawInputModifiers.Shift);
                Equal(0, c.Window.Left.SelectedNames().Length, "Repeating the same boundary gesture removes the range");
                Equal(cursor, c.Window.Left.Current?.Name, "Toggling at a boundary keeps the cursor");
                Check(!c.Window.Left.Rows.Single(row => row.Entry.IsParent).IsMarked, "Ranges never mark the parent entry.");
            }
            await c.KeyAsync(Key.Escape); await c.SelectAsync("folder1"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.End, RawInputModifiers.Shift);
            var range = c.Window.Left.SelectedNames().Where(name => name != "folder1").ToArray();
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.End, RawInputModifiers.Shift);
            Check(c.Window.Left.SelectedNames().SequenceEqual(new[] { "folder1" }), "Repeating a marked range keeps marks outside it.");
            Check(range.Length > 1, "The test removes a range with several files.");
        }),
        new("range-toggle-pages", "UC20; Repeat a page range to remove marks and fill a partial range", async c =>
        {
            for (var index = 0; index < 80; index++) c.AddFile($"page-{index:D3}.txt", "range");
            await c.Window.Left.LoadAsync(c.LeftPath); c.Window.Height = 450;
            foreach (var (key, start, back) in new[] { (Key.PageDown, Key.Home, Key.PageUp), (Key.PageUp, Key.End, Key.PageDown) })
            {
                await c.KeyAsync(Key.Escape); await c.KeyAsync(start);
                var source = c.Window.Left.Current?.Name;
                await c.KeyAsync(key, RawInputModifiers.Shift);
                var range = c.Window.Left.SelectedNames().Order().ToArray();
                Check(range.Length > 1, "Shift page navigation marks more than one file.");
                await c.KeyAsync(back); Equal(source, c.Window.Left.Current?.Name, "Plain page navigation returns to the range source");
                Check(c.Window.Left.SelectedNames().Order().SequenceEqual(range), "Plain page navigation preserves the range.");
                await c.KeyAsync(key, RawInputModifiers.Shift);
                Equal(0, c.Window.Left.SelectedNames().Length, "Marking the same page again removes its marks");
            }
            await c.KeyAsync(Key.Escape); await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift); await c.KeyAsync(Key.Up);
            await c.KeyAsync(Key.End, RawInputModifiers.Shift);
            Check(c.Window.Left.Rows.Where(row => string.CompareOrdinal(row.Name, "page-000.txt") >= 0).All(row => row.IsMarked), "A partial range fills its missing marks.");
            await c.KeyAsync(Key.End, RawInputModifiers.Shift);
            Equal(0, c.Window.Left.SelectedNames().Length, "Repeating a filled range removes its marks");
        }),
        new("feedback-navigation-labels-and-disabled-actions", "UC39; Pick labels, errors and disabled actions", async c =>
        {
            await c.KeyAsync(Key.D, RawInputModifiers.Control); var navigation = c.Window.ActiveNavigationDialog!;
            Check(!navigation.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("Tab / Shift+Tab", StringComparison.Ordinal) == true), "Navigation has no obvious-keyboard hint.");
            await CommentAsync(c, navigation, Text(navigation, "History"), "Go to directory: History");
            await CommentAsync(c, navigation, Text(navigation, "Favorite name"), "Go to directory: Favorite name");
            var save = navigation.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "FavoriteRename");
            Check(!save.IsEnabled, "The tested action is disabled.");
            await CommentAsync(c, navigation, save, "Go to directory: Save name");
            navigation.ShowError("Missing test folder"); await c.SettleAsync();
            var message = Text(navigation, "Could not open directory: Missing test folder");
            await CommentAsync(c, navigation, message, "Go to directory: Could not open directory: Missing test folder");
            await c.ScreenshotAsync("navigation-without-hint", navigation);
            await c.KeyAsync(Key.Escape, target: navigation);
        }),
        new("feedback-main-and-folder-labels", "UC39; Pick static text and validation messages without actions", async c =>
        {
            await CommentAsync(c, c.Window, Text(c.Window, "Ready"), "Ready");
            await CommentAsync(c, c.Window, Text(c.Window, "Shift+F5  Copy here"), "Shift+F5  Copy here");
            await c.KeyAsync(Key.F7); var folder = c.Window.ActiveDialog!;
            await CommentAsync(c, folder, Text(folder, "Folder name"), "New folder: Folder name");
            await c.TextAsync("folder1", folder); await c.KeyAsync(Key.Enter, target: folder);
            Check(folder.Error.Text?.Length > 0, "The dialog displays a real validation error.");
            await CommentAsync(c, folder, folder.Error, "New folder: " + folder.Error.Text);
            Equal("folder1", folder.Editor.Text, "Feedback preserves the folder draft");
            await c.KeyAsync(Key.Escape, target: folder);
        }),
        new("feedback-operation-labels", "UC39; Pick transfer and deletion labels", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F5); var transfer = c.Window.OperationDialog!;
            await CommentAsync(c, transfer, Text(transfer, "Copy alpha.txt to:"), "Copy: Copy alpha.txt to:");
            await c.KeyAsync(Key.Escape, target: transfer);
            await c.KeyAsync(Key.Delete); var deletion = (FileDeleteDialog)c.Window.OperationDialog!;
            await CommentAsync(c, deletion, Text(deletion, "Move to Recycle Bin?"), "Delete: Move to Recycle Bin?");
            await CommentAsync(c, deletion, deletion.Permanently, "Delete: Permanently");
            Check(deletion.Recycle.IsChecked == true, "Commenting on a radio option does not select it.");
            await c.KeyAsync(Key.Escape, target: deletion);
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Feedback never performs the operation.");
        }),
        new("feedback-conflict-and-comment-labels", "UC39; Pick comparison labels and nested comment text", async c =>
        {
            Directory.CreateDirectory(Path.Combine(c.RightPath, "alpha.txt"));
            await c.SelectAsync("alpha.txt"); await CopyAsync(c); var conflict = c.Window.OperationDialog!;
            await CommentAsync(c, conflict, Text(conflict, "Source"), "Name conflict: Source");
            var replace = conflict.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ConflictReplace");
            Check(!replace.IsEnabled, "Replacing a folder with a file is unavailable.");
            await CommentAsync(c, conflict, replace, "Name conflict: Replace");
            await c.KeyAsync(Key.F12, target: conflict); await c.ClickAsync(Text(conflict, "Destination"), target: conflict);
            var draft = c.Window.Feedback.ActiveComment!;
            await CommentAsync(c, draft, Text(draft, "Ctrl+Enter adds the comment · Esc cancels"), "Add a comment: Ctrl+Enter adds the comment · Esc cancels");
            Check(draft.IsVisible, "Nested feedback keeps the original comment open.");
            await c.KeyAsync(Key.Escape, target: draft); await c.KeyAsync(Key.Escape, target: conflict);
        })
    ];
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.DialogProbe;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class SelectionAndDialogScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("polish-marks-cursor-and-toggle", "UC20; Separate file marks from the cursor; toggle marks", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var marked = c.Window.Left.Rows.Single(row => row.Name == "alpha.txt");
            var cursor = c.Window.Left.Current!;
            Check(marked.IsMarked && marked.MarkGlyph.Length > 0, "Marked files have a visible marker.");
            Check(!cursor.IsMarked && cursor.MarkGlyph.Length == 0, "Moving the cursor does not mark its destination.");
            Check(((ISolidColorBrush)marked.NameForeground).Color != ((ISolidColorBrush)cursor.NameForeground).Color, "Marks and the cursor use different text treatments.");
            await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            await c.ScreenshotAsync("marked-files-and-cursor");
            await c.KeyAsync(Key.Up); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            Check(!c.Window.Left.Rows.Single(row => row.Name == "file10.bin").IsMarked, "Marking a row again removes its mark.");
            Check(c.Window.Left.SelectedNames().SequenceEqual(new[] { "alpha.txt" }), "Other marks remain unchanged.");
        }),
        new("polish-escape-clears-marks", "UC20; Escape clears marks and preserves editor cancellation", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var current = c.Window.Left.Current?.Name;
            await c.KeyAsync(Key.F6, RawInputModifiers.Shift); await c.KeyAsync(Key.Escape);
            Equal(2, c.Window.Left.SelectedNames().Length, "The first Escape cancels inline rename without changing marks");
            await c.KeyAsync(Key.Escape);
            Equal(0, c.Window.Left.SelectedNames().Length, "Escape from files clears all marks");
            Check(c.Window.Left.Rows.All(row => !row.IsMarked), "Mark indicators are cleared.");
            Equal(current, c.Window.Left.Current?.Name, "Clearing marks keeps the cursor");
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift); await c.TextAsync("unfinished"); await c.KeyAsync(Key.Escape);
            Equal(0, c.Window.Left.SelectedNames().Length, "Escape from the command line also clears file marks");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Escape returns focus to files.");
        }),
        new("polish-table-footer-clipping", "UC20; Keep table rows inside the table viewport", async c =>
        {
            for (var index = 0; index < 120; index++) c.AddFile($"clipped-{index:D3}.txt", "viewport");
            await c.Window.Left.LoadAsync(c.LeftPath);
            c.Window.Width = 840; c.Window.Height = 400;
            c.Window.Left.Table.Columns[0].Width = new GridLength(600);
            foreach (var scale in new[] { 1d, 1.5d })
            {
                c.Window.SetScaleForTest(scale); await c.KeyAsync(Key.End, RawInputModifiers.Control);
                await c.ScreenshotAsync($"table-footer-{scale * 100}");
                var table = c.Window.Left.Table;
                var footer = c.Window.Left.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "leftFooter");
                var tableBottom = table.TranslatePoint(new Point(0, table.Bounds.Height), c.Window)!.Value.Y;
                var footerTop = footer.TranslatePoint(default, c.Window)!.Value.Y;
                Check(table.ClipToBounds && tableBottom <= footerTop, "Table content is clipped above the footer.");
                Check(footer.Background != null && footer.ClipToBounds, "The footer has its own opaque surface and boundary.");
                var viewports = table.GetVisualDescendants().OfType<ScrollContentPresenter>().ToArray();
                Check(viewports.All(view => view.ClipToBounds), "Virtual rows remain inside their viewport.");
                Check(table.GetVisualDescendants().OfType<ScrollViewer>().All(view => !view.AllowAutoHide), "Scroll bars reserve their own space.");
                foreach (var bar in table.GetVisualDescendants().OfType<ScrollBar>().Where(bar => bar.Orientation == Orientation.Horizontal && bar.IsVisible && bar.Bounds.Height > 0))
                {
                    var top = bar.TranslatePoint(default, c.Window)!.Value.Y;
                    Check(viewports.All(view => view.TranslatePoint(new Point(0, view.Bounds.Height), c.Window)!.Value.Y <= top + 1), "Rows stop above the horizontal scroll bar.");
                }
            }
        }),
        new("polish-default-file-actions", "UC04; UC05; UC06; UC35; Default confirmations share a visible style", async c =>
        {
            await c.KeyAsync(Key.F7); var create = c.Window.ActiveDialog!;
            Primary(create, "CreateFolder"); await c.TextAsync("created", create); await c.KeyAsync(Key.Enter, target: create);
            Check(Directory.Exists(Path.Combine(c.LeftPath, "created")), "Enter creates the folder.");
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F5);
            Primary(c.Window.OperationDialog!, "ConfirmFileTransfer"); await c.KeyAsync(Key.Enter, target: c.Window.OperationDialog);
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Enter confirms copying");
            await c.SelectAsync("file2.log"); await c.KeyAsync(Key.F6);
            Primary(c.Window.OperationDialog!, "ConfirmFileTransfer"); await c.KeyAsync(Key.Enter, target: c.Window.OperationDialog);
            Check(File.Exists(Path.Combine(c.RightPath, "file2.log")) && !File.Exists(Path.Combine(c.LeftPath, "file2.log")), "Enter confirms moving.");
            await c.SelectAsync("file10.bin"); await c.KeyAsync(Key.Delete, RawInputModifiers.Shift);
            var deletion = (FileDeleteDialog)c.Window.OperationDialog!;
            Equal("Yes", Primary(deletion, "ConfirmFileDelete").Content, "The confirmation label contains no Enter hint");
            Check(deletion.Recycle.Content is AccessText { AccessKey: "R", ShowAccessKey: true } && deletion.Permanently.Content is AccessText { AccessKey: "P", ShowAccessKey: true }, "Deletion modes use underlined Windows access keys.");
            await c.ScreenshotAsync("delete-access-keys", deletion); await c.KeyAsync(Key.Enter, target: deletion);
            Check(!File.Exists(Path.Combine(c.LeftPath, "file10.bin")), "Enter confirms permanent deletion.");
        }),
        new("polish-default-navigation-actions", "UC15; UC16; The highlighted action matches Enter", async c =>
        {
            var target = Path.Combine(c.LeftPath, "folder1"); c.Window.Favorites.Add(target);
            await c.KeyAsync(Key.D, RawInputModifiers.Control); var dialog = c.Window.ActiveNavigationDialog!;
            Primary(dialog, "NavigationOpen");
            await c.KeyAsync(Key.F2, target: dialog); await c.TextAsync("Working files", dialog);
            Primary(dialog, "FavoriteRename");
            Check(!dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "NavigationOpen").IsDefault, "Editing a favorite changes the default action.");
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal("Working files", c.Window.Favorites.Items.Single().Name, "Enter saves the favorite name");
            Primary(dialog, "NavigationOpen"); await c.KeyAsync(Key.Enter, target: dialog);
            Equal(target, c.Window.Left.Session.DirectoryPath, "Enter opens the selected location");
        }),
        new("polish-default-conflict-actions", "UC34; The conflict default follows the active action", async c =>
        {
            var target = Path.Combine(c.RightPath, "alpha.txt"); File.WriteAllText(target, "existing version");
            await c.SelectAsync("alpha.txt"); await CopyAsync(c);
            var conflict = (FileConflictDialog)c.Window.OperationDialog!; Primary(conflict, "ConflictSkip");
            await c.KeyAsync(Key.Enter, target: conflict);
            Equal("existing version", File.ReadAllText(target), "Enter initially skips the conflict");
            await CopyAsync(c); conflict = (FileConflictDialog)c.Window.OperationDialog!;
            conflict.NewName.Focus(); conflict.NewName.SelectAll(); await c.TextAsync("alternative.txt", conflict);
            Primary(conflict, "ConflictRename"); await c.ScreenshotAsync("conflict-default", conflict);
            await c.KeyAsync(Key.Enter, target: conflict);
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alternative.txt")), "Enter in the name editor confirms Rename");
        }),
        new("polish-comment-primary-keeps-multiline-input", "UC39; Keep multiline comments and Ctrl+Enter", async c =>
        {
            await c.ClickAsync(c.Window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "StartFeedback"));
            await c.ClickAsync(c.Row("folder1")); var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("First line", comment); Primary(comment, "AddComment", isDefault: false);
            await c.KeyAsync(Key.Enter, target: comment); await c.TextAsync("Second line", comment);
            Equal(0, c.Window.Feedback.Notes.Count, "Enter keeps editing a multiline comment");
            Check(comment.Editor.Text!.Contains('\n'), "The editor accepts a line break.");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            Equal(1, c.Window.Feedback.Notes.Count, "Ctrl+Enter submits the comment");
        })
    ];
}

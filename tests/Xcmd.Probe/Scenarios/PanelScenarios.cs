using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public sealed record Scenario(string Id, string Requirement, Func<ProbeContext, Task> Run);

public static class PanelScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("real-directory-and-render", "UC20; architecture gate", async c =>
        {
            Equal(9, c.Window.Left.Rows.Count(row => !row.Entry.IsParent), "Real directory entries");
            Equal(1, c.Window.Right.Rows.Count(row => !row.Entry.IsParent), "Opposite directory entries");
            Check(c.Window.Left.Rows.Any(row => row.Name == ".env"), "Dotfiles are visible.");
            var handle = c.Window.TryGetPlatformHandle();
            Check(handle?.Handle == IntPtr.Zero && handle.HandleDescriptor == "STUB", "The headless window uses a zero-valued stub handle.");
            await c.ScreenshotAsync("panels");
        }),
        new("natural-name-order", "UC13", c =>
        {
            var rows = c.Window.Left.Rows;
            Check(rows.FindIndex(row => row.Name == "file2.log") < rows.FindIndex(row => row.Name == "file10.bin"), "Natural numeric order.");
            Check(rows.Take(2).All(row => row.Entry.IsDirectory), "Directories precede files.");
            Equal("folder1", rows.First(row => !row.Entry.IsParent).Name, "Directory name order");
            return Task.CompletedTask;
        }),
        new("sort-name-header", "UC13", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.ClickHeaderAsync("Name");
            Check(c.Window.Left.Session.SortOrder.Descending, "Header click reverses the name order.");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Cursor survives sort");
            Equal("folder1", c.Window.Left.Rows.First(row => !row.Entry.IsParent).Name, "Directories stay first and ascending");
            await c.ClickHeaderAsync("Name");
            Check(!c.Window.Left.Session.SortOrder.Descending, "Second click restores ascending order.");
        }),
        new("sort-extension-header", "UC13", async c =>
        {
            await c.ClickHeaderAsync("Extension");
            Equal(SortField.Extension, c.Window.Left.Session.SortOrder.Field, "Extension sort");
            Equal(".env", c.Window.Left.Rows.First(row => !row.Entry.IsDirectory).Name, "Empty extension first");
        }),
        new("sort-size-both-directions", "UC13", async c =>
        {
            await c.ClickHeaderAsync("Size");
            Equal(".env", c.Window.Left.Rows.First(row => !row.Entry.IsDirectory).Name, "Smallest file first");
            await c.ClickHeaderAsync("Size");
            Equal("отчёт 日本語.txt", c.Window.Left.Rows.First(row => !row.Entry.IsDirectory).Name, "Largest UTF-8 file first");
            Equal("folder1", c.Window.Left.Rows.First(row => !row.Entry.IsParent).Name, "Directory placement does not reverse");
        }),
        new("sort-date-and-hotkeys", "UC13", async c =>
        {
            await c.KeyAsync(Key.F5, RawInputModifiers.Control);
            Equal(SortField.Modified, c.Window.Left.Session.SortOrder.Field, "Date sort hotkey");
            Equal("file2.log", c.Window.Left.Rows.First(row => !row.Entry.IsDirectory).Name, "Oldest file first");
            await c.KeyAsync(Key.F6, RawInputModifiers.Control);
            Equal(SortField.Size, c.Window.Left.Session.SortOrder.Field, "Size sort hotkey");
            await c.KeyAsync(Key.F4, RawInputModifiers.Control);
            Equal(SortField.Extension, c.Window.Left.Session.SortOrder.Field, "Extension sort hotkey");
            await c.KeyAsync(Key.F3, RawInputModifiers.Control);
            Equal(SortField.Name, c.Window.Left.Session.SortOrder.Field, "Name sort hotkey");
        }),
        new("arrow-home-end-page-navigation", "UC20", async c =>
        {
            await c.KeyAsync(Key.Down);
            Equal("folder2", c.Window.Left.Current?.Name, "Down changes the cursor");
            await c.KeyAsync(Key.End, RawInputModifiers.Control);
            Equal(c.Window.Left.Rows[^1].Name, c.Window.Left.Current?.Name, "Ctrl+End reaches the last row");
            await c.KeyAsync(Key.Home, RawInputModifiers.Control);
            Equal("..", c.Window.Left.Current?.Name, "Ctrl+Home reaches the parent navigation row");
            await c.KeyAsync(Key.PageDown);
            Check(c.Window.Left.Table.SelectedIndex > 0, "PageDown moves the cursor.");
            await c.KeyAsync(Key.PageUp);
            Equal("..", c.Window.Left.Current?.Name, "PageUp returns to the first row");
        }),
        new("shift-mark-and-move-selection", "UC20; Mark departing rows and keep independent cursor", async c =>
        {
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            Equal(2, c.Window.Left.SelectedNames().Length, "Shift+Down marks the two departed rows");
            Equal(".env", c.Window.Left.Current?.Name, "The cursor advances past the marks");
            await c.KeyAsync(Key.Down);
            Equal(2, c.Window.Left.SelectedNames().Length, "Plain arrows preserve marked rows");
        }),
        new("ctrl-a-and-sort-preserve-selection", "UC13; UC20", async c =>
        {
            await c.KeyAsync(Key.A, RawInputModifiers.Control);
            Equal(9, c.Window.Left.SelectedNames().Length, "Ctrl+A selects all rows");
            await c.ClickHeaderAsync("Size");
            Equal(9, c.Window.Left.SelectedNames().Length, "Sort preserves selected identities");
        }),
        new("mouse-ctrl-toggle", "UC20", async c =>
        {
            await c.ClickAsync(c.Row("alpha.txt"));
            await c.ClickAsync(c.Row("file2.log"), RawInputModifiers.Control);
            Equal(2, c.Window.Left.SelectedNames().Length, "Ctrl+click adds a row");
            await c.ClickAsync(c.Row("alpha.txt"), RawInputModifiers.Control);
            Equal(1, c.Window.Left.SelectedNames().Length, "Ctrl+click removes a row");
        }),
        new("tab-panel-focus", "UC20", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Tab);
            Equal("right", c.Window.ActivePanel.Id, "Tab activates the other panel");
            Check(c.Window.Right.Table.IsKeyboardFocusWithin, "The other table owns keyboard focus.");
            await c.KeyAsync(Key.Tab, RawInputModifiers.Shift);
            Equal("left", c.Window.ActivePanel.Id, "Shift+Tab returns to the first panel");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Each panel keeps its cursor");
        }),
        new("mouse-panel-focus", "UC20", async c =>
        {
            await c.ClickAsync(c.Row("destination.txt", c.Window.Right));
            Equal("right", c.Window.ActivePanel.Id, "Click activates the destination panel");
            await c.KeyAsync(Key.F6, RawInputModifiers.Shift);
            Check(c.Window.Right.RenameEditor?.IsFocused == true, "The rename hotkey acts in the clicked panel.");
        }),
        new("enter-parent-cursor-restoration", "UC15; UC20", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "Enter opens a directory");
            Equal("..", c.Window.Left.Current?.Name, "The parent is selected on directory entry");
            await c.KeyAsync(Key.Back);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Backspace opens the parent");
            Equal("folder1", c.Window.Left.Current?.Name, "Parent cursor is restored");
        }),
        new("opposite-panel-directory", "UC14", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Right.Session.DirectoryPath, "Ctrl+Right opens the pointed directory");
            Equal("left", c.Window.ActivePanel.Id, "Source focus stays active");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Source keyboard focus survives navigation.");
        }),
        new("opposite-panel-file-fallback", "UC14", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            Equal(c.LeftPath, c.Window.Right.Session.DirectoryPath, "A file sends the current directory to the other panel");
            await c.KeyAsync(Key.Tab);
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Left, RawInputModifiers.Control);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Ctrl+Left uses the left destination");
        }),
        new("alt-prefix-jump-unicode", "UC03", async c =>
        {
            c.Input.KeyDown(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
            await c.TextAsync("отч");
            c.Input.KeyUp(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null);
            Equal("отчёт 日本語.txt", c.Window.Left.Current?.Name, "Alt+Unicode prefix jumps without filtering");
            Equal(10, c.Window.Left.Rows.Count, "Jump leaves all rows including the parent visible");
        }),
        new("rename-commit-and-resort", "UC17; architecture gate", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("renamed.txt");
            await c.ScreenshotAsync("rename");
            await c.KeyAsync(Key.Enter);
            Check(File.Exists(Path.Combine(c.LeftPath, "renamed.txt")), "Rename changes the real filesystem.");
            Check(!File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "The old name is gone.");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "renamed.txt")), "File contents survive rename");
            Equal("renamed.txt", c.Window.Left.Current?.Name, "The renamed item stays selected");
            Check(c.Window.Left.EditingRow == null && c.Window.Left.Table.IsKeyboardFocusWithin, "Rename exits and restores focus.");
            Check(c.Window.Left.Rows.FindIndex(row => row.Name == "renamed.txt") > c.Window.Left.Rows.FindIndex(row => row.Name == "file10.bin"), "The renamed item moves into sort order.");
        }),
        new("rename-printable-key-input", "UC17; rename does not consume printable key-down events before text input", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            foreach (var key in new[] { Key.Up, Key.Down, Key.PageUp, Key.PageDown }) await c.KeyAsync(key);
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Vertical editor keys do not move the file cursor");
            Check(c.Window.Left.RenameEditor?.IsFocused == true, "The editor retains keyboard focus");
            await c.KeyAsync(Key.A, RawInputModifiers.Control);
            Check(c.Input.TypeKey(Key.R, PhysicalKey.R, "r"), "Letter key-down remains available for text input");
            Check(c.Input.TypeKey(Key.Space, PhysicalKey.Space, " "), "Space remains available for text input");
            Check(c.Input.TypeKey(Key.R, PhysicalKey.R, "r"), "Further letters also reach the editor");
            await c.TextAsync(".txt");
            Equal("r r.txt", c.Window.Left.EditingRow?.DraftName, "Typing replaces the selected name and edits its draft");
            Equal("", c.Window.CommandLine.Editor.Text, "Rename input never becomes a command");
            await c.KeyAsync(Key.Enter);
            Check(File.Exists(Path.Combine(c.LeftPath, "r r.txt")), "The typed name is committed to the real filesystem");
            Equal("r r.txt", c.Window.Left.Current?.Name, "The renamed file remains current");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Committing returns focus to the file panel");
        }),
        new("rename-escape", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("discard.txt");
            await c.KeyAsync(Key.Escape);
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Escape keeps the original file.");
            Check(!File.Exists(Path.Combine(c.LeftPath, "discard.txt")), "Escape does not create a new name.");
            Check(c.Window.Left.EditingRow == null && c.Window.Left.Table.IsKeyboardFocusWithin, "Escape exits and restores focus.");
        }),
        new("rename-tab-commit-and-switch", "UC17; UC20", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("tabbed.txt");
            await c.KeyAsync(Key.Tab);
            Check(File.Exists(Path.Combine(c.LeftPath, "tabbed.txt")), "Tab commits rename.");
            Equal("right", c.Window.ActivePanel.Id, "Tab then switches panels");
        }),
        new("rename-conflict-and-retry", "UC17; UC34", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("zeta.md");
            await c.KeyAsync(Key.Enter);
            Check(c.Window.OperationDialog != null && c.Window.Left.EditingRow != null, "A collision opens an error dialog and keeps the rename draft.");
            Equal("notes", File.ReadAllText(Path.Combine(c.LeftPath, "zeta.md")), "The destination is not overwritten");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha.txt")), "The source is intact");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            await c.KeyAsync(Key.A, RawInputModifiers.Control);
            await c.TextAsync("retried.txt");
            await c.KeyAsync(Key.Enter);
            Check(File.Exists(Path.Combine(c.LeftPath, "retried.txt")), "A corrected name can be committed.");
        }),
        new("rename-invalid-names", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            foreach (var invalid in new[] { "", "..", "bad/name", "CON", "LPT1.txt", "trailing.", "trailing " })
            {
                await c.KeyAsync(Key.A, RawInputModifiers.Control);
                await c.KeyAsync(Key.Back);
                if (invalid.Length > 0) await c.TextAsync(invalid);
                await c.KeyAsync(Key.Enter);
                Check(c.Window.OperationDialog != null && c.Window.Left.EditingRow != null, "Invalid name stays in edit mode behind the error dialog: " + invalid);
                Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Invalid input leaves the file intact.");
                await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            }
        }),
        new("rename-unicode-and-spaces", "UC17", async c =>
        {
            await c.BeginRenameAsync("отчёт 日本語.txt");
            await c.TextAsync("new отчёт 日本語.txt");
            await c.KeyAsync(Key.Enter);
            Check(File.Exists(Path.Combine(c.LeftPath, "new отчёт 日本語.txt")), "Unicode and spaces survive rename.");
        }),
        new("rename-case-only", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("ALPHA.TXT");
            await c.KeyAsync(Key.Enter);
            Check(Directory.EnumerateFiles(c.LeftPath).Select(Path.GetFileName).Contains("ALPHA.TXT", StringComparer.Ordinal), "Case-only rename reaches the filesystem.");
            Equal("ALPHA.TXT", c.Window.Left.Current?.Name, "The displayed spelling is updated");
        }),
        new("rename-directory", "UC17", async c =>
        {
            await c.BeginRenameAsync("folder1");
            await c.TextAsync("renamed-folder");
            await c.KeyAsync(Key.Enter);
            Check(File.Exists(Path.Combine(c.LeftPath, "renamed-folder", "inside.txt")), "Directory contents survive rename.");
        }),
        new("rename-same-name", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.KeyAsync(Key.Enter);
            Check(c.Window.LastError == null && c.Window.Left.EditingRow == null, "The unchanged name closes without error.");
        }),
        new("rename-editor-owns-navigation", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.KeyAsync(Key.Right);
            await c.KeyAsync(Key.Left);
            await c.TextAsync("x");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Text cursor keys do not move the file cursor");
            Check(c.Window.Left.EditingRow?.DraftName.Contains('x') == true, "Typing edits the name.");
            await c.KeyAsync(Key.F5);
            Check(!File.Exists(Path.Combine(c.RightPath, "alpha.txt")), "File copy is suppressed while editing.");
        }),
        new("rename-right-collapses-selection", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.KeyAsync(Key.Right);
            var editor = c.Window.Left.RenameEditor!;
            Equal("", editor.SelectedText, "Right clears the full-name selection");
            Equal("alpha.txt".Length, editor.CaretIndex, "Right moves the caret to the end of the name");
            Check(editor.IsFocused, "Right keeps keyboard focus in the inline editor.");
            var selected = c.Window.Left.SelectedNames();
            await c.KeyAsync(Key.Down);
            Check(editor.IsFocused, "Down keeps focus in the single-line editor.");
            Check(selected.SequenceEqual(c.Window.Left.SelectedNames()), "Editor navigation leaves file selection unchanged.");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Editor navigation leaves the file cursor unchanged");
            await c.TextAsync("x");
            Equal("alpha.txtx", c.Window.Left.EditingRow?.DraftName, "Text is inserted at the end of the name");
            await c.KeyAsync(Key.Escape);
        }),
        new("rename-navigation-stays-in-editor", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            var editor = c.Window.Left.RenameEditor!;
            var selected = c.Window.Left.SelectedNames();
            foreach (var key in new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.Home, Key.End, Key.PageUp, Key.PageDown })
            {
                await c.KeyAsync(key);
                Check(editor.IsFocused, "The editor keeps focus after " + key);
                Equal("alpha.txt", c.Window.Left.Current?.Name, "The file cursor stays in place after " + key);
                Check(selected.SequenceEqual(c.Window.Left.SelectedNames()), "File selection stays unchanged after " + key);
            }
            await c.KeyAsync(Key.Home);
            Equal(0, editor.CaretIndex, "Home moves to the start of the name");
            await c.KeyAsync(Key.End);
            Equal(editor.Text!.Length, editor.CaretIndex, "End moves to the end of the name");
            await c.KeyAsync(Key.Left, RawInputModifiers.Shift);
            Equal("t", editor.SelectedText, "Shift+Left selects text in the name");
            await c.KeyAsync(Key.Escape);
        }),
        new("rename-click-other-row-cancels", "UC17; UC20", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("discard.txt");
            await c.ClickAsync(c.Row("file2.log"));
            Check(c.Window.Left.EditingRow == null, "Clicking a different file cancels the inline edit.");
            Equal("file2.log", c.Window.Left.Current?.Name, "The clicked row becomes current");
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "The uncommitted rename does not change files.");
        }),
        new("rename-switch-panel-cancels", "UC17; UC20", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("discard.txt");
            await c.ClickAsync(c.Row("destination.txt", c.Window.Right));
            Check(c.Window.Left.EditingRow == null, "Clicking the other panel cancels rename.");
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "The uncommitted name is discarded.");
        }),
        new("refresh-preserves-multiselection", "UC20", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var selected = c.Window.Left.SelectedNames().Order().ToArray();
            c.AddFile("added.txt", "External change");
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Check(selected.SequenceEqual(c.Window.Left.SelectedNames().Order()), "Refresh preserves selected file identities.");
            Check(c.Window.Left.Rows.Any(row => row.Name == "added.txt"), "Refresh loads external changes.");
        }),
        new("refresh-deleted-current", "UC20", async c =>
        {
            await c.SelectAsync("alpha.txt");
            File.Delete(Path.Combine(c.LeftPath, "alpha.txt"));
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Check(c.Window.Left.Current != null && c.Window.Left.Current.Name != "alpha.txt", "A valid cursor replaces the deleted item.");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Refresh retains usable keyboard focus.");
        }),
        new("new-folder-dialog-create", "UC06; architecture gate", async c =>
        {
            await c.KeyAsync(Key.F7);
            var dialog = c.Window.ActiveDialog;
            Check(dialog != null && dialog.Editor.IsFocused, "The application dialog opens with input focus.");
            await c.TextAsync("created-folder", dialog);
            await c.KeyAsync(Key.Enter, target: dialog);
            Check(Directory.Exists(Path.Combine(c.LeftPath, "created-folder")), "The dialog creates a real directory.");
            Check(c.Window.ActiveDialog == null, "The dialog closes after creation.");
            Equal("created-folder", c.Window.Left.Current?.Name, "The created directory is selected");
        }),
        new("new-folder-dialog-cancel", "UC06; architecture gate", async c =>
        {
            await c.KeyAsync(Key.F7);
            await c.TextAsync("discard-folder", c.Window.ActiveDialog);
            await c.KeyAsync(Key.Escape, target: c.Window.ActiveDialog);
            Check(c.Window.ActiveDialog == null && !Directory.Exists(Path.Combine(c.LeftPath, "discard-folder")), "Escape dismisses the dialog without changing files.");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Dialog dismissal restores panel focus.");
        }),
        new("new-folder-dialog-conflict", "UC06; UC34", async c =>
        {
            await c.KeyAsync(Key.F7);
            var dialog = c.Window.ActiveDialog!;
            await c.TextAsync("folder1", dialog);
            await c.KeyAsync(Key.Enter, target: dialog);
            Check(c.Window.ActiveDialog != null && !string.IsNullOrEmpty(dialog.Error.Text), "An existing folder reports a conflict.");
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("copy-real-file-and-conflict", "UC04; UC34", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await CopyAsync(c);
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Copy writes the actual file contents");
            Check(c.Window.Right.Rows.Any(row => row.Name == "alpha.txt"), "Copy refreshes the destination panel.");
            File.WriteAllText(Path.Combine(c.RightPath, "alpha.txt"), "destination version");
            await CopyAsync(c);
            Check(c.Window.OperationDialog is FileConflictDialog, "Copy asks for a conflict decision instead of overwriting.");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            Equal("destination version", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Cancelling a conflict keeps the destination");
        }),
        new("empty-directory-keyboard", "UC20", async c =>
        {
            await c.SelectAsync("folder2");
            await c.KeyAsync(Key.Enter);
            Equal(1, c.Window.Left.Rows.Count, "An empty directory displays only its parent row");
            Equal("..", c.Window.Left.Current?.Name, "The parent row is current in an empty directory");
            await c.KeyAsync(Key.Down);
            await c.KeyAsync(Key.F6, RawInputModifiers.Shift);
            Check(c.Window.Left.EditingRow == null, "Rename safely ignores an empty panel.");
            await c.KeyAsync(Key.Back);
            Equal("folder2", c.Window.Left.Current?.Name, "The cursor returns to the empty directory");
        }),
        new("missing-directory-keeps-state", "UC20", async c =>
        {
            await c.SelectAsync("folder2");
            Directory.Delete(Path.Combine(c.LeftPath, "folder2"));
            await c.KeyAsync(Key.Enter);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Failed navigation keeps the old directory");
            Check(c.Window.Left.Session.Error != null, "The navigation failure is observable.");
        }),
        new("resize-and-columns", "UC13; UC20", async c =>
        {
            c.Window.Width = 1000; c.Window.Height = 500;
            c.Window.Left.Table.Columns[0].Width = new GridLength(180);
            await c.SettleAsync();
            Check(c.Window.Left.Table.Columns.Where(column => column.IsVisible).All(column => double.IsFinite(column.ActualWidth) && column.ActualWidth > 0), "Visible columns have finite positive widths after resize.");
            await c.ScreenshotAsync("compact-panels");
            await c.BeginRenameAsync("alpha.txt");
            Check(c.Window.Left.RenameEditor!.Bounds.Width > 20, "The inline editor remains usable in a narrow panel.");
        }),
        new("large-directory-virtualization", "UC13; UC17; UC20", async c =>
        {
            var prepare = ProbeTimer.Start();
            for (var i = 0; i < 10000; i++) c.AddFile($"bulk-{i:D5}.txt", "x");
            c.Measurements["fixtureCreationMs"] = prepare.Elapsed.TotalMilliseconds;
            var refresh = ProbeTimer.Start();
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            c.Measurements["loadAndLayoutMs"] = refresh.Elapsed.TotalMilliseconds;
            Equal(10009, c.Window.Left.Rows.Count(row => !row.Entry.IsParent), "Large directory entry count");
            var realized = c.Window.Left.Table.GetRealizedContainers().Count();
            c.Measurements["realizedRows"] = realized;
            Check(realized > 0 && realized < 150, "The control realizes visible rows instead of all 10,009 rows.");
            await c.KeyAsync(Key.End, RawInputModifiers.Control);
            Equal(c.Window.Left.Rows[^1].Name, c.Window.Left.Current?.Name, "End reaches a virtualized last row");
            await c.KeyAsync(Key.Home, RawInputModifiers.Control);
            await c.KeyAsync(Key.Down);
            await c.KeyAsync(Key.Down);
            await c.KeyAsync(Key.Down);
            var before = c.Window.Left.Current!.Name;
            await c.KeyAsync(Key.F6, RawInputModifiers.Shift);
            Check(c.Window.Left.RenameEditor?.IsFocused == true, "A recycled row can host a focused editor.");
            await c.TextAsync("bulk-renamed.txt");
            await c.KeyAsync(Key.Enter);
            Check(File.Exists(Path.Combine(c.LeftPath, "bulk-renamed.txt")) && !File.Exists(Path.Combine(c.LeftPath, before)), "Rename works after virtualization.");
            var sort = ProbeTimer.Start();
            await c.ClickHeaderAsync("Size");
            c.Measurements["sortAndLayoutMs"] = sort.Elapsed.TotalMilliseconds;
            await c.ScreenshotAsync("large-directory");
            RenderingProbe.MeasurePaging(c);
        }),
        new("column-resize-pointer", "UC13; UC20", async c =>
        {
            var table = c.Window.Left.Table;
            var header = table.GetVisualDescendants().OfType<TableViewColumnHeader>().First();
            var thumb = header.GetVisualDescendants().OfType<Thumb>().First();
            var origin = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), c.Window)!.Value;
            var before = table.Columns[0].ActualWidth;
            c.Input.MovePointer(origin);
            c.Input.PressPointer(origin, MouseButton.Left);
            c.Input.MovePointer(origin - new Vector(40, 0), RawInputModifiers.LeftMouseButton);
            c.Input.ReleasePointer(origin - new Vector(40, 0), MouseButton.Left);
            await c.SettleAsync();
            Check(table.Columns[0].ActualWidth < before - 20, "Dragging the real column separator shrinks the name column within the panel.");
        }),
        new("double-click-directory", "UC20", async c =>
        {
            await c.ClickAsync(c.Row("folder1"));
            await c.ClickAsync(c.Row("folder1"));
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "Double-click opens the real directory");
        }),
        new("dpi-150-percent", "UC17; UC20", async c =>
        {
            c.Window.SetScaleForTest(1.5);
            await c.SettleAsync();
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("scaled.txt");
            await c.ScreenshotAsync("scaled-rename");
            await c.KeyAsync(Key.Enter);
            Check(File.Exists(Path.Combine(c.LeftPath, "scaled.txt")), "Rename works with a scaled layout.");
            Equal(1.5, c.Window.RenderScaling, "Rendering scale");
            Equal(1920, (int)c.Measurements["frameWidth"], "Physical offscreen pixel width");
        }),
        new("rename-source-removed", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            File.Delete(Path.Combine(c.LeftPath, "alpha.txt"));
            await c.TextAsync("missing.txt");
            await c.KeyAsync(Key.Enter);
            Check(c.Window.LastError != null && c.Window.Left.EditingRow != null, "A removed source reports an error and keeps the editor.");
            Check(!File.Exists(Path.Combine(c.LeftPath, "missing.txt")), "Failed rename creates no file.");
            await c.KeyAsync(Key.Escape);
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Check(c.Window.Left.Rows.All(row => row.Name != "alpha.txt"), "Refresh removes the stale source row.");
        }),
        new("sort-preserves-current-and-range", "UC13; UC20", async c =>
        {
            await c.ClickAsync(c.Row("alpha.txt"));
            await c.ClickAsync(c.Row("file2.log"), RawInputModifiers.Control);
            Equal("file2.log", c.Window.Left.Current?.Name, "Cursor is separate from the first selected item");
            await c.ClickHeaderAsync("Size");
            Equal("file2.log", c.Window.Left.Current?.Name, "Sort preserves the cursor identity");
            Check(c.Window.Left.SelectedNames().Order().SequenceEqual(new[] { "alpha.txt", "file2.log" }), "Sort preserves the selected identities.");
        }),
        new("hidden-file-visible", "UC20", async c =>
        {
            c.AddFile("hidden.txt", "Hidden file");
            File.SetAttributes(Path.Combine(c.LeftPath, "hidden.txt"), FileAttributes.Hidden);
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Check(c.Window.Left.Rows.Any(row => row.Name == "hidden.txt"), "Windows hidden files remain visible.");
        }),
        new("rename-repeated-enter", "UC17", async c =>
        {
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("once.txt");
            c.Input.KeyDown(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            c.Input.KeyUp(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            c.Input.KeyDown(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            c.Input.KeyUp(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await c.SettleAsync();
            Check(File.Exists(Path.Combine(c.LeftPath, "once.txt")), "Repeated Enter produces one renamed file.");
            Check(c.Window.LastError == null && c.Window.Left.EditingRow == null, "Repeated Enter does not create a stale failure.");
        }),
        new("background-read-keeps-input-responsive", "UC20; development requirements", async c =>
        {
            var gate = c.FileSystem.PauseNextRead(c.LeftPath);
            c.Input.KeyDown(Key.R, RawInputModifiers.Control, PhysicalKey.R, null);
            c.Input.KeyUp(Key.R, RawInputModifiers.Control, PhysicalKey.R, null);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(c.Window.IsBusy && c.Window.Left.Session.IsLoading, "A real pending read is observable.");
            c.Input.KeyDown(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            c.Input.KeyUp(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Equal("right", c.Window.ActivePanel.Id, "Panel switching runs while a filesystem read is pending");
            gate.Release.TrySetResult();
            await c.SettleAsync();
            Check(c.Window.Right.Table.IsKeyboardFocusWithin, "Background completion does not steal focus.");
        }),
        new("parent-row-pinned-for-all-sorts", "UC13; UC20", async c =>
        {
            foreach (var field in new[] { "Name", "Extension", "Size", "Modified" })
            {
                for (var direction = 0; direction < 2; direction++)
                {
                    await c.ClickHeaderAsync(field);
                    Equal("..", c.Window.Left.Rows[0].Name, "The parent row stays first for " + field);
                    Check(c.Window.Left.Rows[0].Entry.IsParent, "The parent is a navigation entry.");
                }
            }
        }),
        new("parent-row-enter-restores-cursor", "UC15; UC20", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            await c.SelectAsync("..");
            await c.KeyAsync(Key.Enter);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Enter on the parent row opens the parent directory");
            Equal("folder1", c.Window.Left.Current?.Name, "The directory we left stays under the cursor");
        }),
        new("ctrl-page-up-opens-parent", "UC15; UC20", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            await c.KeyAsync(Key.PageUp, RawInputModifiers.Control);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Ctrl+PageUp opens the parent from an ordinary file");
            Equal("folder1", c.Window.Left.Current?.Name, "Ctrl+PageUp restores the directory cursor");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Parent navigation preserves panel focus.");
        }),
        new("parent-row-cannot-rename-or-copy", "UC17; UC20", async c =>
        {
            await c.SelectAsync("..");
            await c.KeyAsync(Key.F6, RawInputModifiers.Shift);
            Check(c.Window.Left.EditingRow == null, "The parent row cannot enter rename mode.");
            Equal(0, c.Window.Left.SelectedNames().Length, "The parent row is excluded from selected file names");
            await c.KeyAsync(Key.F5);
            Equal(1, Directory.EnumerateFileSystemEntries(c.RightPath).Count(), "Copying the parent row does not copy anything");
            Check(c.Window.LastError == null, "Parent-only copy is a safe no-op.");
        }),
        new("parent-row-root-boundary", "UC20", c =>
        {
            Check(PanelRules.ParentEntry(Path.GetPathRoot(c.LeftPath)!) == null, "A drive root has no parent row.");
            Check(PanelRules.ParentEntry(@"\\server\share\") == null, "A UNC share root has no parent row.");
            Equal(c.LeftPath, PanelRules.ParentEntry(Path.Combine(c.LeftPath, "folder1"))?.FullPath, "A normal directory points to its parent");
            return Task.CompletedTask;
        }),
        new("obsolete-read-is-cancelled", "UC20; development requirements", async c =>
        {
            var panel = c.Window.Left;
            var gate = c.FileSystem.PauseNextRead(c.LeftPath);
            var oldRead = panel.LoadAsync(c.LeftPath, preserveSelection: true);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await panel.LoadAsync(Path.Combine(c.LeftPath, "folder1"));
            await oldRead;
            panel.FocusCurrent();
            await c.SettleAsync();
            Check(gate.WasCancelled, "A newer location cancels the obsolete read.");
            Equal(Path.Combine(c.LeftPath, "folder1"), panel.Session.DirectoryPath, "An old response cannot replace the current location");
            Equal("..", panel.Current?.Name, "The new location selects its parent entry");
        })
    ];
}

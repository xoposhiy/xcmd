// @tests NavigatePanels, SortEntries, JumpToName, RecentLocations, ReadablePanels, RefreshEntries, DiscoverShortcuts, CreateFolder, RestoreWorkspace, ReportControl, KeepFeedback, TryLocalRelease, SharedControls, FileIcons, KeyboardLayouts, FeedbackQueue, HeadlessVerification
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Scenarios;

public static class RefinementScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("refinement-readable-sort-headings", "UC13; centered and readable headings", async c =>
        {
            foreach (var panel in new[] { c.Window.Left, c.Window.Right })
            {
                foreach (var header in panel.Table.GetVisualDescendants().OfType<TableViewColumnHeader>())
                {
                    if (!header.IsVisible || header.Bounds.Width == 0) continue;
                    var button = header.GetVisualDescendants().OfType<Button>().FirstOrDefault(item => item.Name?.Contains("Sort", StringComparison.Ordinal) == true);
                    var text = (button?.GetVisualDescendants() ?? header.GetVisualDescendants()).OfType<TextBlock>().First();
                    var center = text.TranslatePoint(new Point(0, text.Bounds.Height / 2), header)!.Value.Y;
                    Check(Math.Abs(center - header.Bounds.Height / 2) < 1, "Each heading is vertically centered in the whole header.");
                    Check(text.FontSize >= 13, "Headings are at least as large as file names.");
                    Equal(Color.Parse("#F1F5FA"), (text.Foreground as ISolidColorBrush)?.Color, "Heading text uses the primary text color");
                }
            }
            await c.ScreenshotAsync("readable-sort-headings");
        }),
        new("refinement-full-sort-header", "UC13", async c =>
        {
            var button = Button(c.Window, "leftSortName");
            var header = button.GetVisualAncestors().OfType<TableViewColumnHeader>().Single();
            c.Measurements["headerGeometry"] = new { button = button.Bounds.ToString(), header = header.Bounds.ToString(), header.HorizontalContentAlignment, header.VerticalContentAlignment, header.Padding };
            await c.ClickPartAsync(header, 0.8, 0.15);
            Check(c.Window.Left.Session.SortOrder.Descending, "Blank header space sorts the column.");
            await c.ClickPartAsync(header, 0.75, 0.85);
            Check(!c.Window.Left.Session.SortOrder.Descending, "The opposite edge reverses sorting.");
            await c.ClickPartAsync(header, 0.65, 0.5);
            await c.ClickPartAsync(header, 0.65, 0.5);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Header clicks never open the current directory");
        }),
        new("refinement-square-inactive-selection", "UC20", async c =>
        {
            Equal(0d, c.Window.Left.CornerRadius.TopLeft, "Panels have square corners");
            var inactive = (TableViewRow)c.Row("destination.txt", c.Window.Right);
            var border = inactive.GetVisualDescendants().OfType<Border>().First(item => item.Name == "Border");
            Equal(Color.Parse("#1D2532"), (border.Background as ISolidColorBrush)?.Color, "Inactive selection is faint");
            await c.KeyAsync(Key.Tab); await c.SettleAsync();
            Check((border.Background as ISolidColorBrush)?.Color != Color.Parse("#1D2532"), "Activating a panel restores its visible selection.");
            c.Window.Width = 1000; c.Window.Height = 550;
            await c.ScreenshotAsync("square-panels");
        }),
        new("refinement-command-menu", "UC20", async c =>
        {
            var menu = c.Window.GetVisualDescendants().OfType<Menu>().Single();
            Check(menu.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()).SequenceEqual(new[] { "File", "Navigate", "Sort", "View", "Git", "Help" }), "The header contains a command menu.");
            Check(!c.Window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "XCMD   /   FILE PANELS"), "The large title is removed.");
            await c.ClickAsync((MenuItem)menu.Items[0]!);
            Check(c.Window.IsMenuOpen && !c.Window.Readiness.CanRestart, "An open menu defers automatic updates.");
            var file = (MenuItem)menu.Items[0]!;
            await c.ClickAsync(file.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "New folder"));
            Check(c.Window.ActiveDialog != null, "The File menu opens the actual new-folder dialog.");
            await c.KeyAsync(Key.Escape, target: c.Window.ActiveDialog);
        }),
        new("refinement-feedback-history-modal", "UC39", async c =>
        {
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter); await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            var history = c.Window.ActiveNavigationDialog!;
            var selected = history.Directories.SelectedItem;
            await c.KeyAsync(Key.F12, target: history);
            Check(c.Window.Feedback.IsPickingIn(history), "F12 starts feedback in a modal history window.");
            await c.ClickAsync(history.Directories, target: history);
            var comment = c.Window.Feedback.ActiveComment!;
            Check(comment != null && history.IsVisible, "The comment is owned by the existing modal workflow.");
            await c.TextAsync("Keep this history readable.", target: comment);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            Equal(selected, history.Directories.SelectedItem, "Commenting does not change the modal selection");
            Check(c.Window.Feedback.Notes.Single().Target.Name.StartsWith("Go to directory:"), "The comment records its window context.");
            await c.KeyAsync(Key.Enter, target: history);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "The original modal action still works after a comment");
        }),
        new("refinement-release-after-dialog-close", "A pending dialog close sends the key release to the main window", async c =>
        {
            await c.KeyAsync(Key.F7);
            Check(FeedbackProbe.ReleaseKeyWhileDialogCloses(c, c.Window.ActiveDialog!),
                "The main window receives the release after the dialog closes during input dispatch");
        }),
        new("refinement-feedback-folder-draft", "UC39", async c =>
        {
            await c.KeyAsync(Key.F7); var folder = c.Window.ActiveDialog!;
            await c.TextAsync("unfinished folder", target: folder);
            var feedback = folder.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "DialogFeedback");
            await c.ClickAsync(feedback, target: folder);
            await c.ClickAsync(folder.Editor, target: folder);
            var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Explain folder naming rules here.", target: comment);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            Equal("unfinished folder", folder.Editor.Text, "Feedback preserves the unfinished folder name");
            Check(folder.IsVisible && !Directory.Exists(Path.Combine(c.LeftPath, "unfinished folder")), "Selecting an element does not submit the dialog.");
            await c.KeyAsync(Key.F12, target: folder); await c.KeyAsync(Key.Escape, target: folder);
            Check(folder.IsVisible && !c.Window.Feedback.IsPicking, "Escape cancels feedback without closing its modal window.");
            await c.KeyAsync(Key.Escape, target: folder);
        }),
        new("refinement-feedback-nested-comment-draft", "UC39", async c =>
        {
            await c.KeyAsync(Key.F12); await c.ClickAsync(c.Row("alpha.txt"));
            var draft = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Original comment draft", target: draft);
            await c.KeyAsync(Key.F12, target: draft); await c.ClickAsync(draft.Editor, target: draft);
            var nested = c.Window.Feedback.ActiveComment!;
            Check(nested != draft, "The feedback editor can itself receive a comment.");
            await c.TextAsync("Make the comment input taller.", target: nested);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, nested);
            Equal(draft, c.Window.Feedback.ActiveComment, "Closing nested feedback returns to the original editor");
            Equal("Original comment draft", draft.Editor.Text, "The original draft is preserved");
            await c.KeyAsync(Key.Escape, target: draft);
            Equal(1, c.Window.Feedback.Notes.Count, "Only the submitted nested comment is saved");
        }),
        new("refinement-directory-ranking", "UC15", () =>
        {
            var now = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
            var book = new DirectoryUsageBook();
            for (var i = 0; i < 30; i++) book.Visit(@"C:\old-project", now.AddDays(-30));
            book.Visit(@"C:\recent-project", now);
            Equal(@"C:\recent-project", book.Suggest(@"C:\", now)[0].Path, "Recent work outranks a much older frequent location");
            var initial = book.Suggest(@"C:\", now).Single(item => item.Path == @"C:\recent-project").Score;
            var later = book.Suggest(@"C:\", now.AddDays(3)).Single(item => item.Path == @"C:\recent-project").Score;
            Equal(initial / 2, later, "Visit weight halves every three days");
            book.Visit(@"c:\RECENT-project", now);
            Equal(2L, book.Suggest(@"C:\", now)[0].Visits, "Path identity ignores case");
            Check(book.Suggest(@"C:\recent-project", now).All(item => !item.Path.Equals(@"C:\recent-project", StringComparison.OrdinalIgnoreCase)), "The current location is excluded.");
            for (var i = 0; i < 520; i++) book.Visit(@"C:\project-" + i, now);
            Equal(512, book.Capture().Length, "Statistics stay bounded");
        }),
        new("refinement-history-two-columns-and-persistence", "UC15", async c =>
        {
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter); await c.KeyAsync(Key.Back);
            var before = c.Window.DirectoryUsage.Capture().Sum(item => item.Visits);
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Equal(before, c.Window.DirectoryUsage.Capture().Sum(item => item.Visits), "Reloading does not count as another visit");
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt); var history = c.Window.ActiveNavigationDialog!;
            Check(history.Favorites.ItemCount > 0, "History offers shared suggested locations.");
            await c.KeyAsync(Key.Right, target: history);
            Check(history.Favorites.IsKeyboardFocusWithin, "Right enters the suggestions column.");
            await c.KeyAsync(Key.Left, target: history);
            Check(history.Directories.IsKeyboardFocusWithin, "Left returns to this panel's history.");
            await c.KeyAsync(Key.Right, target: history);
            var destination = (string)history.Favorites.SelectedItem!;
            await c.ScreenshotAsync("history-two-columns", history);
            await c.KeyAsync(Key.Enter, target: history);
            Equal(destination, c.Window.Left.Session.DirectoryPath, "Enter opens the selected suggestion in the source panel");
            var state = c.Window.CaptureWorkspace();
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json")); await store.SaveAsync(state);
            await c.Window.RestoreWorkspaceAsync((await store.ReadAsync())!);
            Equal(state.Usage!.Sum(item => item.Visits), c.Window.DirectoryUsage.Capture().Sum(item => item.Visits), "Restoring a session does not inflate visit counts");
        }),
        new("refinement-live-files-and-preserved-selection", "UC20", async c =>
        {
            using var updates = new DirectoryUpdates(c.Window); updates.Start();
            await c.ClickAsync(c.Row("alpha.txt")); await c.ClickAsync(c.Row("file2.log"), RawInputModifiers.Control);
            c.AddFile("external.txt", "Created outside the application");
            await WaitAsync(() => c.Window.Left.Rows.Any(row => row.Name == "external.txt"));
            Equal("file2.log", c.Window.Left.Current?.Name, "External creation preserves the file cursor");
            Check(c.Window.Left.SelectedNames().Order().SequenceEqual(new[] { "alpha.txt", "file2.log" }), "External changes preserve multiple selected files.");
            File.WriteAllText(Path.Combine(c.LeftPath, "external.txt"), new string('x', 200));
            await WaitAsync(() => c.Window.Left.Rows.Any(row => row.Name == "external.txt" && row.Entry.Size == 200));
            File.Move(Path.Combine(c.LeftPath, "external.txt"), Path.Combine(c.LeftPath, "renamed.txt"));
            await WaitAsync(() => c.Window.Left.Rows.Any(row => row.Name == "renamed.txt") && c.Window.Left.Rows.All(row => row.Name != "external.txt"));
            File.Delete(Path.Combine(c.LeftPath, "renamed.txt"));
            await WaitAsync(() => c.Window.Left.Rows.All(row => row.Name != "renamed.txt"));
            File.WriteAllText(Path.Combine(c.RightPath, "right-external.txt"), "Other panel");
            await WaitAsync(() => c.Window.Right.Rows.Any(row => row.Name == "right-external.txt"));
            Equal("left", c.Window.ActivePanel.Id, "Automatic updates do not steal panel focus");
        }),
        new("refinement-live-refresh-defers-rename", "UC17; UC20", async c =>
        {
            using var updates = new DirectoryUpdates(c.Window); updates.Start();
            var gate = c.FileSystem.PauseNextRead(c.LeftPath);
            c.AddFile("arriving.txt", "External change");
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            c.PressKey(Key.F6, RawInputModifiers.Shift);
            gate.Release.TrySetResult(); await c.SettleAsync();
            Check(c.Window.Left.EditingRow != null && c.Window.Left.RenameEditor?.IsFocused == true, "A late directory read cannot replace an active rename editor.");
            Check(c.Window.Left.Rows.All(row => row.Name != "arriving.txt"), "Applying the refresh waits for editing to end.");
            await c.KeyAsync(Key.Escape);
            await WaitAsync(() => c.Window.Left.Rows.Any(row => row.Name == "arriving.txt"));
        }),
        new("refinement-live-deleted-directory", "UC20", async c =>
        {
            await c.SelectAsync("folder2"); await c.KeyAsync(Key.Enter);
            using var updates = new DirectoryUpdates(c.Window); updates.Start();
            await updates.PollAsync();
            Directory.Delete(Path.Combine(c.LeftPath, "folder2"));
            await WaitAsync(() => c.Window.Left.Session.DirectoryPath == c.LeftPath);
            Check(c.Window.Left.Rows.All(row => row.Name != "folder2"), "Removing the displayed directory automatically opens its accessible parent.");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Automatic parent fallback restores keyboard focus.");
            await c.KeyAsync(Key.Down);
            Equal("folder1", c.Window.Left.Current?.Name, "Keyboard navigation still works after the displayed directory disappears");
        }),
        new("refinement-live-fallback-does-not-steal-focus", "UC20", async c =>
        {
            await c.SelectAsync("folder2"); await c.KeyAsync(Key.Enter);
            var directory = c.Window.Left.Session.DirectoryPath;
            using var updates = new DirectoryUpdates(c.Window); updates.Start(); await updates.PollAsync();
            var gate = c.FileSystem.PauseNextRead(directory);
            Directory.Delete(directory); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            c.PressKey(Key.Tab); gate.Release.TrySetResult(); await c.SettleAsync();
            await WaitAsync(() => c.Window.Left.Session.DirectoryPath == c.LeftPath);
            Check(c.Window.ActivePanel == c.Window.Right && c.Window.Right.Table.IsKeyboardFocusWithin, "A delayed parent fallback leaves focus in the panel the user switched to.");
        }),
        new("refinement-centered-cells-and-column-reset", "UC13; 34116a4", async c =>
        {
            c.Window.Left.Table.Columns[0].Width = new GridLength(120);
            c.Window.Left.Table.Columns[2].Width = new GridLength(250);
            c.Window.Left.ResetColumns(); await c.SettleAsync();
            Check(c.Window.Left.Table.Columns[2].ActualWidth >= 112, "The size column fits a ten-gigabyte byte count.");
            Check(c.Window.Left.Table.Columns[^1].Width.IsAbsolute && c.Window.Left.Table.Columns[^1].CanUserResize != false, "The Modified column has its own resizable width.");
            var header = c.Window.Left.Table.GetVisualDescendants().OfType<TableViewColumnHeader>().Last();
            Check(header.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>().Any(thumb => thumb.IsVisible), "The Modified divider is visible.");
            var row = c.Row("alpha.txt");
            var texts = row.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Text is "TXT" or "3").ToArray();
            Check(texts.Length >= 2 && texts.All(text => Math.Abs(text.TranslatePoint(new Avalonia.Point(0, text.Bounds.Height / 2), row)!.Value.Y - row.Bounds.Height / 2) < 2), "Type and size values are visually centered within the file row.");
            var history = Button(c.Window, "leftHistory");
            Equal("History", history.Content, "History has no dropdown arrow");
            Check(ToolTip.GetTip(history)?.ToString()?.Contains("Alt+Down") == true, "Hover help explains the history shortcut.");
        }),
        new("refinement-enter-selects-parent", "UC20", async c =>
        {
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter);
            Equal("..", c.Window.Left.Current?.Name, "Entering a directory selects its parent entry");
            await c.KeyAsync(Key.Enter);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "A second Enter returns to the directory we left");
            Equal("folder1", c.Window.Left.Current?.Name, "Returning selects the child directory");
        }),
        new("refinement-system-icons-cache-capacity", "UC20; folder icons remain visible after browsing more than 512 paths", async c =>
        {
            for (var index = 0; index < 540; index++)
                Directory.CreateDirectory(Path.Combine(c.LeftPath, $"icon-folder-{index:D4}"));
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.KeyAsync(Key.Home, RawInputModifiers.Control);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var page = 0; page < 100; page++)
            {
                await c.Window.Left.Icons.Completion;
                foreach (var image in c.Window.Left.Table.GetVisualDescendants().OfType<Image>().Where(image => image.DataContext is FileRow))
                {
                    var row = (FileRow)image.DataContext!;
                    Check(image.Source != null, "Every realised row has an icon: " + row.Name);
                    if (row.Entry.IsDirectory) seen.Add(row.Entry.FullPath);
                }
                if (c.Window.Left.Table.SelectedIndex == c.Window.Left.Rows.Count - 1) break;
                await c.KeyAsync(Key.PageDown);
            }
            Check(seen.Count > 512, "Browsing exercised more unique folder icons than the cache capacity");
            await FeedbackRemarksProbe.CheckNativeIconsAsync(c);
            await c.Window.Left.LoadAsync(c.RightPath); await c.SettleAsync();
            await c.Window.Left.Icons.Completion;
            Check(c.Row("destination.txt").GetVisualDescendants().OfType<Image>().Any(image => image.Source != null), "New file types still have icons after the cache fills");
        }),
        new("refinement-system-icons", "UC20; native integration", async c =>
        {
            var folder = await Task.Run(() => SystemIcons.Read(Path.Combine(c.LeftPath, "folder1"), true));
            var file = await Task.Run(() => SystemIcons.Read(Path.Combine(c.LeftPath, "alpha.txt"), false));
            Check(folder.Bytes.Any(value => value != 0) && file.Bytes.Any(value => value != 0), "Windows returns real folder and file icon pixels.");
            await c.Window.Left.Icons.Completion; await c.SettleAsync();
            foreach (var name in new[] { "folder1", "alpha.txt" })
                Check(c.Row(name).GetVisualDescendants().OfType<Image>().Any(image => image.Source != null), "The actual name cell displays a native system icon for " + name);
            await c.ScreenshotAsync("system-icons");
        }),
        new("refinement-alt-search-across-layouts", "UC03; native integration", async c =>
        {
            using var layouts = new KeyboardLayoutFixture();
            Check(KeyboardLayouts.PrefixCandidates("еуые").Contains("test", StringComparer.OrdinalIgnoreCase), "Windows layout translation maps the same physical keys from Russian to English.");
            c.AddFile("test.txt", "Layout-aware search"); await c.KeyAsync(Key.R, RawInputModifiers.Control);
            c.Input.KeyDown(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
            await c.TextAsync("еуые");
            c.Input.KeyUp(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null);
            Equal("test.txt", c.Window.Left.Current?.Name, "Alt search finds an English name typed with Russian keys");
            Equal(10, c.Window.Left.Rows.Count(row => !row.Entry.IsParent), "Name jumping does not filter the file list");
        }),
        new("refinement-alt-search-without-text-events", "UC03; physical input", async c =>
        {
            using var layouts = new KeyboardLayoutFixture();
            c.AddFile("test.txt", "Physical key input"); await c.KeyAsync(Key.R, RawInputModifiers.Control);
            c.Input.KeyDown(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
            foreach (var (key, physical, symbol) in new[] { (Key.T, PhysicalKey.T, "е"), (Key.E, PhysicalKey.E, "у"), (Key.S, PhysicalKey.S, "ы"), (Key.T, PhysicalKey.T, "е") })
            {
                c.Input.KeyDown(key, RawInputModifiers.Alt, physical, symbol);
                c.Input.KeyUp(key, RawInputModifiers.Alt, physical, symbol);
            }
            c.Input.KeyUp(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null); await c.SettleAsync();
            Equal("test.txt", c.Window.Left.Current?.Name, "Alt keys find the name even when Windows sends no text events");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin && !c.Window.IsMenuOpen, "Alt search keeps focus in the file panel.");
        }),
        new("refinement-alt-search-deduplicates-text-events", "UC03; physical input", async c =>
        {
            c.AddFile("test.txt", "Physical and text input"); await c.KeyAsync(Key.R, RawInputModifiers.Control);
            c.Input.KeyDown(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
            foreach (var (key, physical, symbol) in new[] { (Key.T, PhysicalKey.T, "t"), (Key.E, PhysicalKey.E, "e"), (Key.S, PhysicalKey.S, "s"), (Key.T, PhysicalKey.T, "t") })
            {
                c.Input.KeyDown(key, RawInputModifiers.Alt, physical, symbol); c.Input.Text(symbol);
                c.Input.KeyUp(key, RawInputModifiers.Alt, physical, symbol);
            }
            c.Input.KeyUp(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null); await c.SettleAsync();
            Equal("test.txt", c.Window.Left.Current?.Name, "A key and its matching text event add one search character");
        }),
        new("refinement-alt-release-and-rejected-letter", "UC03", async c =>
        {
            c.AddFile("test.txt", "Search after Alt release"); await c.KeyAsync(Key.R, RawInputModifiers.Control);
            c.Input.KeyDown(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
            c.Input.KeyDown(Key.T, RawInputModifiers.Alt, PhysicalKey.T, "t");
            c.Input.KeyUp(Key.T, RawInputModifiers.Alt, PhysicalKey.T, "t");
            c.Input.KeyUp(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null);
            c.Input.KeyDown(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
            c.Input.Text("e");
            c.Input.KeyUp(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
            await c.SettleAsync();
            Equal("te", c.Window.NameSearchPrefix, "Plain input continues the prefix after Alt release");
            await c.TextAsync("z"); Equal("te", c.Window.NameSearchPrefix, "An unmatched letter leaves the prefix unchanged");
            await c.TextAsync("st"); Equal("test", c.Window.NameSearchPrefix, "Search continues after rejecting an unmatched letter");
            Equal("test.txt", c.Window.Left.Current?.Name, "The matching file stays selected");
            Check(!c.Window.Readiness.CanRestart, "An active name search defers automatic restart.");
            await c.KeyAsync(Key.Up); Check(c.Window.IsNameSearchActive, "Arrow navigation keeps the search and visits matching files.");
            await c.KeyAsync(Key.LeftAlt); await c.TextAsync("a"); await c.KeyAsync(Key.Escape);
            Check(!c.Window.IsNameSearchActive && c.Window.NameSearchPrefix.Length == 0, "Escape clears the search.");
            await c.TextAsync("test"); Check(!c.Window.IsNameSearchActive, "Ordinary typing cannot restart a dismissed search.");
        }),
        new("refinement-search-action-reset", "UC03", async c =>
        {
            await c.KeyAsync(Key.LeftAlt); await c.TextAsync("a");
            await c.ClickAsync(c.Row("file2.log")); Check(!c.Window.IsNameSearchActive, "Pointer navigation clears the prefix.");
            await c.KeyAsync(Key.LeftAlt); await c.TextAsync("a");
            await c.KeyAsync(Key.Tab); Check(!c.Window.IsNameSearchActive, "Switching panels clears the prefix.");
            await c.KeyAsync(Key.LeftAlt); await c.TextAsync("d");
            await c.KeyAsync(Key.F12); Check(!c.Window.IsNameSearchActive && c.Window.Feedback.IsPicking, "Starting feedback ends the search.");
            await c.KeyAsync(Key.Escape);
        }),
        new("refinement-history-single-selection", "UC15", async c =>
        {
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter); await c.KeyAsync(Key.Back);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt); var dialog = c.Window.ActiveNavigationDialog!;
            Check(dialog.Directories.SelectedIndex >= 0 && dialog.Favorites.SelectedIndex == -1, "Only the history column starts selected.");
            await c.KeyAsync(Key.Right, target: dialog);
            Check(dialog.Directories.SelectedIndex == -1 && dialog.Favorites.SelectedIndex >= 0, "Switching columns moves the only selection.");
            await c.KeyAsync(Key.Left, target: dialog);
            Check(dialog.Favorites.SelectedIndex == -1 && dialog.Directories.SelectedIndex >= 0, "Returning restores the history cursor without a second selection.");
            await c.ClickAsync((Control)dialog.Favorites.ContainerFromIndex(0)!, target: dialog);
            Check(dialog.Directories.SelectedIndex == -1 && dialog.Favorites.SelectedIndex == 0, "Pointer selection also clears the other column.");
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("refinement-all-window-layouts", "UC20; visual review", async c =>
        {
            await c.Window.Left.Icons.Completion; await c.Window.Right.Icons.Completion;
            await c.ScreenshotAsync("main");
            for (var i = 0; i < 16; i++) c.Window.DirectoryUsage.Visit(Path.Combine(c.FixturePath, "project-" + i), DateTimeOffset.UtcNow);
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter); await c.KeyAsync(Key.Back);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt); var history = c.Window.ActiveNavigationDialog!;
            await c.ScreenshotAsync("history", history);
            Check(((Control)history.Favorites.ContainerFromIndex(0)!).Bounds.Height <= 32, "Compact history rows fit at least twelve locations in the dialog.");
            await c.KeyAsync(Key.Escape, target: history);
            await c.KeyAsync(Key.F7); var folder = c.Window.ActiveDialog!;
            await c.ScreenshotAsync("new-folder", folder);
            await c.KeyAsync(Key.Enter, target: folder); await c.ScreenshotAsync("new-folder-error", folder);
            Check(folder.Bounds.Height < 260, "The folder form fits its content without a large blank area.");
            await c.KeyAsync(Key.Escape, target: folder);
            await c.KeyAsync(Key.F12); await c.ClickAsync(c.Row("alpha.txt")); var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Keep this window compact and align its controls.", comment);
            await c.ScreenshotAsync("comment", comment);
            Check(comment.Bounds.Height < 340, "The comment form fits its input and actions.");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            await c.ClickAsync(Button(c.Window, "ShowNotes"));
            await c.ScreenshotAsync("notes", c.Window.Feedback.ActiveNotes!);
            await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveNotes);
        }),
        new("refinement-resolution-keeps-original-comment", "UC39; feedback resolution", async c =>
        {
            var root = Path.Combine(c.FixturePath, "releases"); AddRelease(root, NewId, false);
            var issues = new IssueStore(Path.Combine(c.FixturePath, "issues"));
            issues.Add(new("known", FeedbackIssue.New, "Original feedback text", new("left.sort.name", "Name"), OldId));
            issues.Add(new("unrelated", FeedbackIssue.New, "Keep this open", new("left.files", "Files"), OldId));
            issues.Change("start", ["known"]); issues.Change("close", ["known"]);
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json")); await store.SaveAsync(c.Window.CaptureWorkspace());
            using var lifecycle = new WorkspaceLifecycle(c.Window, store, new ReleaseCatalog(root, NewId, "Xcmd.Probe.exe"));
            await lifecycle.InitializeAsync([]);
            var notes = await issues.ReadAsync();
            Equal("Original feedback text", notes.Single(item => item.Id == "known").Text, "Resolution keeps the original comment text");
            Equal(FeedbackIssue.Closed, notes.Single(item => item.Id == "known").Status, "Closing does not depend on launching a new release");
            Equal(FeedbackIssue.New, notes.Single(item => item.Id == "unrelated").Status, "Other comments remain new");
            Equal("Original feedback text", issues.Read().Single(item => item.Id == "known").Text, "Closed issues keep original feedback.");
        })
    ];
}

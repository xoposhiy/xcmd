using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.NavigationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class NavigationAndColorsScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("navigation-shared-window-and-shortcuts", "UC15; UC16; combined navigation", async c =>
        {
            var dialog = await OpenHistoryAsync(c);
            Equal(DirectoryList.History, dialog.ActiveList, "Alt+Down starts in history"); CheckOneSelection(dialog);
            await c.KeyAsync(Key.D, RawInputModifiers.Control, dialog);
            Check(ReferenceEquals(dialog, c.Window.ActiveNavigationDialog), "Ctrl+D reuses the same window.");
            Equal(DirectoryList.Favorites, dialog.ActiveList, "Ctrl+D selects favorites"); CheckOneSelection(dialog);
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt, dialog);
            Equal(DirectoryList.History, dialog.ActiveList, "Alt+Down returns to history"); CheckOneSelection(dialog);
            await c.KeyAsync(Key.Escape, target: dialog);
            await c.KeyAsync(Key.D, RawInputModifiers.Control); dialog = c.Window.ActiveNavigationDialog!;
            Equal(DirectoryList.Favorites, dialog.ActiveList, "Opening with Ctrl+D starts in favorites");
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal(c.RightPath, c.Window.Left.Session.DirectoryPath, "Enter opens the only active favorite");
        }),
        new("navigation-tab-and-reverse-tab", "UC15; UC16; keyboard focus", async c =>
        {
            var dialog = await OpenHistoryAsync(c);
            await c.KeyAsync(Key.Tab, target: dialog); Check(dialog.Favorites.IsKeyboardFocusWithin, "Tab reaches favorites and frequent locations."); CheckOneSelection(dialog);
            await c.KeyAsync(Key.Tab, RawInputModifiers.Shift, dialog); Check(dialog.Directories.IsKeyboardFocusWithin, "Shift+Tab returns to history.");
            await c.KeyAsync(Key.Tab, target: dialog);
            Check(dialog.Favorites.IsKeyboardFocusWithin, "The next Tab reaches the combined list."); CheckOneSelection(dialog);
            await c.KeyAsync(Key.Tab, target: dialog); Check(dialog.NameEditor.IsFocused, "Tab continues to the favorite editor.");
            await c.KeyAsync(Key.Tab, RawInputModifiers.Shift, dialog); Check(dialog.Favorites.IsKeyboardFocusWithin, "Shift+Tab returns from the editor to favorites.");
            await c.KeyAsync(Key.Tab, target: dialog); await c.KeyAsync(Key.Tab, target: dialog);
            var add = FavoritesProbe.Button(dialog, "FavoriteAdd"); Check(add.IsKeyboardFocusWithin, "Tab continues to enabled action buttons.");
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("navigation-root-exclusion-and-attention", "UC15; meaningful frequent locations", c =>
        {
            var now = DateTimeOffset.UtcNow; var book = new DirectoryUsageBook();
            book.Restore([new(@"C:\", 10000, 10000, now), new(@"C:\project", 2, 2, now)]);
            book.Visit(@"C:\", now); book.Seed([@"D:\", @"C:\other"]);
            Check(book.Capture().All(item => item.Path is not (@"C:\" or @"D:\")), "Roots are excluded from stored ranking and suggestions.");
            Equal(@"C:\project", book.Suggest(@"C:\other", now)[0].Path, "A root cannot outrank working directories");
            book.Seed([@"C:\transit"]);
            Equal(@"C:\transit", book.Suggest(@"C:\other", now)[^1].Path, "Briefly opened directories follow scored ones.");
            book.Visit(@"C:\data.zip|", now); book.Seed([@"C:\data.zip|docs"]); book.Visit(GitPath.Log(@"C:\project", ""), now);
            Equal(2, book.Suggest(@"C:\other", now).Length, "Archive and Git locations are not frequent directories.");
            var attention = new DirectoryAttention();
            Equal(0, ObserveFor(attention, @"C:\transit", now, 2, true).Count(), "A short pass through a directory earns no visit");
            Equal(1, ObserveFor(attention, @"C:\project", now.AddSeconds(3), 20, true).Count(), "Ten active seconds earn one visit per stay");
            Equal(0, ObserveFor(attention, @"C:\background", now.AddMinutes(1), 30, false).Count(), "Background time earns no visits");
            Equal(1, ObserveFor(attention, @"C:\background", now.AddMinutes(2), 12, true).Count(), "Active work can qualify a background location later");
            var suspended = new DirectoryAttention(); suspended.Observe(@"C:\sleep", now, true);
            Check(suspended.Observe(@"C:\sleep", now.AddHours(1), true) == null, "A suspended timer cannot count a long sleep as work.");
            return Task.CompletedTask;
        }),
        new("navigation-legacy-ranking-migration", "UC15; UC38; ranking migration", async c =>
        {
            var state = c.Window.CaptureWorkspace() with { UsageVersion = 0, Usage = [new(@"C:\", 10000, 10000, DateTimeOffset.UtcNow)] };
            await c.Window.RestoreWorkspaceAsync(state);
            Check(c.Window.DirectoryUsage.Capture().All(item => item.Visits == 0), "Legacy transition counts do not become qualified work visits.");
            Equal(2, c.Window.CaptureWorkspace().UsageVersion, "New sessions save the ranking version");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Ranking migration preserves panel locations");
        }),
        new("navigation-two-column-rendering", "UC15; UC16; visual review", async c =>
        {
            var dialog = await OpenHistoryAsync(c);
            await c.ScreenshotAsync("navigation-history", dialog);
            await c.KeyAsync(Key.D, RawInputModifiers.Control, dialog); await c.ScreenshotAsync("navigation-favorites", dialog);
            dialog.Width = dialog.MinWidth; dialog.SetScaleForTest(1.5); await c.ScreenshotAsync("navigation-scaled", dialog);
            Check(dialog.Favorites.Bounds.Width > 300 && dialog.Directories.Bounds.Width > 300, "Both lists remain usable at the minimum width.");
            Equal(2, dialog.GetVisualDescendants().OfType<ListBox>().Count(), "Navigation has exactly two lists");
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("navigation-pinned-favorites-and-type-search", "UC15; UC16; pinned favorites, deduplication and immediate substring search", async c =>
        {
            var favorite = c.Window.Favorites.Add(c.RightPath);
            c.Window.Favorites.Rename(favorite.Id, "Working 日本語");
            c.Window.DirectoryUsage.Visit(c.RightPath, DateTimeOffset.UtcNow);
            var frequent = Path.Combine(c.LeftPath, "folder1");
            c.Window.DirectoryUsage.Visit(frequent, DateTimeOffset.UtcNow);
            var dialog = await FavoritesProbe.OpenAsync(c);
            Check(dialog.Favorites.Items[0] is FavoriteDirectory { Name: "Working 日本語" }, "Pinned favorites precede frequent locations");
            Equal(1, dialog.Favorites.Items.Cast<object>().Count(item => item is string path && path == frequent), "Frequent locations follow the pins");
            Check(!dialog.Favorites.Items.Cast<object>().OfType<string>().Contains(c.RightPath), "Pinned paths do not repeat among frequent locations");
            Check(dialog.Favorites.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "★"), "Pinned favorites show a star");
            await c.TextAsync("日本語", dialog);
            Check(dialog.Favorites.SelectedItem is FavoriteDirectory, "Typing finds a custom favorite name");
            await c.KeyAsync(Key.Escape, target: dialog);
            Check(dialog.IsVisible && dialog.NameSearchPrefix.Length == 0, "Escape clears the search first");
            await c.TextAsync("folder1", dialog);
            Equal(frequent, dialog.Favorites.SelectedItem as string, "Typing finds a frequent directory by substring");
            Check(!dialog.NameEditor.IsEnabled && !FavoritesProbe.Button(dialog, "FavoriteRemove").IsEnabled, "Frequent locations cannot accidentally edit or remove a pin");
            await c.TextAsync("☃", dialog);
            Equal("folder1", dialog.NameSearchPrefix, "Unmatched input is ignored");
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal(frequent, c.Window.Left.Session.DirectoryPath, "Enter opens the selected frequent directory");
        }),
        new("navigation-search-active-column-and-keyboard-layout", "UC15; UC16; first matching row, physical input and search reset", async c =>
        {
            using var layouts = new KeyboardLayoutFixture();
            var path = Path.Combine(c.LeftPath, "folder1");
            var first = c.Window.Favorites.Add(c.RightPath); c.Window.Favorites.Rename(first.Id, "My work");
            var second = c.Window.Favorites.Add(path); c.Window.Favorites.Rename(second.Id, "work");
            var dialog = await OpenHistoryAsync(c);
            await c.TextAsync("left", dialog);
            Equal(dialog.Directories.Items[0] as string, dialog.Directories.SelectedItem as string, "Typing begins at the uppermost match in history after Alt+Down");
            CheckOneSelection(dialog);
            await c.KeyAsync(Key.Right, target: dialog);
            Equal("", dialog.NameSearchPrefix, "Changing columns clears the query");
            await c.TextAsync("work", dialog);
            Equal(first.Id, (dialog.Favorites.SelectedItem as FavoriteDirectory)?.Id, "The uppermost substring match wins over a later prefix match");
            await c.KeyAsync(Key.Down, target: dialog);
            Equal(second.Id, (dialog.Favorites.SelectedItem as FavoriteDirectory)?.Id, "Down cycles through matches");
            await c.KeyAsync(Key.Up, target: dialog);
            Equal(first.Id, (dialog.Favorites.SelectedItem as FavoriteDirectory)?.Id, "Up cycles back through matches");
            await c.KeyAsync(Key.Escape, target: dialog);
            await c.KeyAsync(Key.W, target: dialog); await c.KeyAsync(Key.O, target: dialog);
            Equal("wo", dialog.NameSearchPrefix, "Physical key input works without Alt");
            await c.TextAsync("o", dialog);
            Equal("wo", dialog.NameSearchPrefix, "Text from the same physical key is not appended twice");
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt, dialog);
            Equal("", dialog.NameSearchPrefix, "The history shortcut clears the search");
            await c.KeyAsync(Key.D, RawInputModifiers.Control, dialog);
            await c.TextAsync("цщкл", dialog);
            Check(dialog.Favorites.SelectedItem is FavoriteDirectory, "The installed keyboard layouts are tried for typed text");
            Equal("цщкл", dialog.NameSearchPrefix, "Wrong-layout typing finds the Latin favorite name");
            await c.KeyAsync(Key.Escape, target: dialog); await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("colors-source-text-and-executable-priority", "UC19; configurable file groups", async c =>
        {
            await GitFixture.InitializeAsync(c.LeftPath); await GitFixture.RefreshAsync(c.Window.Left.Git);
            foreach (var name in new[] { "script.sh", "script.ps1", "script.cmd", "program.exe", "source.py", "readme.TXT", "Dockerfile", "unknown.pdf" }) c.AddFile(name, "File color fixture");
            Directory.CreateDirectory(Path.Combine(c.LeftPath, "folder.cs")); await c.KeyAsync(Key.R, RawInputModifiers.Control);
            foreach (var name in new[] { "script.sh", "script.ps1", "script.cmd", "source.py", "Dockerfile" }) Equal(Color.Parse("#C3ABF5"), TypeColor(c, name), "Source files use the source rule before executable or text rules");
            foreach (var name in new[] { "script.sh", "program.exe", "readme.TXT", "Dockerfile" }) Equal(Colors.White, FileColor(c, name), "File names use white regardless of their type");
            Equal(Color.Parse("#E9BD79"), TypeColor(c, "program.exe"), "Programs use amber in the Type column");
            Equal(Color.Parse("#8DD3E8"), TypeColor(c, "readme.TXT"), "Text types use blue and ignore extension case");
            Equal(Colors.White, FileColor(c, "unknown.pdf"), "Unmatched files keep the default color");
            Equal(Colors.White, TypeColor(c, "folder.cs"), "Directory names are not classified as source files");
            await c.SelectAsync("script.sh"); Equal(Colors.White, FileColor(c, "script.sh"), "Selected file text keeps contrast against the selection background");
            Equal(Color.Parse("#C3ABF5"), TypeColor(c, "script.sh"), "The current file keeps its type color in the Type column");
            await c.KeyAsync(Key.Insert);
            Equal(Color.Parse("#C3ABF5"), TypeColor(c, "script.sh"), "Marked files keep their type color in the Type column");
            Equal(Color.Parse("#E9BD79"), TypeColor(c, "program.exe"), "Executable extensions use the executable color");
            Equal(Color.Parse("#8DD3E8"), TypeColor(c, "readme.TXT"), "Text extensions use the text color");
            await c.SelectAsync("folder1"); await c.Window.Left.Icons.Completion; await c.ScreenshotAsync("file-colors");
        }),
        new("colors-config-reload-and-preservation", "UC19; persistent configuration", async c =>
        {
            await GitFixture.InitializeAsync(c.LeftPath); await GitFixture.RefreshAsync(c.Window.Left.Git);
            var settings = Path.Combine(c.FixturePath, "profile", "file-types.json");
            await FileTypeSettings.LoadAsync(settings);
            var custom = "{\"rules\":[{\"name\":\"custom\",\"color\":\"#91E0A1\",\"extensions\":[\".txt\"],\"fileNames\":[]}]}";
            await File.WriteAllTextAsync(settings, custom); c.Window.FileTypeSettingsPath = settings;
            var menu = c.Window.GetVisualDescendants().OfType<Menu>().Single();
            await c.ClickAsync((MenuItem)menu.Items[3]!); await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Enter);
            Equal(Colors.White, FileColor(c, "alpha.txt"), "Changing file-type colors keeps file names white");
            Equal(Color.Parse("#91E0A1"), TypeColor(c, "alpha.txt"), "Reloading file types updates existing Type cells");
            await FileTypeSettings.LoadAsync(settings); Equal(custom, await File.ReadAllTextAsync(settings), "Loading a new release preserves custom rules");
            await File.WriteAllTextAsync(settings, "{broken"); await c.Window.ReloadFileTypesAsync();
            Equal(Color.Parse("#91E0A1"), TypeColor(c, "alpha.txt"), "Invalid edits keep the last valid colors");
            Equal("{broken", await File.ReadAllTextAsync(settings), "Invalid user settings are kept for correction");
            await File.WriteAllTextAsync(settings, "{\"rules\":[{\"name\":\"bad\",\"color\":\"green\",\"extensions\":[\".txt\"],\"fileNames\":[]}]}");
            await c.Window.ReloadFileTypesAsync();
            Equal(Color.Parse("#91E0A1"), TypeColor(c, "alpha.txt"), "A rule with an invalid color keeps the last valid colors");
        }),
        new("colors-rename-changes-group", "UC19; UC17; updated file category", async c =>
        {
            await GitFixture.InitializeAsync(c.LeftPath); await GitFixture.RefreshAsync(c.Window.Left.Git);
            await c.BeginRenameAsync("alpha.txt"); await c.TextAsync("alpha.cs"); await c.KeyAsync(Key.Enter); await c.SelectAsync("folder1");
            Equal(Color.Parse("#C3ABF5"), TypeColor(c, "alpha.cs"), "Renaming an extension updates its color group");
            Equal(Colors.White, FileColor(c, "alpha.cs"), "Renamed file names remain white");
        }),
        new("layout-panel-strip-and-type-header", "UC20; b4fcf9cd", async c =>
        {
            Equal(new Thickness(0, 2, 0, 0), c.Window.Left.BorderThickness, "Only the top edge indicates panel activity");
            Equal(Colors.Transparent, ((ISolidColorBrush)c.Window.Right.BorderBrush!).Color, "The inactive panel has no visible outline");
            var button = WorkspaceProbe.Button(c.Window, "leftSortExtension");
            var header = button.GetVisualAncestors().OfType<TableViewColumnHeader>().Single();
            var label = button.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Type");
            var center = label.TranslatePoint(new Point(label.Bounds.Width / 2, 0), header)!.Value.X;
            Check(Math.Abs(center - header.Bounds.Width / 2) < 2, "The Type heading is centered in its column.");
            var row = c.Row("alpha.txt");
            var kind = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "TXT");
            var cellCenter = kind.TranslatePoint(new Point(kind.Bounds.Width / 2, 0), c.Window.Left.Table)!.Value.X;
            var headingCenter = label.TranslatePoint(new Point(label.Bounds.Width / 2, 0), c.Window.Left.Table)!.Value.X;
            Check(Math.Abs(cellCenter - headingCenter) < 2, "The Type heading and file types share the same horizontal center.");
            await c.KeyAsync(Key.Tab); Equal(Colors.Transparent, ((ISolidColorBrush)c.Window.Left.BorderBrush!).Color, "The activity strip moves when switching panels");
            await c.ScreenshotAsync("panel-activity-strip");
        }),
        new("layout-application-icon", "Application identity; executable and window icon", async c =>
        {
            Check(c.Window.Icon != null, "The main window has the packaged application icon.");
            var nativeIcon = await Task.Run(() => SystemIcons.Read(Path.Combine(AppContext.BaseDirectory, "Xcmd.Desktop.exe"), false));
            Check(nativeIcon.Bytes.Any(value => value != 0), "Windows reads the actual executable icon.");
            await c.KeyAsync(Key.F7);
            Check(c.Window.ActiveDialog!.Icon == c.Window.Icon, "Owned dialogs use the same application icon.");
            await c.KeyAsync(Key.Escape, target: c.Window.ActiveDialog);
        })
    ];
}

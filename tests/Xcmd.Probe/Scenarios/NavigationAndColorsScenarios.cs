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
            await c.KeyAsync(Key.Tab, target: dialog); Check(dialog.Suggestions.IsKeyboardFocusWithin, "Tab reaches frequent locations."); CheckOneSelection(dialog);
            await c.KeyAsync(Key.Tab, RawInputModifiers.Shift, dialog); Check(dialog.Directories.IsKeyboardFocusWithin, "Shift+Tab returns to history.");
            await c.KeyAsync(Key.Tab, target: dialog); await c.KeyAsync(Key.Tab, target: dialog);
            Check(dialog.Favorites.IsKeyboardFocusWithin, "The next Tab reaches favorites."); CheckOneSelection(dialog);
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
        new("navigation-three-column-rendering", "UC15; UC16; visual review", async c =>
        {
            var dialog = await OpenHistoryAsync(c);
            await c.ScreenshotAsync("navigation-history", dialog);
            await c.KeyAsync(Key.D, RawInputModifiers.Control, dialog); await c.ScreenshotAsync("navigation-favorites", dialog);
            dialog.Width = dialog.MinWidth; dialog.SetScaleForTest(1.5); await c.ScreenshotAsync("navigation-scaled", dialog);
            Check(dialog.Favorites.Bounds.Width > 200 && dialog.Suggestions.Bounds.Width > 200, "All three lists remain usable at the minimum width.");
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("colors-source-text-and-executable-priority", "UC19; configurable file groups", async c =>
        {
            foreach (var name in new[] { "script.sh", "script.ps1", "script.cmd", "program.exe", "source.py", "readme.TXT", "Dockerfile", "unknown.pdf" }) c.AddFile(name, "File color fixture");
            Directory.CreateDirectory(Path.Combine(c.LeftPath, "folder.cs")); await c.KeyAsync(Key.R, RawInputModifiers.Control);
            foreach (var name in new[] { "script.sh", "script.ps1", "script.cmd", "source.py", "Dockerfile" }) Equal(Color.Parse("#C3ABF5"), FileColor(c, name), "Source files use the source rule before executable or text rules");
            Equal(Color.Parse("#E9BD79"), FileColor(c, "program.exe"), "Programs use amber");
            Equal(Color.Parse("#8DD3E8"), FileColor(c, "readme.TXT"), "Text files use blue and ignore extension case");
            Equal(Colors.White, FileColor(c, "unknown.pdf"), "Unmatched files keep the default color");
            Equal(Colors.White, FileColor(c, "folder.cs"), "Directory names are not classified as source files");
            await c.SelectAsync("script.sh"); Equal(Colors.White, FileColor(c, "script.sh"), "Selected file text keeps contrast against the selection background");
            await c.SelectAsync("folder1"); await c.Window.Left.Icons.Completion; await c.ScreenshotAsync("file-colors");
        }),
        new("colors-config-reload-and-preservation", "UC19; persistent configuration", async c =>
        {
            var settings = Path.Combine(c.FixturePath, "profile", "file-types.json");
            await FileTypeSettings.LoadAsync(settings);
            var custom = "{\"rules\":[{\"name\":\"custom\",\"color\":\"#91E0A1\",\"extensions\":[\".txt\"],\"fileNames\":[]}]}";
            await File.WriteAllTextAsync(settings, custom); c.Window.FileTypeSettingsPath = settings;
            var menu = c.Window.GetVisualDescendants().OfType<Menu>().Single();
            await c.ClickAsync((MenuItem)menu.Items[3]!); await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Enter);
            Equal(Color.Parse("#91E0A1"), FileColor(c, "alpha.txt"), "The View command applies edited rules to existing rows");
            await FileTypeSettings.LoadAsync(settings); Equal(custom, await File.ReadAllTextAsync(settings), "Loading a new release preserves custom rules");
            await File.WriteAllTextAsync(settings, "{broken"); await c.Window.ReloadFileTypesAsync();
            Equal(Color.Parse("#91E0A1"), FileColor(c, "alpha.txt"), "Invalid edits keep the last valid colors");
            Equal("{broken", await File.ReadAllTextAsync(settings), "Invalid user settings are kept for correction");
        }),
        new("colors-rename-changes-group", "UC19; UC17; updated file category", async c =>
        {
            await c.BeginRenameAsync("alpha.txt"); await c.TextAsync("alpha.cs"); await c.KeyAsync(Key.Enter); await c.SelectAsync("folder1");
            Equal(Color.Parse("#C3ABF5"), FileColor(c, "alpha.cs"), "Renaming an extension updates its color group");
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

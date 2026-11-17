// @tests RecentLocations, FavoriteLocations, RestoreWorkspace, ReportControl, TryLocalRelease
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FavoritesProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class FavoriteScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("favorites-add-rename-and-remove", "UC16; keyboard workflow", async c =>
        {
            var dialog = await OpenAsync(c);
            Equal(0, dialog.Favorites.Items.OfType<FavoriteDirectory>().Count(), "A new profile has no sample favorites");
            await c.ScreenshotAsync("empty-favorites", dialog);
            await c.KeyAsync(Key.Insert, target: dialog);
            Equal(c.LeftPath, c.Window.Favorites.Items.Single().Path, "Insert adds the current directory");
            await c.KeyAsync(Key.Insert, target: dialog);
            Equal(1, c.Window.Favorites.Items.Count, "Adding the current directory twice selects the existing favorite");
            await RenameAsync(c, dialog, "Working files");
            Equal("Working files", c.Window.Favorites.Items.Single().Name, "Enter saves the favorite name");
            Check(dialog.Favorites.IsKeyboardFocusWithin, "Saving a name returns to list navigation.");
            await c.ScreenshotAsync("named-favorite", dialog);
            await c.KeyAsync(Key.Delete, target: dialog);
            Equal(0, c.Window.Favorites.Items.Count, "Delete removes the favorite");
            Check(Directory.Exists(c.LeftPath), "Removing a favorite keeps the directory and its files.");
            await c.KeyAsync(Key.Escape, target: dialog);
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Closing favorites returns focus to the source panel.");
        }),
        new("favorites-rename-validation", "UC16; name editing", async c =>
        {
            c.Window.Favorites.Add(c.LeftPath); var dialog = await OpenAsync(c);
            await RenameAsync(c, dialog, "   ");
            Check(dialog.Error != null && c.Window.Favorites.Items.Single().Name == "left", "An empty label keeps the saved name and shows an error.");
            await c.TextAsync("Project 日本語", dialog); await c.KeyAsync(Key.Enter, target: dialog);
            Equal("Project 日本語", c.Window.Favorites.Items.Single().Name, "A corrected Unicode label can be saved");
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("favorites-shared-navigation-and-cursor", "UC16; UC15; panel focus", async c =>
        {
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter); await c.SelectAsync("inside.txt");
            var dialog = await OpenAsync(c); await c.KeyAsync(Key.Insert, target: dialog); await c.KeyAsync(Key.Escape, target: dialog);
            await c.KeyAsync(Key.Back); dialog = await OpenAsync(c); await c.KeyAsync(Key.Enter, target: dialog);
            Equal("inside.txt", c.Window.Left.Current?.Name, "Returning through favorites restores the directory cursor");
            await c.KeyAsync(Key.Tab); dialog = await OpenAsync(c);
            Equal(1, dialog.Favorites.Items.OfType<FavoriteDirectory>().Count(), "Both panels share the same favorite list");
            await c.ClickAsync(Button(dialog, "NavigationOpen"), target: dialog);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Right.Session.DirectoryPath, "A favorite opens in the panel that requested it");
            Check(c.Window.Right.Table.IsKeyboardFocusWithin && c.Window.ActivePanel == c.Window.Right, "The source panel keeps keyboard focus.");
            Equal("inside.txt", c.Window.Left.Current?.Name, "The other panel keeps its file cursor");
        }),
        new("favorites-cancel-preserves-workspace", "UC16; selection", async c =>
        {
            c.Window.Favorites.Add(c.RightPath);
            await c.ClickAsync(c.Row("alpha.txt")); await c.ClickAsync(c.Row("file2.log"), RawInputModifiers.Control);
            var dialog = await OpenAsync(c);
            Check(!c.Window.Readiness.CanRestart && c.Window.Readiness.DialogOpen, "An open favorite dialog defers automatic updates.");
            await c.ClickAsync(Button(dialog, "NavigationCancel"), target: dialog);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Cancel keeps the directory");
            Equal("file2.log", c.Window.Left.Current?.Name, "Cancel keeps the cursor");
            Check(c.Window.Left.SelectedNames().Order().SequenceEqual(new[] { "alpha.txt", "file2.log" }), "Cancel keeps multiple selected files.");
        }),
        new("favorites-missing-directory-and-retry", "UC16; unavailable location", async c =>
        {
            var path = Path.Combine(c.LeftPath, "folder2"); c.Window.Favorites.Add(path); Directory.Delete(path);
            var dialog = await OpenAsync(c); await c.KeyAsync(Key.Enter, target: dialog);
            Check(!dialog.IsVisible && dialog.Error == null, "A missing favorite opens its existing parent without an error.");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "A missing favorite opens the nearest existing parent");
            Equal(path, c.Window.Favorites.Items.Single().Path, "Fallback preserves the saved favorite path");
            Directory.CreateDirectory(path); dialog = await OpenAsync(c); await c.KeyAsync(Key.Enter, target: dialog);
            Equal(path, c.Window.Left.Session.DirectoryPath, "A saved favorite opens the original directory when it exists again");
        }),
        new("favorites-cancel-pending-navigation", "UC16; asynchronous cancellation", async c =>
        {
            var path = Path.Combine(c.LeftPath, "folder1"); c.Window.Favorites.Add(path);
            var dialog = await OpenAsync(c); var gate = c.FileSystem.PauseNextRead(path);
            c.PressKey(Key.Enter, target: dialog); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(dialog.IsOpening && !dialog.Favorites.IsEnabled, "A pending favorite disables repeated submission.");
            c.PressKey(Key.Enter, target: dialog); c.PressKey(Key.Escape, target: dialog); await c.SettleAsync();
            Check(gate.WasCancelled && c.Window.ActiveNavigationDialog == null, "Escape cancels a pending favorite read.");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "A cancelled read cannot change the panel later");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Cancellation restores panel focus.");
        }),
        new("favorites-pending-navigation-once", "UC16; asynchronous completion", async c =>
        {
            var path = Path.Combine(c.LeftPath, "folder1"); c.Window.Favorites.Add(path);
            var dialog = await OpenAsync(c); var gate = c.FileSystem.PauseNextRead(path);
            c.PressKey(Key.Enter, target: dialog); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            c.PressKey(Key.Enter, target: dialog); gate.Release.TrySetResult(); await c.SettleAsync();
            Check(c.Window.ActiveNavigationDialog == null && !gate.WasCancelled, "The original read finishes and closes favorites.");
            Equal(path, c.Window.Left.Session.DirectoryPath, "The selected favorite opens once");
            Equal(1, c.Window.Left.Session.PreviousDirectories.Count, "Repeated Enter creates one history transition");
        }),
        new("favorites-immediate-persistence-and-legacy-session", "UC16; UC38; saved preferences", async c =>
        {
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json"));
            using var lifecycle = new WorkspaceLifecycle(c.Window, store, null);
            await lifecycle.InitializeAsync(new[] { c.LeftPath, c.RightPath });
            var dialog = await OpenAsync(c); await c.KeyAsync(Key.Insert, target: dialog); await lifecycle.PendingWork;
            Equal(c.LeftPath, (await store.ReadAsync())!.Favorites!.Single().Path, "Adding a favorite saves it immediately");
            await RenameAsync(c, dialog, "Current work"); await lifecycle.PendingWork;
            var state = (await store.ReadAsync())!;
            Equal("Current work", state.Favorites!.Single().Name, "Renaming is saved immediately");
            await c.KeyAsync(Key.Escape, target: dialog);
            c.Window.Favorites.Restore([]); await c.Window.RestoreWorkspaceAsync(state);
            Equal("Current work", c.Window.Favorites.Items.Single().Name, "Session restoration keeps custom labels");
            lifecycle.Dispose();
            using var pathsLifecycle = new WorkspaceLifecycle(c.Window, store, null);
            await pathsLifecycle.InitializeAsync(new[] { c.RightPath, c.LeftPath });
            Equal("Current work", c.Window.Favorites.Items.Single().Name, "Explicit panel paths retain saved favorites");
            dialog = await OpenAsync(c); await c.KeyAsync(Key.Delete, target: dialog); await pathsLifecycle.PendingWork;
            Equal(0, (await store.ReadAsync())!.Favorites!.Length, "Removing a favorite is saved immediately");
            await c.KeyAsync(Key.Escape, target: dialog);
            var legacyJson = JsonSerializer.SerializeToNode(state)!; legacyJson.AsObject().Remove("Favorites");
            await c.Window.RestoreWorkspaceAsync(legacyJson.Deserialize<WorkspaceState>()!);
            Equal(0, c.Window.Favorites.Items.Count, "Older sessions restore with an empty favorite list");
        }),
        new("favorites-feedback-keeps-name-draft", "UC16; UC39; nested feedback", async c =>
        {
            c.Window.Favorites.Add(c.LeftPath); var dialog = await OpenAsync(c);
            await c.KeyAsync(Key.F2, target: dialog); await c.TextAsync("Unfinished favorite name", dialog);
            await c.KeyAsync(Key.F12, target: dialog); await c.ClickAsync(dialog.NameEditor, target: dialog);
            var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Keep this editor aligned with the list.", comment);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            Equal("Unfinished favorite name", dialog.NameEditor.Text, "Feedback preserves the favorite name draft");
            Equal("left", c.Window.Favorites.Items.Single().Name, "Feedback does not submit the name change");
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal("Unfinished favorite name", c.Window.Favorites.Items.Single().Name, "Name editing continues after feedback");
            await c.ScreenshotAsync("favorite-feedback", dialog);
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("favorites-scaled-layout-and-pointer-navigation", "UC16; UC20; visual review", async c =>
        {
            c.Window.Favorites.Add(c.RightPath);
            for (var i = 0; i < 18; i++) c.Window.Favorites.Add(Path.Combine(c.LeftPath, "Project 日本語 " + i));
            var dialog = await OpenAsync(c); await c.ScreenshotAsync("favorites", dialog);
            dialog.SetScaleForTest(1.5); dialog.Width = dialog.MinWidth;
            await c.ScreenshotAsync("favorites-scaled", dialog);
            dialog.SetScaleForTest(1); dialog.Width = 780; await c.SettleAsync();
            var row = (Control)dialog.Favorites.ContainerFromIndex(0)!;
            await c.ClickAsync(row, target: dialog); await c.ClickAsync(row, target: dialog);
            Equal(c.RightPath, c.Window.Left.Session.DirectoryPath, "Double-click opens the pointed favorite");
        })
    ];
}

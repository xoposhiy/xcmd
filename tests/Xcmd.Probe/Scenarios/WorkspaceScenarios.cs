using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Scenarios;

public static class WorkspaceScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("workspace-session-roundtrip", "UC38; Session restore; UC15; UC20", async c =>
        {
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter); await c.SelectAsync("inside.txt"); await c.KeyAsync(Key.Back);
            await c.ClickAsync(c.Row("alpha.txt")); await c.ClickAsync(c.Row("file2.log"), RawInputModifiers.Control);
            await c.ClickHeaderAsync("Size");
            c.Window.Left.Table.Columns[0].Width = new GridLength(240);
            c.Window.Width = 1120; c.Window.Height = 640; c.Window.Position = new PixelPoint(20, 30);
            await c.KeyAsync(Key.Tab);
            var expected = c.Window.CaptureWorkspace();
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json"));
            await store.SaveAsync(expected);
            await c.Window.InitializeAsync(c.RightPath, c.LeftPath);
            await c.Window.RestoreWorkspaceAsync((await store.ReadAsync())!);
            Equal("right", c.Window.ActivePanel.Id, "The active panel is restored");
            Equal("file2.log", c.Window.Left.Current?.Name, "The separate file cursor is restored");
            Check(c.Window.Left.SelectedNames().Order().SequenceEqual(new[] { "alpha.txt", "file2.log" }), "Multiple selected files survive JSON persistence.");
            Equal(SortField.Size, c.Window.Left.Session.SortOrder.Field, "The sort field is restored");
            Equal(240d, c.Window.Left.Table.Columns[0].Width.Value, "Column width is restored");
            Equal(1120d, c.Window.Width, "Window width is restored");
            Check(c.Window.Right.Table.IsKeyboardFocusWithin, "The restored active panel receives keyboard focus.");
            Check(c.Window.Left.Session.PreviousDirectories.Contains(Path.Combine(c.LeftPath, "folder1")), "Directory history is restored.");
            await c.KeyAsync(Key.Tab); await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            await c.KeyAsync(Key.Enter, target: c.Window.ActiveNavigationDialog);
            Equal("inside.txt", c.Window.Left.Current?.Name, "Historical directories retain their own cursors");
        }),
        new("workspace-scroll-and-empty-selection", "UC38; Session restore; UC20", async c =>
        {
            for (var i = 0; i < 100; i++) c.AddFile($"scroll-{i:D3}.txt", "x");
            await c.KeyAsync(Key.R, RawInputModifiers.Control); await c.KeyAsync(Key.End);
            c.Window.Left.Table.SelectedItems!.Clear();
            var state = c.Window.CaptureWorkspace();
            Check(state.Left.ScrollY > 0, "The test has a scrolled file panel.");
            await c.Window.InitializeAsync(c.RightPath, c.LeftPath);
            await c.Window.RestoreWorkspaceAsync(state); await c.SettleAsync();
            Equal(0, c.Window.Left.Table.SelectedItems!.Count, "An empty selection stays empty");
            Equal(state.Left.Cursor, c.Window.Left.Current?.Name, "The cursor survives an empty selection");
            Check(Math.Abs(state.Left.ScrollY - c.Window.CaptureWorkspace().Left.ScrollY) < 1, "Scroll position is restored.");
            await c.ScreenshotAsync("restored-workspace");
        }),
        new("workspace-minimized-state", "UC38; Session restore", async c =>
        {
            c.Window.TrackPlacement();
            c.Window.Width = 1100; c.Window.Height = 600;
            c.Window.WindowState = WindowState.Maximized;
            c.Window.WindowState = WindowState.Minimized;
            var state = c.Window.CaptureWorkspace();
            Check(state.Window.Minimized && state.Window.Maximized, "A minimized window remembers that it was maximized.");
            c.Window.WindowState = WindowState.Normal;
            await c.Window.RestoreWorkspaceAsync(state);
            Equal(WindowState.Minimized, c.Window.WindowState, "The minimized state is restored");
            Check(!c.Window.ShowActivated, "An automatic update does not activate a minimized window.");
            c.Window.WindowState = WindowState.Normal;
        }),
        new("workspace-missing-items-and-directory", "UC38; Session restore", async c =>
        {
            await c.SelectAsync("folder2"); await c.KeyAsync(Key.Enter);
            var state = c.Window.CaptureWorkspace();
            Directory.Delete(Path.Combine(c.LeftPath, "folder2"));
            await c.Window.RestoreWorkspaceAsync(state);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "An unavailable directory falls back to its nearest accessible parent");
            Check(c.Window.Left.Rows.All(row => row.Name != "folder2"), "Restoration uses the real current filesystem.");
        }),
        new("workspace-invalid-session-fallback", "UC38; Session restore", async c =>
        {
            var path = Path.Combine(c.FixturePath, "session.json");
            File.WriteAllText(path, "{invalid json");
            using var lifecycle = new WorkspaceLifecycle(c.Window, new SessionStore(path), null);
            await lifecycle.InitializeAsync(new[] { c.LeftPath, c.RightPath });
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "An invalid saved session does not prevent startup");
            Check((await new SessionStore(path).ReadAsync()) != null, "A valid session is saved after recovery.");
        }),
        new("workspace-idle-policy", "UC38; Automatic release update", c =>
        {
            var ready = new RestartReadiness(false, false, false, false, TimeSpan.FromSeconds(3));
            Check(ready.CanRestart, "An idle application can update.");
            Check(!(ready with { Busy = true }).CanRestart, "File work delays updates.");
            Check(!(ready with { Editing = true }).CanRestart, "Inline rename delays updates.");
            Check(!(ready with { DialogOpen = true }).CanRestart, "Dialogs delay updates.");
            Check(!(ready with { FeedbackActive = true }).CanRestart, "Feedback selection and comments delay updates.");
            Check(!(ready with { IdleFor = TimeSpan.FromSeconds(1) }).CanRestart, "Recent input delays updates.");
            return Task.CompletedTask;
        }),
        new("workspace-release-validation", "UC38; Automatic release update", async c =>
        {
            var root = Path.Combine(c.FixturePath, "releases"); Directory.CreateDirectory(root);
            var catalog = new ReleaseCatalog(root, OldId, "Xcmd.Probe.exe");
            var marker = Path.Combine(root, "latest.txt");
            File.WriteAllText(marker, "../../outside"); Equal<ReleaseInfo?>(null, await catalog.FindNextAsync(), "Invalid release paths are rejected");
            File.WriteAllText(marker, NewId); Equal<ReleaseInfo?>(null, await catalog.FindNextAsync(), "Incomplete publication is ignored");
            AddRelease(root, NewId, false);
            Equal(NewId, (await catalog.FindNextAsync())?.Id, "A complete verified release is discovered");
            File.WriteAllText(Path.Combine(root, NewId, "verification.json"), "{\"total\":1,\"failures\":1}");
            Equal<ReleaseInfo?>(null, await catalog.FindNextAsync(), "A failed verification cannot become an update");
            File.WriteAllText(marker, OldId); Equal<ReleaseInfo?>(null, await catalog.FindNextAsync(), "The current release is not restarted");
        }),
        new("workspace-update-waits-for-rename-and-fails-safely", "UC38; Automatic release update; UC17", async c =>
        {
            var root = Path.Combine(c.FixturePath, "releases"); AddRelease(root, NewId, false);
            File.WriteAllText(Path.Combine(root, "latest.txt"), NewId);
            using var lifecycle = new WorkspaceLifecycle(c.Window, new SessionStore(Path.Combine(c.FixturePath, "session.json")), new ReleaseCatalog(root, OldId, "Xcmd.Probe.exe"));
            await lifecycle.InitializeAsync(new[] { c.LeftPath, c.RightPath });
            await c.BeginRenameAsync("alpha.txt"); await Task.Delay(2200); await lifecycle.PollAsync();
            Check(c.Window.IsVisible && c.Window.Left.EditingRow != null && lifecycle.UpdateError == null, "A pending update leaves the rename draft untouched.");
            await c.KeyAsync(Key.Escape);
            await WaitAsync(() => lifecycle.UpdateError != null);
            Check(c.Window.IsVisible && c.Window.IsEnabled, "A launch failure leaves the current application open and usable.");
            await c.KeyAsync(Key.Tab); Equal("right", c.Window.ActivePanel.Id, "Input still works after update failure");
        }),
        new("workspace-update-waits-for-copy", "UC38; Automatic release update; UC04", async c =>
        {
            var root = Path.Combine(c.FixturePath, "releases"); AddRelease(root, NewId, false);
            File.WriteAllText(Path.Combine(root, "latest.txt"), NewId);
            using var lifecycle = new WorkspaceLifecycle(c.Window, new SessionStore(Path.Combine(c.FixturePath, "session.json")), new ReleaseCatalog(root, OldId, "Xcmd.Probe.exe"));
            await lifecycle.InitializeAsync(new[] { c.LeftPath, c.RightPath });
            await c.SelectAsync("alpha.txt");
            var gate = c.FileSystem.PauseNextCopy(Path.Combine(c.LeftPath, "alpha.txt"));
            c.PressKey(Key.F5); ConfirmTransfer(c); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                await Task.Delay(2200); await lifecycle.PollAsync();
                Check(c.Window.IsVisible && c.Window.IsBusy && lifecycle.UpdateError == null, "A real pending copy blocks the update.");
            }
            finally { gate.Release.TrySetResult(); }
            await c.SettleAsync();
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "The pending copy completes before any update attempt");
        }),
        new("workspace-process-startup-failure-keeps-session", "UC38; Automatic release update; process integration", async c =>
        {
            var root = Path.Combine(c.FixturePath, "releases"); var reports = Path.Combine(c.FixturePath, "reports"); Directory.CreateDirectory(reports);
            var oldExe = AddRelease(root, OldId, true); AddRelease(root, NewId, true);
            var sessionPath = Path.Combine(c.FixturePath, "session.json");
            await c.SelectAsync("alpha.txt"); await new SessionStore(sessionPath).SaveAsync(c.Window.CaptureWorkspace());
            using var old = StartHost(oldExe, sessionPath, root, reports, NewId);
            try
            {
                await WaitAsync(() => File.Exists(Path.Combine(reports, OldId + ".json")));
                File.WriteAllText(Path.Combine(root, "latest.txt"), NewId);
                var failurePath = Path.Combine(reports, OldId + "-failed-update.json");
                await WaitAsync(() => File.Exists(failurePath));
                using var report = await ReadWorkspaceReportAsync(failurePath);
                Check(report.RootElement.GetProperty("IsEnabled").GetBoolean(), "The current window is usable after a replacement startup failure.");
                var state = report.RootElement.GetProperty("state").Deserialize<WorkspaceState>()!;
                Equal("alpha.txt", state.Left.Cursor, "A failed real process handoff retains the current cursor");
                Check(!File.Exists(Path.Combine(reports, NewId + ".json")), "A failed replacement never opens its window.");
                await old.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { await old.StopAsync(); }
        }),
        new("workspace-close-saves-session", "UC38; Session restore", async c =>
        {
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json"));
            using var lifecycle = new WorkspaceLifecycle(c.Window, store, null);
            await lifecycle.InitializeAsync(new[] { c.LeftPath, c.RightPath });
            await c.SelectAsync("file2.log");
            c.Window.Close();
            await WaitAsync(() => !c.Window.IsVisible);
            Equal("file2.log", (await store.ReadAsync())!.Left.Cursor, "An ordinary close saves the latest cursor");
        }),
        new("workspace-real-process-handoff", "UC38; Automatic release update; process integration", async c =>
        {
            var root = Path.Combine(c.FixturePath, "releases"); var reports = Path.Combine(c.FixturePath, "reports"); Directory.CreateDirectory(reports);
            var oldExe = AddRelease(root, OldId, true); AddRelease(root, NewId, true);
            var sessionPath = Path.Combine(c.FixturePath, "session.json");
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift); await c.KeyAsync(Key.Tab);
            var issues = new IssueStore(Path.Combine(c.FixturePath, "issues"));
            await issues.AddAsync(new("note", FeedbackIssue.New, "Keep the active file visible.", new("left.files", "File list"), OldId));
            var favorite = c.Window.Favorites.Add(c.LeftPath); c.Window.Favorites.Rename(favorite.Id, "Working files");
            c.Window.CommandLine.Restore(new("tool unfinished", ["tool first", "tool second"], Caret: 4, SelectionStart: 4, SelectionEnd: 4));
            var expected = c.Window.CaptureWorkspace(); await new SessionStore(sessionPath).SaveAsync(expected);
            using var old = StartHost(oldExe, sessionPath, root, reports);
            try
            {
                await WaitAsync(() => File.Exists(Path.Combine(reports, OldId + ".json")));
                File.WriteAllText(Path.Combine(root, "latest.txt"), NewId);
                await WaitAsync(() => File.Exists(Path.Combine(reports, NewId + ".json")));
                await old.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Equal(0, old.ExitCode, "The old real process closes cleanly");
                using var report = await ReadWorkspaceReportAsync(Path.Combine(reports, NewId + ".json"));
                Check(report.RootElement.GetProperty("parentExited").GetBoolean(), "The replacement opens only after the old process exits.");
                Equal(0, report.RootElement.GetProperty("nativeApplicationWindows").GetInt32(), "The replacement uses the actual app without native windows");
                Check(!report.RootElement.GetProperty("consoleAttached").GetBoolean(), "The replacement process has no console");
                var actual = report.RootElement.GetProperty("state").Deserialize<WorkspaceState>()!;
                Equal(expected.Left.DirectoryPath, actual.Left.DirectoryPath, "The new process restores the left directory");
                Equal(expected.Left.Cursor, actual.Left.Cursor, "The new process restores the file cursor");
                Equal("right", actual.ActivePanel, "The new process restores the active panel");
                Equal("Keep the active file visible.", (await issues.ReadAsync()).Single().Text, "Comments survive a real process update in their independent store");
                Check(actual.Notes == null, "The replacement session does not duplicate issues");
                Equal("Working files", actual.Favorites!.Single().Name, "Named favorites survive a real process update");
                Equal(c.LeftPath, actual.Favorites!.Single().Path, "The replacement keeps the favorite directory");
                Equal(11L, actual.Left.Sizes!.Single(item => item.Name == "folder1").Size.Bytes, "Folder sizes survive a real process update");
                Equal(expected.CommandLine!.Draft, actual.CommandLine!.Draft, "The new process restores the command draft without execution");
                Equal(string.Join('|', expected.CommandLine.History), string.Join('|', actual.CommandLine.History), "The command history survives a release update");
                Equal(4, actual.CommandLine.Caret, "The command caret survives a release update");
                c.Measurements["processHandoffVerified"] = true;
            }
            finally { await old.StopAsync(); }
        }),
        new("workspace-feedback-select-comment-list", "UC39; Element feedback; refinement list", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.ClickAsync(Button(c.Window, "StartFeedback"));
            Check(c.Window.Feedback.IsPicking, "Feedback mode selects elements.");
            await c.ClickAsync(c.Row("folder1"));
            var dialog = c.Window.Feedback.ActiveComment!;
            Check(dialog != null && dialog.Editor.IsFocused, "Selecting an element opens a focused comment editor.");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Selecting feedback does not activate the underlying file action");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Feedback does not change the file cursor");
            await c.TextAsync("Keep the folder icon visible.", target: dialog);
            await c.ScreenshotAsync("selected-element");
            await c.ScreenshotAsync("feedback-comment", dialog);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, dialog);
            Equal(1, c.Window.Feedback.Notes.Count, "The comment is added once");
            Check(c.Window.Feedback.Notes[0].Target.Id.StartsWith("FileRowTheme.", StringComparison.Ordinal), "Clicking row background records its source-defined template control");
            Check(c.Window.Feedback.Notes[0].Target.Name.Contains("folder1"), "The selected row is identified.");
            await c.ClickAsync(Button(c.Window, "ShowNotes"));
            await c.ScreenshotAsync("refinement-list", c.Window.Feedback.ActiveNotes);
            await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveNotes);
        }),
        new("workspace-feedback-empty-and-remove", "UC39; Element feedback; refinement list", async c =>
        {
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json"));
            using var lifecycle = new WorkspaceLifecycle(c.Window, store, null);
            await lifecycle.InitializeAsync(new[] { c.LeftPath, c.RightPath });
            await c.ClickAsync(Button(c.Window, "StartFeedback")); await c.ClickAsync(c.Row("alpha.txt"));
            var dialog = c.Window.Feedback.ActiveComment!;
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, dialog);
            Check(dialog.IsVisible && c.Window.Feedback.Notes.Count == 0, "An empty comment cannot be added.");
            await c.TextAsync("Clarify the file name.", target: dialog);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, dialog);
            await c.ClickAsync(Button(c.Window, "ShowNotes"));
            var notes = c.Window.Feedback.ActiveNotes!;
            var list = notes.GetVisualDescendants().OfType<ListBox>().Single();
            await c.ClickAsync(list.ContainerFromIndex(0)!, target: notes);
            await c.ClickAsync(notes.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "RemoveNote"), target: notes);
            Equal(0, c.Window.Feedback.Notes.Count, "The selected comment is removed");
            Equal(0, (await c.Window.Feedback.Store!.ReadAsync()).Length, "Removing a comment updates the independent store");
            await c.KeyAsync(Key.Escape, target: notes);
        }),
        new("workspace-feedback-cancel-and-persist", "UC39; Element feedback; session restore", async c =>
        {
            var sessionPath = Path.Combine(c.FixturePath, "session.json");
            using var lifecycle = new WorkspaceLifecycle(c.Window, new SessionStore(sessionPath), null);
            await lifecycle.InitializeAsync(new[] { c.LeftPath, c.RightPath });
            await c.ClickAsync(Button(c.Window, "StartFeedback")); await c.KeyAsync(Key.Escape);
            Check(!c.Window.Feedback.IsPicking && c.Window.Feedback.Notes.Count == 0, "Escape cancels element selection without a comment.");
            await c.ClickAsync(Button(c.Window, "StartFeedback")); await c.ClickAsync(Button(c.Window, "leftSortSize"));
            var dialog = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Show a clearer sort direction.\nKeep the label short.", target: dialog);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, dialog);
            await lifecycle.PendingWork;
            var restored = (await c.Window.Feedback.Store!.ReadAsync()).Single();
            Equal("Show a clearer sort direction.\nKeep the label short.", restored.Text, "Comments are saved immediately without a session save");
            c.Window.Feedback.Restore([]); await c.Window.Feedback.RefreshAsync();
            Equal(restored.Text, c.Window.Feedback.Notes[0].Text, "Comments restore from the independent store");
        })
    ];
}

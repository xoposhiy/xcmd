using Avalonia.Input;
using Avalonia.Input.Platform;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class GitHistoryScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("git-history-commit-message", "Commit columns show the full selected message, and snapshots keep that message across navigation", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            var expected = await fixture.AddDetailedCommitAsync();
            var panel = c.Window.Right;
            var originalHeight = panel.Table.Bounds.Height;
            await c.SelectAsync("..");
            await c.KeyAsync(Key.L, RawInputModifiers.Control | RawInputModifiers.Alt);
            Check(panel.IsCommitMessageVisible, "The commit-message area appears in the log");
            Equal(expected, panel.CommitMessage.Text, "The entire commit message preserves paragraphs and Unicode");
            Check(panel.Table.IsKeyboardFocusWithin, "Reading a commit message keeps keyboard focus in the commit list");
            await GitHistoryProbe.ResizeMessageAsync(c, panel);
            foreach (var scale in new[] { 1d, 1.5d })
            {
                c.Window.SetScaleForTest(scale);
                await c.ScreenshotAsync("git-commit-message-" + scale * 100);
                GitHistoryProbe.CheckMessageScroll(panel);
                GitHistoryProbe.CheckLogPresentation(c, panel);
            }
            var commitName = panel.Current!.Name;
            await c.KeyAsync(Key.Tab);
            Check(c.Window.ActivePanel == c.Window.Left && GitHistoryProbe.KeyboardHelp(c).Contains("Ctrl+Shift+C", StringComparison.Ordinal), "The physical panel shows the file comparison shortcut");
            await c.ClickAsync(panel.CommitMessage);
            Check(c.Window.ActivePanel == panel && GitHistoryProbe.KeyboardHelp(c).Contains("Full repository snapshot", StringComparison.Ordinal), "Focusing the opposite commit message updates the active panel before its shortcut hints");
            await c.KeyAsync(Key.A, RawInputModifiers.Control);
            Equal(expected, panel.CommitMessage.SelectedText, "The full message can be selected for copying");
            await c.KeyAsync(Key.Down);
            Equal(commitName, panel.Current?.Name, "Message caret movement does not navigate the commit list");
            await c.TextAsync("git log");
            Equal("git log", c.Window.CommandLine.Editor.Text, "Typing from the read-only message starts a command");
            Check(c.Window.CommandLine.HasKeyboardFocus, "Read-only message focus does not swallow command text");
            await c.KeyAsync(Key.Escape);
            await c.ClickAsync(panel.CommitMessage);
            await c.KeyAsync(Key.Tab);
            Check(c.Window.ActivePanel == c.Window.Left && c.Window.Left.Table.IsKeyboardFocusWithin, "Tab from the message switches panels");
            await c.KeyAsync(Key.Tab);
            await c.ClickAsync(panel.CommitMessage);
            await c.KeyAsync(Key.Escape);
            Check(panel.Table.IsKeyboardFocusWithin, "Escape from the message returns to the commit list");
            c.PressKey(Key.Down); c.PressKey(Key.Up);
            await c.SettleAsync();
            Equal(expected, panel.CommitMessage.Text, "Rapid navigation cannot apply a message from an older selection");
            await c.KeyAsync(Key.Down);
            Check(panel.CommitMessage.Text!.StartsWith("Update unrelated file", StringComparison.Ordinal) && !panel.CommitMessage.Text.Contains("Final paragraph", StringComparison.Ordinal), "The message follows the selected commit");
            await c.KeyAsync(Key.Home, RawInputModifiers.Control);
            Check(!panel.CommitMessage.Text!.Contains("Update unrelated file", StringComparison.Ordinal), "The parent row clears the previous message");
            await c.KeyAsync(Key.Down);
            Equal(expected, panel.CommitMessage.Text, "Returning to a commit restores its cached full message");
            await c.KeyAsync(Key.Enter);
            Check(panel.IsCommitMessageVisible && panel.Table.Bounds.Height < c.Window.Left.Table.Bounds.Height, "Snapshot browsing keeps space for the current commit message");
            Equal(expected, panel.CommitMessage.Text, "Entering a commit keeps its full message visible");
            Check(panel.Table.Columns[3].IsVisible && panel.Table.Columns[0].Header is not string, "Snapshots restore normal file columns including Git");
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter);
            await c.SelectAsync("inside.txt");
            Equal(expected, panel.CommitMessage.Text, "The commit message remains visible across folders and file selections");
            await c.ScreenshotAsync("git-snapshot-commit-message");
            await c.KeyAsync(Key.Back);
            await c.KeyAsync(Key.Back);
            Equal(expected, panel.CommitMessage.Text, "Returning to the log restores the selected commit message");
            await c.KeyAsync(Key.Escape);
            c.Window.SetScaleForTest(1); await c.SettleAsync();
            Check(!panel.IsCommitMessageVisible && Math.Abs(panel.Table.Bounds.Height - originalHeight) < 0.1, "Leaving Git history restores the normal file layout");
        }),
        new("git-history-folder", "Folder history and commit trees use the same file panel, virtual filesystem, cursor and Git column", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            var panel = c.Window.Right;
            var originalTable = panel.Table;
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.L, RawInputModifiers.Control | RawInputModifiers.Alt);
            Equal(panel, c.Window.ActivePanel, "The adjacent history panel receives focus");
            Equal("folder1", c.Window.Left.Current?.Name, "The source cursor stays on the folder");
            Equal(2, GitHistoryProbe.Commits(panel).Length, "Only commits that changed the folder are listed");
            Check(panel.DisplayPath.Contains("folder1", StringComparison.Ordinal), "The normal path header identifies the history scope");
            Check(panel.Current!.Name.EndsWith(" Update nested files", StringComparison.Ordinal), "History is newest first");
            GitHistoryProbe.CheckLogPresentation(c, panel);
            await c.ScreenshotAsync("git-history-log");
            await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer!.TextContent.Text!.Contains("Full nested commit message.", StringComparison.Ordinal), "F3 shows the full commit message");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.Enter);
            Equal("tree", GitPath.Parse(panel.Session.DirectoryPath).Kind, "Enter opens the complete commit tree");
            Equal(originalTable, panel.Table, "The same file table renders the log and snapshot");
            Check(panel.Session.FileSystem is GitFileSystem, "PanelSession reads through the Git virtual filesystem");
            Check(panel.IsCommitMessageVisible && panel.CommitMessage.Text!.Contains("Full nested commit message.", StringComparison.Ordinal), "The full message identifies the opened commit");
            Check(panel.Rows.Any(row => row.Name == "alpha.txt" && !row.Git.HasChanges), "Unchanged files remain visible with a blank Git cell");
            await c.KeyAsync(Key.F4, RawInputModifiers.Control);
            Equal(SortField.Extension, panel.Session.SortOrder.Field, "Snapshot sorting uses the normal Ctrl+F4 shortcut");
            await c.KeyAsync(Key.F3, RawInputModifiers.Control);
            Check(panel.Rows.Single(row => row.Name == "folder1").Git.HasChanges, "Folder Git cells include descendant changes");
            Equal("..", panel.Current?.Name, "The parent row is initially current");
            await c.KeyAsync(Key.F3);
            Equal("changes", GitPath.Parse(panel.Session.DirectoryPath).Kind, "F3 on the parent row opens actionable changed files");
            Check(panel.Rows.Any(row => row.Name == "folder1/new 日本語.txt"), "The changed-file list includes added paths");
            await c.KeyAsync(Key.Back);
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter);
            Equal(GitChange.Added, panel.Rows.Single(row => row.Name == "new 日本語.txt").Git.Unstaged, "The ordinary Git column marks added snapshot files");
            await c.SelectAsync("inside.txt");
            await c.KeyAsync(Key.D, RawInputModifiers.Control | RawInputModifiers.Alt);
            Check(c.Window.Viewer!.TextContent.Text!.Contains("+Nested committed version", StringComparison.Ordinal), "Snapshot diff compares with the parent commit");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.F3);
            Equal("Nested committed version\n", c.Window.Viewer!.TextContent.Text, "The viewer reads the committed blob");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await GitHistoryProbe.CheckFileInteractionAsync(c, panel, "inside.txt", "new 日本語.txt");
            foreach (var scale in new[] { 1d, 1.5d })
            {
                c.Window.SetScaleForTest(scale);
                await c.ScreenshotAsync("git-history-tree-" + scale * 100);
            }
            await c.KeyAsync(Key.Back);
            Equal("folder1", panel.Current?.Name, "Back selects the exited folder");
            await c.KeyAsync(Key.Back);
            Check(GitPath.IsLog(panel.Session.DirectoryPath) && panel.Current!.Name.EndsWith(" Update nested files", StringComparison.Ordinal), "Back returns to the same commit");
            await c.KeyAsync(Key.Escape);
            Equal(c.RightPath, panel.Session.DirectoryPath, "Escape restores the original adjacent folder");
            c.Window.SetScaleForTest(1);
            c.Window.Activate(c.Window.Left);
            await GitFixture.RefreshAsync(c.Window.Left.Git);
            await c.ClickAsync(c.Window.Left.Git.ActionsButton);
            await c.ClickAsync(c.Window.Left.Git.LogCommand);
            Equal(panel, c.Window.ActivePanel, "The status menu keeps focus in the adjacent log");
            Equal(4, GitHistoryProbe.Commits(panel).Length, "The status menu opens the displayed repository root history");
        }),
        new("git-history-file-and-diff", "File history filters commits; full snapshots ignore working changes and cannot change working files", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            var file = Path.Combine(c.LeftPath, "alpha.txt");
            File.WriteAllText(file, "Staged root version\n");
            await GitFixture.RunAsync(c.LeftPath, "add", "alpha.txt");
            File.AppendAllText(file, "Working tail\n");
            File.AppendAllText(Path.Combine(c.LeftPath, "file10.bin"), "Outside the file scope");
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.D, RawInputModifiers.Control | RawInputModifiers.Alt);
            var diff = c.Window.Viewer!.TextContent.Text!;
            Check(diff.Contains("-Committed root version", StringComparison.Ordinal) && diff.Contains("+Staged root version", StringComparison.Ordinal) && diff.Contains("+Working tail", StringComparison.Ordinal), "Working diff includes staged and unstaged changes");
            c.Window.Viewer.Close(); await c.SettleAsync(); c.Window.Left.FocusCurrent();
            await c.KeyAsync(Key.L, RawInputModifiers.Control | RawInputModifiers.Alt);
            var panel = c.Window.Right;
            Equal(2, GitHistoryProbe.Commits(panel).Length, "Only commits that changed the file are listed");
            await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer!.TextContent.Text!.Contains("alpha.txt", StringComparison.Ordinal) && !c.Window.Viewer.TextContent.Text.Contains("file10.bin", StringComparison.Ordinal), "Working-tree F3 also respects a single-file history scope");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await GitHistoryProbe.SelectCommitAsync(c, panel, "Update root files");
            await c.KeyAsync(Key.Enter);
            Check(panel.Rows.Any(row => row.Name == "renamed file.log" && row.Git.Unstaged == GitChange.Renamed), "The full tree marks the renamed file");
            Check(panel.Rows.All(row => row.Name != "zeta.md"), "Deleted files are absent from the snapshot tree");
            await c.KeyAsync(Key.F3);
            Equal("changes", GitPath.Parse(panel.Session.DirectoryPath).Kind, "F3 on the parent row includes deleted files in an actionable list");
            await c.SelectAsync("zeta.md");
            await c.KeyAsync(Key.D, RawInputModifiers.Control | RawInputModifiers.Alt);
            Check(c.Window.Viewer!.TextContent.Text!.Contains("-notes", StringComparison.Ordinal), "Deleted commit files have a usable diff");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.F3);
            Equal("notes", c.Window.Viewer!.TextContent.Text, "Deleted commit files open the parent version");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.Back);
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Enter);
            Equal("Committed root version\n", c.Window.Viewer!.TextContent.Text, "Snapshot content ignores working changes");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.F8); await c.KeyAsync(Key.F4); await c.KeyAsync(Key.F7);
            Check(!c.Window.HasOpenDialog, "Snapshot write commands do not open file operations");
            await c.Window.Clipboard!.SetTextAsync("unchanged");
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal("unchanged", await c.Window.Clipboard!.TryGetTextAsync(), "Unsupported comparison keeps the clipboard unchanged");
            Check(c.Window.LastError?.Contains("cannot be compared in VS Code", StringComparison.Ordinal) == true, "Comparison explains the Git history limit");
            Equal(0, c.Launcher.Requests.Count, "Snapshot paths never reach VS Code");
            await c.KeyAsync(Key.F10, RawInputModifiers.Shift);
            Equal(0, c.Shell.Menus.Count, "Snapshot rows never reach the Windows shell menu");
            Check(c.Window.LastError?.Contains("unavailable in Git history", StringComparison.Ordinal) == true, "The shell menu explains the Git history limit");
            Equal("Staged root version\nWorking tail\n", File.ReadAllText(file), "History leaves working files unchanged");
            await c.KeyAsync(Key.Back);
            Check(GitPath.IsLog(panel.Session.DirectoryPath) && panel.Current!.Name.EndsWith(" Update root files", StringComparison.Ordinal), "Back restores the commit cursor");
        }),
        new("git-history-working-tree", "Working tree uses the history scope and the normal panel for staged, unstaged and untracked paths", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            var alpha = Path.Combine(c.LeftPath, "alpha.txt");
            File.WriteAllText(alpha, "Outside folder change\n");
            var nested = Path.Combine(c.LeftPath, "folder1", "inside.txt");
            File.WriteAllText(nested, "Staged version\n");
            await GitFixture.RunAsync(c.LeftPath, "add", "folder1/inside.txt");
            File.AppendAllText(nested, "Working version\n");
            File.Delete(Path.Combine(c.LeftPath, "folder1", "new 日本語.txt"));
            File.WriteAllText(Path.Combine(c.LeftPath, "folder1", "draft.txt"), "Untracked draft\n");
            File.AppendAllText(Path.Combine(c.LeftPath, ".git", "info", "exclude"), "\nignored.txt\n");
            File.WriteAllText(Path.Combine(c.LeftPath, "folder1", "ignored.txt"), "Ignored");
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.L, RawInputModifiers.Control | RawInputModifiers.Alt);
            var panel = c.Window.Right;
            Equal("Working tree", panel.Current?.Name, "Working tree appears before commits");
            Check(panel.CommitMessage.Text!.Contains("Uncommitted changes", StringComparison.Ordinal), "Working tree shows an explanation instead of an old commit message");
            await c.KeyAsync(Key.F3);
            var text = c.Window.Viewer!.TextContent.Text!;
            foreach (var path in new[] { "folder1/inside.txt", "folder1/new 日本語.txt", "folder1/draft.txt" })
                Check(text.Contains(path, StringComparison.Ordinal), "F3 lists a scoped change: " + path);
            Check(!text.Contains("alpha.txt", StringComparison.Ordinal) && !text.Contains("ignored.txt", StringComparison.Ordinal), "Other folders and ignored files are excluded");
            Check(text.Contains("Modified\tModified\tfolder1/inside.txt", StringComparison.Ordinal), "F3 separates staged and working changes");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.Enter);
            Equal("working", GitPath.Parse(panel.Session.DirectoryPath).Kind, "Enter opens the normal working-change panel");
            Equal(3, panel.Rows.Count(row => !row.Entry.IsParent), "Only scoped changed files are listed");
            var changed = panel.Rows.Single(row => row.Name == "folder1/inside.txt");
            Check(changed.Git.Staged == GitChange.Modified && changed.Git.Unstaged == GitChange.Modified, "The ordinary Git column shows both statuses");
            Check(changed.Entry.Size > 0 && panel.DisplayPath.Contains("folder1", StringComparison.Ordinal), "Working changes show real file sizes and the selected scope");
            await c.ScreenshotAsync("git-working-tree-files");
            await c.SelectAsync("folder1/inside.txt");
            await c.KeyAsync(Key.D, RawInputModifiers.Control | RawInputModifiers.Alt);
            Check(c.Window.Viewer!.TextContent.Text!.Contains("+Working version", StringComparison.Ordinal), "Working file diff includes unstaged changes");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.SelectAsync("folder1/new 日本語.txt"); await c.KeyAsync(Key.F3);
            Equal("Added content\n", c.Window.Viewer!.TextContent.Text, "A deleted working file opens its HEAD version");
            c.Window.Viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.Back);
            Equal("Working tree", panel.Current?.Name, "Back restores the working-tree cursor");
            await c.KeyAsync(Key.Escape);
            await GitFixture.RunAsync(c.LeftPath, "restore", "--source=HEAD", "--staged", "--worktree", "folder1");
            File.Delete(Path.Combine(c.LeftPath, "folder1", "draft.txt"));
            c.Window.Activate(c.Window.Left); c.Window.Left.FocusCurrent();
            await c.KeyAsync(Key.L, RawInputModifiers.Control | RawInputModifiers.Alt);
            Check(panel.Rows.All(row => row.Name != "Working tree"), "Changes outside the history scope do not add a working-tree entry");
        }),
        new("git-history-root-and-empty", "Initial commits, literal filters and empty repositories use real Git and the virtual filesystem", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            var reader = new GitHistoryReader();
            var scope = await reader.ResolveAsync(c.LeftPath, CancellationToken.None);
            var log = await reader.LogAsync(scope, 0, CancellationToken.None);
            Equal(4, log.Length, "Repository history includes all commits");
            var initial = log.Last();
            var changes = await reader.ChangesAsync(scope.Root, initial.Id, CancellationToken.None);
            Check(changes.Length > 0 && changes.All(change => change.Status == "A"), "The first commit compares with an empty tree");
            using var documents = new GitHistoryDocuments();
            var report = await documents.DiffAsync(scope.Root, "alpha.txt", initial.Id, changes.First(change => change.Path == "alpha.txt"), CancellationToken.None);
            Check(File.ReadAllText(report).Contains("+abc", StringComparison.Ordinal), "The initial commit has an added-file patch");
            File.WriteAllText(Path.Combine(c.LeftPath, "literal[1].txt"), "literal\n");
            File.WriteAllText(Path.Combine(c.LeftPath, "literal1.txt"), "other\n");
            await GitFixture.RunAsync(c.LeftPath, "add", "."); await GitFixture.RunAsync(c.LeftPath, "commit", "-m", "Add literal names");
            File.AppendAllText(Path.Combine(c.LeftPath, "literal1.txt"), "other change\n");
            await GitFixture.RunAsync(c.LeftPath, "commit", "-am", "Change other name");
            var literal = await reader.ResolveAsync(Path.Combine(c.LeftPath, "literal[1].txt"), CancellationToken.None);
            Equal(1, (await reader.LogAsync(literal, 0, CancellationToken.None)).Length, "Path filters treat brackets literally");
            File.WriteAllText(Path.Combine(c.LeftPath, "untracked.txt"), "Untracked text\n");
            report = await documents.DiffAsync(scope.Root, "untracked.txt", null, null, CancellationToken.None);
            Check(File.ReadAllText(report).Contains("+Untracked text", StringComparison.Ordinal), "Untracked files compare with an empty file");
            await GitFixture.RunAsync(c.RightPath, "init", "-b", "main");
            try
            {
                var files = new GitFileSystem(new LocalFileSystem(), reader);
                var path = await files.OpenLogAsync(c.RightPath, CancellationToken.None);
                var entries = await files.ReadDirectoryAsync(path, CancellationToken.None);
                Equal("Working tree", entries.Single().Name, "An unborn repository shows its untracked files as working tree");
                Check(GitHistoryRules.InScope("folder", "new/name.txt", "folder/old.txt"), "Moved files remain in their previous folder scope");
                Check(!GitHistoryRules.InScope("folder", "folder-other/file.txt"), "Scope matching requires a path boundary");
            }
            finally { GitFixture.AllowCleanup(c.RightPath); }
        })
    ];
}

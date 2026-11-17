// @tests OpenOtherPanel, RefreshEntries, DiscoverShortcuts, BrowseHistory, BrowseSnapshot, InspectWorkingChanges, ReadDiff, AsyncCoordination
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class GitUsabilityFixScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("git-usability-return-and-viewer", "Cross-panel Git entry restores its original folder; historical viewers retain origin and enforce read-only actions", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            await c.SelectAsync(".."); await c.KeyAsync(Key.L, RawInputModifiers.Control | RawInputModifiers.Alt);
            var panel = c.Window.Right;
            var sort = panel.Session.SortOrder;
            await c.KeyAsync(Key.F6, RawInputModifiers.Control);
            Equal(sort, panel.Session.SortOrder, "Log sort shortcuts cannot change the next file sort");
            Check(!GitHistoryProbe.Summary(panel).Contains("bytes", StringComparison.Ordinal), "Commit counts do not show filesystem sizes");
            Check(!GitHistoryProbe.KeyboardHelp(c).Contains("F5  Copy", StringComparison.Ordinal), "Git help does not advertise unavailable file commands");
            await c.KeyAsync(Key.Enter);
            Check(panel.Rows.All(row => row.DisplayDate.Length == 0), "Snapshot files have unknown modification dates");
            Check(ToolTip.GetTip((Control)panel.Table.Columns[3].Header!)!.ToString()!.Contains("first parent", StringComparison.Ordinal), "The Git legend describes commit comparisons");
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            var revision = GitPath.Parse(panel.Session.DirectoryPath).Revision[..10];
            Check(viewer.IsDocumentReadOnly && viewer.Title!.Contains(revision, StringComparison.Ordinal) && viewer.Title.Contains("alpha.txt", StringComparison.Ordinal), "The viewer identifies the original file and commit");
            Check(!viewer.Title!.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase), "The title does not expose a cache path as the file origin");
            Check(!GitHistoryProbe.ViewerCommand(viewer, "viewer.edit").IsEnabled && !GitHistoryProbe.ViewerCommand(viewer, "viewer.open").IsEnabled, "Both external commands respect snapshot read-only state");
            var launches = c.Launcher.Requests.Count;
            await c.KeyAsync(Key.F4, target: viewer); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, viewer);
            Equal(launches, c.Launcher.Requests.Count, "Historical content cannot be edited or opened externally through shortcuts");
            viewer.Close(); await c.SettleAsync(); panel.FocusCurrent();
            await c.KeyAsync(Key.C, RawInputModifiers.Control);
            Check(c.Window.LastError!.Contains("Git history", StringComparison.Ordinal) && !c.Window.LastError.Contains("archive", StringComparison.OrdinalIgnoreCase), "Copy feedback names the actual source and does not recommend an unavailable command");
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Left, RawInputModifiers.Control);
            await c.KeyAsync(Key.Tab); await c.KeyAsync(Key.Escape);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Escape restores a panel entered through Ctrl+Left");
            c.Window.Activate(panel); panel.FocusCurrent(); await c.KeyAsync(Key.Escape);
            Equal(c.RightPath, panel.Session.DirectoryPath, "The original log panel also restores its physical location");
            Check(GitHistoryProbe.KeyboardHelp(c).Contains("F5  Copy", StringComparison.Ordinal), "Normal folder help returns outside Git");
        }),
        new("git-usability-empty-and-refresh", "Empty history explains its state; Working tree refreshes coherently and logs show new local commits", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            var empty = Path.Combine(c.LeftPath, "empty"); Directory.CreateDirectory(empty);
            File.WriteAllText(Path.Combine(empty, "ignored.txt"), "Ignored");
            File.AppendAllText(Path.Combine(c.LeftPath, ".git", "info", "exclude"), "\n/empty/\n");
            await c.Window.Left.LoadAsync(empty); c.Window.Activate(c.Window.Left);
            c.Window.GitHistory.Open(selectedItem: false); await c.SettleAsync();
            var panel = c.Window.Right;
            Equal("No commits changed this path.", panel.CommitMessage.Text, "Empty path history gives a specific reason");
            await c.KeyAsync(Key.Escape); await c.Window.Left.LoadAsync(c.LeftPath);
            File.AppendAllText(Path.Combine(c.LeftPath, "alpha.txt"), "Working change");
            c.Window.Activate(c.Window.Left); c.Window.GitHistory.Open(selectedItem: false); await c.SettleAsync();
            await c.KeyAsync(Key.Enter);
            File.WriteAllText(Path.Combine(c.LeftPath, "after-open.txt"), "New file");
            await c.SelectAsync(".."); await c.KeyAsync(Key.F3);
            Check(panel.Rows.Any(row => row.Name == "after-open.txt") && c.Window.Viewer!.TextContent.Text!.Contains("after-open.txt", StringComparison.Ordinal), "F3 refreshes the panel and report from the same captured changes");
            c.Window.Viewer!.Close(); await c.SettleAsync(); panel.FocusCurrent();
            File.WriteAllText(Path.Combine(c.LeftPath, "automatic.txt"), "Auto refresh");
            using (var updates = new DirectoryUpdates(c.Window)) await updates.PollAsync();
            Check(panel.Rows.Any(row => row.Name == "automatic.txt"), "Live Git panels participate in background refresh");
            Check(GitHistoryProbe.Summary(panel).Contains("Ctrl+R", StringComparison.Ordinal), "Manual refresh remains discoverable");
            await GitFixture.RunAsync(c.LeftPath, "add", "."); await GitFixture.RunAsync(c.LeftPath, "commit", "-m", "New local commit");
            await c.KeyAsync(Key.Back);
            Check(GitHistoryProbe.Commits(panel).Any(row => row.Name.EndsWith(" New local commit", StringComparison.Ordinal)), "Log refresh detects a new HEAD");
            await c.KeyAsync(Key.Escape);
            var unborn = Path.Combine(c.FixturePath, "unborn"); Directory.CreateDirectory(unborn);
            await GitFixture.RunAsync(unborn, "init", "-b", "main");
            try
            {
                await c.Window.Left.LoadAsync(unborn); c.Window.Activate(c.Window.Left);
                c.Window.GitHistory.Open(selectedItem: false); await c.SettleAsync();
                Equal("This repository has no commits yet.", panel.CommitMessage.Text, "Unborn repositories explain why there are no commits");
            }
            finally { GitFixture.AllowCleanup(unborn); }
        }),
        new("git-usability-cancel-and-deletion", "Escape cancels pending Git work without changing drafts, errors stay visible, and staged deletions have a usable diff", async c =>
        {
            using var fixture = await GitHistoryFixture.CreateAsync(c.LeftPath);
            c.Window.CommandLine.Editor.Text = "keep this draft";
            c.Window.GitHistory.Open(selectedItem: false);
            c.Window.CommandLine.FocusCommand(); c.PressKey(Key.Escape); await c.SettleAsync();
            Equal("keep this draft", c.Window.CommandLine.Editor.Text, "Escape cancels Git work before clearing the command draft");
            Check(!c.Window.Right.HasHistory && c.Window.StatusMessage.Contains("canceled", StringComparison.Ordinal), "Canceled log reads cannot steal focus with a late result");
            c.Window.Activate(c.Window.Left); c.Window.Left.FocusCurrent();
            c.Window.GitHistory.Open(selectedItem: false); await c.SettleAsync(); await c.KeyAsync(Key.Enter);
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.D, RawInputModifiers.Control | RawInputModifiers.Alt);
            Check(c.Window.LastError!.Contains("did not change", StringComparison.Ordinal) && c.Window.StatusMessage == c.Window.LastError, "Expected Git errors are not replaced with Ready");
            await c.KeyAsync(Key.Escape); c.Window.Activate(c.Window.Left); c.Window.Left.FocusCurrent();
            await GitFixture.RunAsync(c.LeftPath, "rm", "alpha.txt");
            c.Window.GitHistory.Open(selectedItem: false); await c.SettleAsync(); await c.KeyAsync(Key.Enter);
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.D, RawInputModifiers.Control | RawInputModifiers.Alt);
            Check(c.Window.Viewer!.TextContent.Text!.Contains("-Committed root version", StringComparison.Ordinal), "A staged deletion compares its HEAD content with the missing working file");
            Check(c.Window.Viewer.Title!.Contains("HEAD → working tree", StringComparison.Ordinal), "The diff title states what is compared");
        })
    ];
}

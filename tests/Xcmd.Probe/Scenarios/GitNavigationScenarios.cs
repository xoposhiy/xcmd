// @tests NavigatePanels, RefreshEntries, SeeRepositoryState, RepositoryStatus, AsyncCoordination
using Avalonia.Media;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Scenarios;

public static class GitNavigationScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("git-navigation-cached-ignore", "Ignored names stay gray while Git refreshes after navigation", async c =>
        {
            var ignored = Path.Combine(c.LeftPath, "ignored");
            var nested = Path.Combine(ignored, "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(ignored, "generated.txt"), "generated");
            File.WriteAllText(Path.Combine(ignored, "tracked.txt"), "tracked");
            File.WriteAllText(Path.Combine(nested, "deeper.txt"), "generated");
            var reader = new DelayedGitReader { Root = c.LeftPath };
            var panel = new PanelView("cached-ignore", new LocalFileSystem(), null, reader);
            await panel.LoadAsync(c.LeftPath);
            var snapshot = new GitStatus("outer", 0, 0, 0, 0, 0, 0)
            {
                Root = c.LeftPath, IgnoredPaths = ["ignored/"], TrackedIgnoredPaths = ["ignored/tracked.txt"]
            };
            reader.Requests[0].SetResult(snapshot);
            await panel.Git.PendingWork;
            await panel.LoadAsync(ignored);
            var oldRead = panel.Git.PendingWork;
            Check(!oldRead.IsCompleted, "Navigation completes while the full Git read is delayed");
            Equal(Color.Parse("#7B8DA5"), ((ISolidColorBrush)panel.Rows.Single(row => row.Name == "generated.txt").NameForeground).Color,
                "The first displayed file name is already gray");
            Equal(Brushes.White, panel.Rows.Single(row => row.Name == "tracked.txt").NameForeground, "Tracked exceptions retain their normal name color immediately");
            Equal("!", panel.Rows.Single(row => row.Name == "tracked.txt").Git.WorkingGlyph, "Tracked exceptions retain their warning immediately");
            await panel.LoadAsync(nested);
            Check(panel.Rows.Single(row => row.Name == "deeper.txt").Git.Ignored, "Fast navigation into deeper folders retains the inherited ignore state");
            reader.Requests[2].SetResult(snapshot with { IgnoredPaths = [] });
            await panel.Git.PendingWork;
            Equal(Brushes.White, panel.Rows.Single(row => row.Name == "deeper.txt").NameForeground, "Fresh ignore rules replace the cached color");
            reader.Requests[1].SetResult(snapshot);
            await oldRead;
            Check(!panel.Rows.Single(row => row.Name == "deeper.txt").Git.Ignored, "An obsolete Git result cannot restore the old ignore rule");
            await panel.LoadAsync(ignored);
            Check(!panel.Rows.Single(row => row.Name == "generated.txt").Git.Ignored, "Returning uses the refreshed snapshot");
            reader.Requests[3].SetResult(snapshot);
            await panel.Git.PendingWork;
            reader.Root = nested;
            await panel.LoadAsync(nested);
            Check(panel.Git.Status == null && panel.Rows.All(row => !row.Git.Ignored), "An unknown nested repository does not inherit its parent's ignore state");
            reader.Requests[4].SetResult(new("inner", 0, 0, 0, 0, 0, 0) { Root = nested });
            await panel.Git.PendingWork;
            reader.Root = c.LeftPath;
            await panel.LoadAsync(ignored);
            Check(panel.Rows.Single(row => row.Name == "generated.txt").Git.Ignored, "Returning from a nested repository restores the outer snapshot before Git finishes");
            reader.Requests[5].SetResult(snapshot);
            await panel.Git.PendingWork;
            panel.Icons.Dispose();
        }),
        new("git-navigation-root-boundaries", "The fast root check respects nested Git directories, Git files and metadata paths", async c =>
        {
            var root = Path.Combine(c.LeftPath, "repository");
            var nested = Path.Combine(root, "ignored", "inner");
            var descendant = Path.Combine(nested, "deep");
            Directory.CreateDirectory(Path.Combine(root, ".git", "objects"));
            Directory.CreateDirectory(descendant);
            var reader = new GitStatusReader();
            Equal(root, await reader.FindRootAsync(descendant, default), "Ordinary descendants use their nearest repository");
            Directory.CreateDirectory(Path.Combine(nested, ".git"));
            Equal(nested, await reader.FindRootAsync(descendant, default), "A nested repository directory stops inheritance");
            Directory.Delete(Path.Combine(nested, ".git"));
            File.WriteAllText(Path.Combine(nested, ".git"), "gitdir: elsewhere");
            Equal(nested, await reader.FindRootAsync(descendant, default), "A worktree Git file also stops inheritance");
            Equal<string?>(null, await reader.FindRootAsync(Path.Combine(root, ".git", "objects"), default), "Git metadata is not a working tree");
        }),
        new("git-navigation-refresh-waits-for-directory", "Refresh defers while navigation prepares its first display", async c =>
        {
            var destination = Path.Combine(c.LeftPath, "folder1");
            File.WriteAllText(Path.Combine(destination, "loaded.txt"), "Next directory");
            var reader = new DelayedGitReader { Root = c.LeftPath };
            var panel = new PanelView("refresh-during-navigation", new LocalFileSystem(), null, reader);
            await panel.LoadAsync(c.LeftPath);
            reader.Requests[0].SetResult(new("cached", 0, 0, 0, 0, 0, 0) { Root = c.LeftPath });
            await panel.Git.PendingWork;

            var root = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.RootRead = root;
            var navigation = panel.LoadAsync(destination);
            bool refreshed;
            bool keptRows;
            try
            {
                await WaitAsync(() => !panel.Git.DirectoryReady.IsCompleted);
                refreshed = await panel.RefreshAsync(() => true, () => false).WaitAsync(TimeSpan.FromSeconds(5));
                keptRows = panel.Rows.Any(row => row.Name == "alpha.txt");
            }
            finally { root.TrySetResult(c.LeftPath); }
            Check(await navigation.WaitAsync(TimeSpan.FromSeconds(5)), "Navigation publishes the destination after Git is ready.");
            await WaitAsync(() => reader.Requests.Count == 2);
            reader.Requests[1].SetResult(null);
            await panel.Git.PendingWork;
            panel.Icons.Dispose();

            Check(!refreshed && keptRows, "Refresh leaves the displayed rows in place while navigation prepares.");
            Check(panel.Rows.Any(row => row.Name == "loaded.txt"), "The destination rows appear after navigation.");
        }),
        new("git-navigation-obsolete-root", "An obsolete root check cannot apply its cached status or launch Git", async c =>
        {
            var reader = new DelayedGitReader { Root = c.LeftPath };
            var view = new GitStatusView("root-check", reader);
            view.SetDirectory(c.LeftPath);
            reader.Requests[0].SetResult(new("cached", 0, 0, 0, 0, 0, 0) { Root = c.LeftPath });
            await view.PendingWork;
            var delayedRoot = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.RootRead = delayedRoot;
            view.SetDirectory(Path.Combine(c.LeftPath, "folder1"));
            var oldRead = view.PendingWork;
            reader.RootRead = null;
            reader.Root = null;
            view.SetDirectory(c.RightPath);
            await view.DirectoryReady;
            reader.Requests[1].SetResult(null);
            await view.PendingWork;
            delayedRoot.SetResult(c.LeftPath);
            await oldRead;
            Check(view.Status == null, "A late root result cannot restore another repository");
            Equal(2, reader.Requests.Count, "An obsolete root result does not launch a full status read");
        })
    ];
}

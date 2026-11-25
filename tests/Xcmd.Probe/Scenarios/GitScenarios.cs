// @tests ReadablePanels, MarkEntries, ViewSelectionSize, SeeRepositoryState, RepositoryStatus
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Media;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Features.Panels.Git;
using Xcmd.Desktop.Shared.Ui;
using Xcmd.Infrastructure;
using Xcmd.Core;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class GitScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("git-workflows-polling", "Panels share workflow results and poll active runs more often", async c =>
        {
            var target = new WorkflowTarget("github.com/team/project", "main", false);
            var clock = new WorkflowFixture();
            var first = new TaskCompletionSource<WorkflowStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var cache = new WorkflowStatusCache(_ => { Interlocked.Increment(ref calls); return first.Task; }, clock);
            var a = cache.ReadAsync(target);
            var b = cache.ReadAsync(target);
            var failedRun = new Uri("https://github.com/team/project/actions/runs/400");
            var active = WorkflowStatus.FromRuns("current", [new("1", "Build", "current", "in_progress", ""), new("2", "Tests", "current", "completed", "failure", failedRun),
                new("2", "Tests", "older", "completed", "success")]);
            first.SetResult(active);
            await a;
            await b;
            Equal(1, calls, "Both panels share an in-flight request");
            Equal("Failed", active.Label, "A failed workflow is visible while another is still running");
            Equal(failedRun, active.RunUrl, "The failed badge links to the failed workflow run");
            Equal(TimeSpan.FromSeconds(30), active.RefreshAfter, "A pending workflow receives a fast refresh");
            clock.Advance(TimeSpan.FromSeconds(29)); await cache.ReadAsync(target);
            Equal(1, calls, "Repeated directory reads reuse the cached result");
            clock.Advance(TimeSpan.FromSeconds(1)); await cache.ReadAsync(target);
            Equal(2, calls, "A visible panel refreshes when the active interval expires");
            Equal(TimeSpan.FromMinutes(5), WorkflowStatus.FromRuns("current", [new("1", "Build", "current", "completed", "success")]).RefreshAfter, "Completed work polls less often");
            Equal("", WorkflowStatus.Empty(false).Label, "Repositories without workflows have no badge");
            Equal("Unknown", WorkflowStatus.Empty(true).Label, "A commit with no runs has an unknown result");
        }),
        new("git-workflows-panel", "A GitHub remote shows branch results and late responses cannot follow panel navigation", async c =>
        {
            var root = Directory.CreateDirectory(Path.Combine(c.FixturePath, "workflow-repo")).FullName;
            await GitFixture.InitializeAsync(root);
            await GitFixture.RunAsync(root, "remote", "add", "origin", "git@github.com:team/project.git");
            var requested = new TaskCompletionSource<WorkflowTarget>(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = new TaskCompletionSource<WorkflowStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cache = new WorkflowStatusCache(target => { requested.TrySetResult(target); return response.Task; }, TimeProvider.System);
            var view = new GitStatusView("workflow-probe", workflowCache: cache);
            var host = new Window { Content = view, Width = 450, Height = 180 };
            var opened = new List<ExternalLaunch>();
            view.WorkflowRunRequested += opened.Add;
            var runUrl = new Uri("https://github.com/team/project/actions/runs/123");
            try
            {
                host.Show(); host.Activate();
                view.SetDirectory(root);
                var target = await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Equal(new WorkflowTarget("github.com/team/project", "main", false), target, "The SSH remote and local branch select the GitHub query");
                view.SetDirectory("");
                response.SetResult(WorkflowStatus.FromRuns("abcdef12", [new("1", "Verify", "abcdef12", "completed", "success", runUrl)]));
                await c.UpdateLayoutAsync();
                Check(WorkflowFixture.Indicator(view)?.IsVisible != true, "A late result stays hidden outside the repository");
                view.SetDirectory(root);
                await WorkspaceProbe.WaitAsync(() => WorkflowFixture.Shows(view, "Passed", "●"));
                var indicator = WorkflowFixture.Indicator(view)!;
                Check(WorkflowFixture.Details(view).Contains("Verify: success", StringComparison.Ordinal), "The CI indicator describes each workflow");
                await c.ScreenshotAsync("git-workflow-status", host);
                File.WriteAllText(Path.Combine(root, "untracked.txt"), "change");
                await GitFixture.RefreshAsync(view);
                view.MaxWidth = 190;
                host.SetScaleForTest(1.5);
                await c.ScreenshotAsync("git-workflow-changes-150", host);
                await c.ClickAsync(indicator, target: host);
                Equal(runUrl.AbsoluteUri, opened.Single().Target, "The CI link works in a narrow header at 150% scale");
                Check(!view.IsMenuOpen, "Clicking CI leaves the repository menu closed");
                await c.ClickAsync(view.ActionsButton, target: host);
                Check(view.IsMenuOpen, "The adjacent branch and change counts open repository actions");
            }
            finally { view.SetDirectory(""); host.Close(); await view.PendingWork; GitFixture.AllowCleanup(root); }
        }),
        new("git-workflows-open-run", "Open the displayed GitHub Actions run from the workspace", async c =>
        {
            await GitFixture.InitializeAsync(c.RightPath);
            await GitFixture.RunAsync(c.RightPath, "remote", "add", "origin", "https://" + WorkflowFixture.LinkRepository + ".git");
            var view = c.Window.Right.Git;
            await GitFixture.RefreshAsync(view);
            await WorkspaceProbe.WaitAsync(() => WorkflowFixture.Shows(view, "Running", "●"));
            var indicator = WorkflowFixture.Indicator(view)!;
            var expected = new ExternalLaunch(ExternalLaunchKind.Url, WorkflowFixture.RunningRun, c.RightPath);
            await c.SelectAsync("alpha.txt");
            await c.ClickAsync(indicator);
            Equal(expected, c.Launcher.Requests.Single(), "Clicking CI sends the exact running workflow URL to the browser launcher");
            Check(!view.IsMenuOpen, "The workflow link leaves the repository menu closed");
            Check(c.Window.ActivePanel == c.Window.Left, "The workflow link leaves the active file panel unchanged");
            Check(indicator.Focus(), "A workflow link can receive keyboard focus");
            c.Input.KeyDown(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            c.Input.KeyDown(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            c.Input.KeyUp(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await c.SettleAsync();
            Equal(expected, c.Launcher.Requests.Last(), "Enter on the focused workflow link opens the same run");
            Equal(2, c.Launcher.Requests.Count, "Holding Enter opens the run once and leaves the selected file closed");
            await c.KeyAsync(Key.Enter);
            Equal(ExternalLaunchKind.File, c.Launcher.Requests.Last().Kind, "A fresh Enter opens the selected file after focus returns to the panel");
        }),
        new("git-workflows-remote-tip", "The badge follows the remote commit after a push and ignores runs for older commits", async c =>
        {
            var root = Directory.CreateDirectory(Path.Combine(c.FixturePath, "remote-tip")).FullName;
            await GitFixture.InitializeAsync(root);
            await GitFixture.RunAsync(root, "remote", "add", "origin", "https://github.com/team/project.git");
            await WorkflowFixture.AdvanceRemoteAsync(root, "first");
            var remote = new WorkflowRemote();
            var clock = new WorkflowFixture();
            var cache = new WorkflowStatusCache(target => GitHubWorkflows.ReadAsync(target, remote.RunAsync), clock);
            var view = new GitStatusView("workflow-tip", workflowCache: cache);
            var host = new Window { Content = view, Width = 450, Height = 180 };
            var opened = new List<ExternalLaunch>();
            view.WorkflowRunRequested += opened.Add;
            try
            {
                host.Show(); host.Activate();
                view.SetDirectory(root);
                await WorkspaceProbe.WaitAsync(() => WorkflowFixture.Shows(view, "Passed", "●"));
                var indicator = WorkflowFixture.Indicator(view)!;
                await c.ClickAsync(indicator, target: host);
                Equal("https://github.com/team/project/actions/runs/123", opened.Single().Target, "The completed badge uses the GitHub CLI run URL");
                var previous = view.Status!.WorkflowTarget!.TrackingCommit;
                remote.DelayTip();
                await WorkflowFixture.AdvanceRemoteAsync(root, "second");
                await GitFixture.RefreshAsync(view);
                Check(view.Status!.WorkflowTarget!.TrackingCommit != previous, "A push changes the cache key before the completed run expires");
                Check(WorkflowFixture.Shows(view, "Unknown", "○"), "The old pass clears while the new tip is being checked");
                remote.ReturnTip("next");
                await WorkspaceProbe.WaitAsync(() => WorkflowFixture.Details(view).Contains("No workflow runs", StringComparison.Ordinal));
                Equal("next", remote.RequestedCommit, "Query workflow runs for the live remote commit");
                Check(WorkflowFixture.Shows(view, "Unknown", "○"), "An old successful run cannot turn the new tip green");
                await c.ClickAsync(indicator, target: host);
                Equal(1, opened.Count, "An unknown badge cannot open an old run");
                await c.ScreenshotAsync("git-workflow-unknown", host);
                remote.RunCommit = "next";
                remote.Conclusion = "failure";
                clock.Advance(TimeSpan.FromSeconds(30));
                await GitFixture.RefreshAsync(view);
                await WorkspaceProbe.WaitAsync(() => WorkflowFixture.Shows(view, "Failed", "×"));
                await c.ClickAsync(indicator, target: host);
                Equal("https://github.com/team/project/actions/runs/456", opened.Last().Target, "The new tip opens its own run");
                await c.ScreenshotAsync("git-workflow-failed", host);
            }
            finally { remote.ReturnTip("next"); view.SetDirectory(""); host.Close(); await view.PendingWork; GitFixture.AllowCleanup(root); }
        }),
        new("git-panel-status", "Repository branch and compact status share the path header without changing table geometry", async c =>
        {
            var initialTableBounds = c.Window.Left.Table.Bounds;
            await GitFixture.RunAsync(c.LeftPath, "init", "-b", "feature/панель");
            await GitFixture.RunAsync(c.LeftPath, "config", "core.longpaths", "true");
            await GitFixture.RunAsync(c.LeftPath, "add", ".");
            await GitFixture.RunAsync(c.LeftPath, "-c", "user.name=Probe", "-c", "user.email=probe@example.test", "commit", "-m", "fixture");
            var view = c.Window.Left.Git;
            await GitFixture.RefreshAsync(view);
            Equal("feature/панель", view.Status?.Branch, "Branch name");
            Check(!view.Summary.Contains("✓", StringComparison.Ordinal), "Clean repository uses branch colour without a fallback-font checkmark");
            var cleanBranch = view.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "feature/панель");
            Check(cleanBranch.Foreground is ISolidColorBrush green && green.Color.G > green.Color.R, "Clean branch is green");
            File.AppendAllText(Path.Combine(c.LeftPath, "alpha.txt"), "changed");
            File.WriteAllText(Path.Combine(c.LeftPath, "new.txt"), "new");
            await GitFixture.RunAsync(c.LeftPath, "mv", "zeta.md", "renamed.md");
            await GitFixture.RefreshAsync(view);
            Equal(1, view.Status?.Staged, "Staged rename");
            Equal(1, view.Status?.Changed, "Unstaged change");
            Equal(1, view.Status?.Untracked, "Untracked file");
            await c.ScreenshotAsync("git-status");
            var header = view.GetVisualAncestors().OfType<Grid>().First(control => control.Name == c.Window.Left.Id + "LocationHeader");
            Check(header.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains(c.Window.Left.DisplayPath, StringComparison.Ordinal) == true), "Git and path share the location header");
            Check(view.Summary.Contains("+1 ~1 ?1", StringComparison.Ordinal), "Staged, changed and untracked counters use compact symbols");
            Equal(initialTableBounds, c.Window.Left.Table.Bounds, "Git result does not move or resize the file table");
            Equal(c.Window.Left.Table.Bounds.Y, c.Window.Right.Table.Bounds.Y, "Panel tables stay aligned");
            await c.KeyAsync(Key.A, RawInputModifiers.Control);
            var selectedBytes = c.Window.Left.Rows.Where(row => !row.Entry.IsDirectory && !row.Entry.IsParent).Sum(row => row.Entry.Size);
            Check(c.Window.Left.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains(FileSizeText.Format(selectedBytes), StringComparison.Ordinal) == true), "Selection summary totals regular-file sizes without counting directories");
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            await view.PendingWork;
            Equal(1, view.Status?.Changed, "Subfolder reports the whole repository");
            await GitFixture.RunAsync(c.LeftPath, "checkout", "--detach");
            await GitFixture.RefreshAsync(view);
            Check(view.Status!.Branch.StartsWith("detached ", StringComparison.Ordinal), "Detached HEAD uses commit identity");
            var empty = Path.Combine(Path.GetTempPath(), "xcmd-no-git-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            try
            {
                await c.Window.Left.LoadAsync(empty);
                await view.PendingWork;
                Check(!view.IsVisible && !view.ActionsButton.IsVisible && view.Status == null && view.Summary.Length == 0, "No repository hides the complete Git button");
                await c.SettleAsync();
                Equal(initialTableBounds, c.Window.Left.Table.Bounds, "Leaving Git preserves table geometry at the same scale");
                foreach (var scale in new[] { 1d, 1.5d })
                {
                    c.Window.SetScaleForTest(scale);
                    await c.ScreenshotAsync("git-empty-row-" + scale * 100);
                    Check(!view.IsVisible && !view.ActionsButton.IsVisible, "The empty button stays hidden at each display scale");
                    Equal(initialTableBounds.Y, c.Window.Left.Table.Bounds.Y, "Hiding Git keeps the table's vertical position at each scale");
                    Equal(c.Window.Left.Table.Bounds.Y, c.Window.Right.Table.Bounds.Y, "Headers stay aligned with and without Git");
                }
            }
            finally
            {
                await c.Window.Left.LoadAsync(c.LeftPath);
                Directory.Delete(empty);
                GitFixture.AllowCleanup(c.LeftPath);
            }
        }),
        new("git-remote-colours", "A cloud indicates any remote; real diverged history reports ahead and behind, and deletion has its own count", async c =>
        {
            await GitFixture.RunAsync(c.LeftPath, "init", "-b", "main");
            await GitFixture.RunAsync(c.LeftPath, "config", "core.longpaths", "true");
            await GitFixture.RunAsync(c.LeftPath, "config", "user.name", "Probe");
            await GitFixture.RunAsync(c.LeftPath, "config", "user.email", "probe@example.test");
            try
            {
                await GitFixture.RunAsync(c.LeftPath, "add", ".");
                await GitFixture.RunAsync(c.LeftPath, "commit", "-m", "initial");
                await GitFixture.RunAsync(c.LeftPath, "remote", "add", "origin", ".");
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                Check(c.Window.Left.Git.Status!.HasRemote, "Remote exists even without a tracking branch");
                Check(c.Window.Left.Git.GetVisualDescendants().OfType<PathIcon>().Single(icon => icon.GetValue(FeedbackTarget.IdProperty) == "GitStatusView.remote").IsVisible, "Remote cloud is visible");
                await c.ScreenshotAsync("git-clean-remote");
                await GitFixture.RunAsync(c.LeftPath, "update-ref", "refs/remotes/origin/main", "HEAD");
                await GitFixture.RunAsync(c.LeftPath, "branch", "--set-upstream-to=origin/main", "main");
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                Check(!c.Window.Left.Git.Summary.Contains('↑') && !c.Window.Left.Git.Summary.Contains('↓'), "Synchronized upstream has no zero arrows");
                File.AppendAllText(Path.Combine(c.LeftPath, "alpha.txt"), "local");
                await GitFixture.RunAsync(c.LeftPath, "commit", "-am", "local");
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                Check(c.Window.Left.Git.Summary.Contains("↑1", StringComparison.Ordinal) && !c.Window.Left.Git.Summary.Contains('↓'), "Ahead-only state hides the zero behind count");
                await GitFixture.RunAsync(c.LeftPath, "checkout", "-b", "remote-work", "HEAD~1");
                File.AppendAllText(Path.Combine(c.LeftPath, "zeta.md"), "remote");
                await GitFixture.RunAsync(c.LeftPath, "commit", "-am", "remote");
                await GitFixture.RunAsync(c.LeftPath, "update-ref", "refs/remotes/origin/main", "HEAD");
                await GitFixture.RunAsync(c.LeftPath, "checkout", "main");
                File.Delete(Path.Combine(c.LeftPath, "file2.log"));
                File.AppendAllText(Path.Combine(c.LeftPath, "alpha.txt"), "changed");
                File.WriteAllText(Path.Combine(c.LeftPath, "new.txt"), "new");
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                var status = c.Window.Left.Git.Status!;
                Equal(1, status.Ahead, "Actual local-only commit count");
                Equal(1, status.Behind, "Actual remote-only commit count");
                Equal("origin/main", status.Upstream, "Configured upstream");
                Equal(1, status.Deleted, "Deleted file gets a distinct red counter");
                Equal(1, status.Changed, "Deleted files do not inflate modified count");
                await c.ScreenshotAsync("git-diverged-deleted");
            }
            finally { GitFixture.AllowCleanup(c.LeftPath); }
        }),
        new("git-child-background-navigation", "A delayed child read does not hide the current repository or survive navigation", async c =>
        {
            var children = new TaskCompletionSource<GitStatus[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reader = new DelayedGitReader { Children = children };
            var view = new GitStatusView("child-probe", reader);
            view.SetDirectory(c.LeftPath);
            var previous = view.PendingWork;
            reader.Requests[0].SetResult(new("ready", 0, 0, 0, 0, 0, 0) { Root = c.LeftPath });
            await FileOperationProbe.WaitUntilAsync(() => reader.ChildReadStarted.Task.IsCompleted);
            Equal("ready", view.Status?.Branch, "The current repository is visible while child statuses are pending");
            Check(view.IsVisible && !view.PendingWork.IsCompleted, "The current header does not wait for a slow child repository");
            reader.Children = null;
            view.SetDirectory(c.RightPath);
            reader.Requests[1].SetResult(null);
            await view.PendingWork;
            children.SetResult([new("obsolete", 1, 0, 0, 0, 0, 0) { Root = c.LeftPath }]);
            await previous;
            Equal(0, view.ChildRepositories.Length, "Navigation discards obsolete child repository statuses");
            Check(view.Status == null && !view.IsVisible, "A non-repository directory keeps its header empty");
        }),
        new("git-background-navigation", "Slow Git work never blocks keyboard input; overlapping refreshes coalesce and obsolete results are discarded", async c =>
        {
            var reader = new DelayedGitReader();
            var view = new GitStatusView("probe", reader);
            view.SetDirectory(c.LeftPath);
            var old = view.PendingWork;
            view.Refresh(); view.Refresh();
            Equal(1, reader.Requests.Count, "Refreshes do not accumulate");
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Enter);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "Keyboard navigation works during pending Git");
            view.SetDirectory(c.RightPath);
            reader.Requests[1].SetResult(new GitStatus("new", 0, 0, 0, 0, 2, 1));
            await view.PendingWork;
            reader.Requests[0].SetResult(new GitStatus("obsolete", 9, 9, 0, 0, 0, 0));
            await old;
            Equal("new", view.Status?.Branch, "Late result cannot replace current location");
            Check(view.Summary.Contains("↑2", StringComparison.Ordinal) && view.Summary.Contains("↓1", StringComparison.Ordinal), "Upstream divergence is concise");
            view.SetDirectory(c.LeftPath);
            reader.Requests[2].SetResult(null);
            await view.PendingWork;
            Check(!view.IsVisible && !view.ActionsButton.IsVisible, "A null repository result removes the entire previous control");
            view.SetDirectory("");
            Check(!view.IsVisible && view.Summary.Length == 0, "Search and non-directory locations hide Git actions");
        })
    ];
}

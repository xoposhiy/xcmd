// @tests SeeRepositoryState, DelegateCommit, SyncRepository, ExternalLaunch
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels.Git;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class GitCommitScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("git-switch-branch-recent", "Switch from four dated local branches at the bottom of the repository menu", async c =>
        {
            using var repo = new GitBranchFixture(c);
            await repo.PrepareAsync();
            await repo.AddMoreBranchesAsync();
            File.AppendAllText(repo.FilePath("alpha.txt"), "local edit");
            var items = await repo.OpenRecentAsync();
            Check(items.Select(item => item.Header?.ToString()).SequenceEqual(new[]
            {
                GitBranchFixture.Topic + " · 2025-05-03", "another_branch · 2025-05-02", "main · 2025-05-01",
                "z-older · 2025-05-01", "_Other branches…"
            }), "The four newest tips appear in commit date order above Other branches");
            Check(items[2].IsChecked && !items[2].IsEnabled, "The current branch is marked");
            await c.ScreenshotAsync("recent-branches");
            await c.ClickAsync(items[3]);
            Equal("z-older", c.Window.Left.Git.Status?.Branch, "One click switches to the fourth branch");
            Check(!c.Window.HasOpenDialog && File.ReadAllText(repo.FilePath("alpha.txt")).EndsWith("local edit", StringComparison.Ordinal),
                "A compatible edit follows the switch without another dialog");
            var dialog = await repo.OpenAsync();
            Check(GitBranchFixture.VisibleRows(dialog).SequenceEqual([
                (GitBranchFixture.Topic, "2025-05-03"), ("another_branch", "2025-05-02"),
                ("main", "2025-05-01"), ("z-older", "2025-05-01"), ("z-oldest", "2025-05-01")
            ]), "The picker shows all branch names beside their commit dates, newest first, with ties sorted by name");
            Check(!GitBranchFixture.Message(dialog).Contains("Switching keeps", StringComparison.Ordinal),
                "Opening the picker does not show switch advice");
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("git-switch-branch", "Browse local branches and switch with staged, unstaged and untracked changes", async c =>
        {
            using var repo = new GitBranchFixture(c);
            await repo.PrepareAsync();
            await c.Window.Right.LoadAsync(c.LeftPath);
            await GitFixture.RefreshAsync(c.Window.Right.Git);
            File.WriteAllText(repo.FilePath("alpha.txt"), "staged");
            await GitFixture.RunAsync(c.LeftPath, "add", "alpha.txt");
            File.AppendAllText(repo.FilePath("alpha.txt"), " and unstaged");
            File.WriteAllText(repo.FilePath("notes.txt"), "untracked");

            var dialog = await repo.OpenAsync();
            Equal("main", GitBranchFixture.SelectedBranch(dialog), "The list starts on the current branch");
            await c.ScreenshotAsync("local-branches", dialog);
            await c.KeyAsync(Key.F12, target: dialog);
            await c.ClickAsync(GitBranchFixture.List(dialog), target: dialog);
            Check(c.Window.Feedback.ActiveComment != null, "The branch list accepts feedback");
            await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveComment);
            await c.KeyAsync(Key.Escape, target: dialog);
            Check(!c.Window.HasOpenDialog && !File.Exists(repo.FilePath("topic.txt")), "Escape leaves the checkout unchanged");

            dialog = await repo.OpenAsync();
            await repo.SwitchToTopicAsync(dialog);
            Equal(GitBranchFixture.Topic, c.Window.Left.Git.Status?.Branch, "The source panel shows the new branch");
            Equal(GitBranchFixture.Topic, c.Window.Right.Git.Status?.Branch, "The other panel in the repository updates too");
            Equal(1, c.Window.Left.Git.Status?.Staged, "The index retains the staged change");
            Equal(1, c.Window.Left.Git.Status?.Changed, "The working tree retains the unstaged change");
            Equal("staged and unstaged", File.ReadAllText(repo.FilePath("alpha.txt")), "Working file content survives");
            Equal("untracked", File.ReadAllText(repo.FilePath("notes.txt")), "Untracked notes survive");
            Check(c.Window.Right.Rows.Any(row => row.Name == "topic.txt"), "Files from the selected branch appear in the panel");
        }),
        new("git-switch-branch-conflicts", "Protect tracked edits and ignored files that the selected branch would overwrite", async c =>
        {
            using var repo = new GitBranchFixture(c);
            await repo.PrepareAsync();
            File.WriteAllText(repo.FilePath("zeta.md"), "local work");
            var items = await repo.OpenRecentAsync();
            Equal(3, items.Length, "Two local branches and Other branches are visible");
            await c.ClickAsync(items[0]);
            var dialog = c.Window.OperationDialog ?? throw new InvalidOperationException("The blocked switch did not show the Git error.");
            Equal(GitBranchFixture.Topic, GitBranchFixture.SelectedBranch(dialog), "The blocked branch stays selected for retry");
            Check(dialog.IsVisible && GitBranchFixture.Message(dialog).Contains("zeta.md", StringComparison.Ordinal),
                "Git names the conflicting tracked file in the picker");
            Equal("local work", File.ReadAllText(repo.FilePath("zeta.md")), "The conflicting edit survives");
            await GitFixture.RunAsync(c.LeftPath, "restore", "zeta.md");
            File.WriteAllText(repo.FilePath("topic.txt"), "untracked work");
            await repo.SwitchToTopicAsync(dialog);
            Check(dialog.IsVisible && GitBranchFixture.Message(dialog).Contains("topic.txt", StringComparison.Ordinal),
                "Git names the untracked file that would be overwritten");
            Equal("untracked work", File.ReadAllText(repo.FilePath("topic.txt")), "The untracked file survives");
            File.WriteAllText(repo.FilePath(".gitignore"), "topic.txt\n");
            File.WriteAllText(repo.FilePath("topic.txt"), "ignored work");
            await repo.SwitchToTopicAsync(dialog);
            Check(dialog.IsVisible && GitBranchFixture.Message(dialog).Contains("topic.txt", StringComparison.Ordinal),
                "Git names the ignored file that would be overwritten");
            Equal("ignored work", File.ReadAllText(repo.FilePath("topic.txt")), "The ignored file survives");
            await c.ScreenshotAsync("branch-switch-blocked", dialog);
            await c.KeyAsync(Key.Escape, target: dialog);
            await GitFixture.RefreshAsync(c.Window.Left.Git);
            Equal("main", c.Window.Left.Git.Status?.Branch, "The original branch stays checked out");
        }),
        new("git-remote-commands", "Push and pull the current repository and choose how to join diverged branches", async c =>
        {
            using var repository = await GitRemoteFixture.CreateAsync(c);
            var status = c.Window.Left.Git;
            await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "folder1"));
            await GitFixture.RefreshAsync(status);

            await c.ClickAsync(status.ActionsButton);
            var push = GitRemoteFixture.Menu(status, "Push");
            Check(push.IsEffectivelyEnabled, "A repository with a remote offers Push");
            await c.ClickAsync(push);
            Check(c.Window.LastError == null, "Push sends the local commit from the panel's repository");
            await GitFixture.RefreshAsync(status);
            await repository.ClonePeerAsync();

            await repository.CommitAsync(repository.Peer, "remote-first.txt");
            await GitFixture.RunAsync(repository.Peer, "push");
            await c.ClickAsync(status.ActionsButton);
            await c.ClickAsync(GitRemoteFixture.Menu(status, "Pull"));
            Check(File.Exists(Path.Combine(c.LeftPath, "remote-first.txt")), "Plain Pull fast-forwards the local branch");

            await repository.CommitAsync(c.LeftPath, "local.txt");
            await GitFixture.RunAsync(c.LeftPath, "branch", "--track", "merge-probe", "origin/main");
            await GitFixture.RunAsync(c.LeftPath, "branch", "-f", "merge-probe", "HEAD");
            await repository.CommitAsync(repository.Peer, "remote-second.txt");
            await GitFixture.RunAsync(repository.Peer, "push");
            await GitFixture.RunAsync(c.LeftPath, "fetch");
            await GitFixture.RefreshAsync(status);
            await c.ClickAsync(status.ActionsButton);
            Check(!GitRemoteFixture.Menu(status, "Pull").IsVisible, "Plain Pull is replaced when the branches diverge");
            Check(GitRemoteFixture.Menu(status, "PullMerge").IsVisible, "The menu offers a merge");
            await c.ClickAsync(GitRemoteFixture.Menu(status, "PullRebase"));
            Check(c.Window.LastError == null && File.Exists(Path.Combine(c.LeftPath, "remote-second.txt")), "Pull with rebase includes the upstream changes");
            await GitFixture.RunAsync(c.LeftPath, "merge-base", "--is-ancestor", "origin/main", "HEAD^");

            await GitFixture.RunAsync(c.LeftPath, "switch", "merge-probe");
            await GitFixture.RunAsync(c.LeftPath, "config", "pull.ff", "only");
            await GitFixture.RefreshAsync(status);
            await c.ClickAsync(status.ActionsButton);
            await c.ClickAsync(GitRemoteFixture.Menu(status, "PullMerge"));
            Check(c.Window.LastError == null && File.Exists(Path.Combine(c.LeftPath, "remote-second.txt")), "Pull with merge includes the upstream changes");
            await GitFixture.RunAsync(c.LeftPath, "merge-base", "--is-ancestor", "origin/main", "HEAD^2");
        }),
        new("git-commit-menu", "UC28/UC37; Git status opens Commit and launches interactive Sonnet with medium effort at the repository root", async c =>
        {
            await GitFixture.RunAsync(c.LeftPath, "init", "-b", "main");
            await GitFixture.RunAsync(c.RightPath, "init", "-b", "other");
            try
            {
                await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "folder1"));
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                c.Window.CommandLine.Editor.Text = "unfinished command";
                foreach (var scale in new[] { 1d, 1.5d })
                {
                    c.Window.SetScaleForTest(scale);
                    await c.SettleAsync();
                    GitHeaderProbe.CheckAlignment(c.Window.Left);
                    await c.ClickAsync(c.Window.Left.Git.ActionsButton);
                    Check(c.Window.IsMenuOpen && c.Window.Left.Git.CommitCommand.IsVisible, "Git actions open on a normal left click");
                    Equal("_Commit repository with Claude Code…", c.Window.Left.Git.CommitCommand.Header as string, "The command names the repository scope and external tool");
                    GitHeaderProbe.CheckMenuPlacement(c.Window.Left, c.Window);
                    await c.ScreenshotAsync("commit-menu-" + scale * 100);
                    await c.KeyAsync(Key.Escape);
                    Check(!c.Window.IsMenuOpen, "Escape dismisses repository actions");
                }
                Equal(0, c.Launcher.Requests.Count, "Opening and dismissing the menu never starts an agent");
                await c.ClickAsync(c.Window.Left.Git.ActionsButton);
                await c.ClickAsync(c.Window.Left.Git.CommitCommand);
                var launch = c.Launcher.Requests.Single();
                Equal(c.LeftPath, launch.WorkingDirectory, "Commit from a subdirectory uses the whole repository");
                Equal(ExternalLaunchKind.Command, launch.Kind, "Use the independent external-program launcher");
                Equal("--model sonnet --effort medium \"Commit all changes.\"", launch.Arguments, "Only model, effort and the brief initial prompt are passed");
                Check(!c.Window.IsBusy && !c.Window.HasOpenDialog && c.Window.IsEnabled, "The file manager does not wait for the agent session");
                Equal("unfinished command", c.Window.CommandLine.Editor.Text, "Commit leaves the command draft intact");
                await c.KeyAsync(Key.Tab);
                await GitFixture.RefreshAsync(c.Window.Right.Git);
                c.Window.Activate(c.Window.Left);
                await c.ClickAsync(c.Window.Right.Git.ActionsButton);
                Equal(c.Window.Right, c.Window.ActivePanel, "The clicked repository activates its own panel");
                await c.ClickAsync(c.Window.Right.Git.CommitCommand);
                Equal(c.RightPath, c.Launcher.Requests.Last().WorkingDirectory, "The other status commits its own repository");
                await c.KeyAsync(Key.Down);
                Check(!c.Window.IsBusy, "Panel input remains usable after starting Claude Code");
            }
            finally { GitFixture.AllowCleanup(c.LeftPath); GitFixture.AllowCleanup(c.RightPath); }
        }),
        new("git-commit-launch-errors", "UC28; unavailable Claude Code and launch failures remain visible and leave panels usable", async c =>
        {
            Check(!new GitStatusView("unknown-repository").ActionsButton.IsEnabled, "An unknown repository has no actionable Git status");
            await GitFixture.RunAsync(c.LeftPath, "init", "-b", "main");
            try
            {
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                c.Launcher.Failure = new IOException("Claude Code launch failed (probe)");
                await c.ClickAsync(c.Window.Left.Git.ActionsButton);
                await c.ClickAsync(c.Window.Left.Git.CommitCommand);
                Equal("Claude Code launch failed (probe)", c.Window.LastError, "The launch error is visible in the main window");
                Check(!c.Window.IsBusy && !c.Window.HasOpenDialog, "A launch failure releases the workflow");
                await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter);
                Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "Navigation still works after the failure");
                try
                {
                    await new ClaudeCommitLauncher(Path.Combine(c.FixturePath, "missing-claude.exe")).PrepareAsync(c.LeftPath);
                    Check(false, "An unavailable executable must fail explicitly");
                }
                catch (IOException error) { Check(error.Message.Contains("Claude Code was not found", StringComparison.Ordinal), "Missing Claude has an actionable diagnostic"); }
            }
            finally { GitFixture.AllowCleanup(c.LeftPath); }
        }),
        new("git-commit-native-launch", "UC28; real Windows Shell launch preserves interactive Claude arguments and repository paths containing spaces, Unicode and shell characters", async c =>
        {
            var root = Path.Combine(c.LeftPath, "repo & 日本語");
            var nested = Path.Combine(root, "nested folder"); Directory.CreateDirectory(nested);
            await GitFixture.RunAsync(root, "init", "-b", "main");
            try
            {
                var fixture = CommandProbe.Prepare(c);
                File.WriteAllText(Path.Combine(fixture.Directory, "editor-probe.mode"), "");
                var launch = await new ClaudeCommitLauncher(fixture.Executable).PrepareAsync(char.ToLowerInvariant(nested[0]) + nested[1..]);
                await new WindowsExternalLauncher(showWindows: false).LaunchAsync(launch);
                var report = await CommandProbe.ReadReportAsync(fixture.Report);
                Equal(root, report.WorkingDirectory, "The native child starts at the literal repository root");
                Check(report.Arguments.SequenceEqual(new[] { "--model", "sonnet", "--effort", "medium", "Commit all changes." }), "The native child receives the intact prompt and interactive-mode arguments");
                Check(c.Window.IsEnabled && !c.Window.IsBusy, "Starting the independent process leaves the main window available");
            }
            finally { GitFixture.AllowCleanup(root); }
        })
    ];
}

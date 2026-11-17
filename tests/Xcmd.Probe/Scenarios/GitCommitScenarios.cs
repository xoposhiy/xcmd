// @tests SeeRepositoryState, DelegateCommit, ExternalLaunch
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class GitCommitScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
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

using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Scenarios;

public static class LauncherScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("launcher-latest-arguments-and-working-directory", "UC40; stable entry point", async c =>
        {
            var fixture = LauncherProbe.Prepare(c);
            LauncherProbe.AddEndpoint(fixture, OldId);
            LauncherProbe.AddEndpoint(fixture, NewId);
            LauncherProbe.SelectRelease(fixture, NewId);
            var reportPath = Path.Combine(fixture.Root, "arguments.json");
            string[] arguments = ["", "two words", "日本語", "embedded\"quote", "trailing\\", "& % PATH"];
            var timer = ProbeTimer.Start();
            Equal(0, await LauncherProbe.RunAsync(fixture, c.LeftPath, [reportPath, .. arguments]), "The launcher succeeds");
            c.Measurements["launcherExitMs"] = timer.Elapsed.TotalMilliseconds;
            var report = await CommandProbe.ReadReportAsync(reportPath);
            Equal(fixture.ReleaseExecutable(NewId), report.Executable, "The marker selects the latest verified executable");
            Equal(c.LeftPath, report.WorkingDirectory, "Launch preserves the caller's working directory");
            Check(arguments.SequenceEqual(report.Arguments), "Launch preserves exact argument boundaries and contents.");
        }),
        new("launcher-rejects-invalid-and-unverified-releases", "UC40; verified startup", async c =>
        {
            var fixture = LauncherProbe.Prepare(c);
            await LauncherProbe.CheckRejectedReleasesAsync(fixture);
        }),
        new("launcher-shortcut-native-shell", "UC40; taskbar shortcut", async c =>
        {
            var fixture = LauncherProbe.Prepare(c);
            LauncherProbe.AddEndpoint(fixture, NewId);
            LauncherProbe.SelectRelease(fixture, NewId);
            Equal(0, await LauncherProbe.RunAsync(fixture, c.LeftPath, "--create-shortcut", fixture.Shortcut), "The launcher creates its shortcut");
            TaskbarProbe.CheckShortcut(fixture);
            LauncherProbe.OpenShortcut(fixture);
            var report = await CommandProbe.ReadReportAsync(Path.Combine(fixture.Releases, NewId, "launch-report.json"));
            Equal(fixture.ReleaseExecutable(NewId), report.Executable, "The real Windows shortcut starts the verified release");
            Equal(fixture.Root, report.WorkingDirectory, "The shortcut uses its configured working directory");
        }),
        new("launcher-busy-file-keeps-release-publishing-available", "UC40; UC38; independent release files", async c =>
        {
            var fixture = LauncherProbe.Prepare(c);
            await LauncherProbe.CheckLockedInstallAsync(fixture);
            LauncherProbe.AddEndpoint(fixture, NewId);
            LauncherProbe.SelectRelease(fixture, NewId);
            var reportPath = Path.Combine(fixture.Root, "after-publish.json");
            Equal(0, await LauncherProbe.RunAsync(fixture, c.LeftPath, reportPath), "The existing launcher starts a new release after a deferred update");
            var report = await CommandProbe.ReadReportAsync(reportPath);
            Equal(fixture.ReleaseExecutable(NewId), report.Executable, "Publishing changes the launch target without replacing the launcher");
        }),
        new("launcher-window-taskbar-identity-and-relaunch", "UC40; stable taskbar identity", c =>
        {
            var fixture = LauncherProbe.Prepare(c);
            TaskbarProbe.CheckWindowIdentity(fixture.Launcher);
            c.Measurements["invisibleShellFixtureWindows"] = 1;
            return Task.CompletedTask;
        }),
        new("launcher-headless-application-survives-and-updates", "UC40; UC38; launcher and process handoff", async c =>
        {
            var fixture = LauncherProbe.Prepare(c);
            await LauncherProbe.CheckHeadlessHandoffAsync(c, fixture);
        })
    ];
}

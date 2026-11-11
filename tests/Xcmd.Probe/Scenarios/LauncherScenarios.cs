using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Scenarios;

public static class LauncherScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("launcher-report-publication-waits-for-unlock", "UC40; bounded report reading under real Windows sharing locks", LauncherProbe.CheckReportPublicationAsync),
        new("launcher-startup-restores-workspace", "UC40; UC38; startup measurement", async c =>
        {
            var fixture = LauncherProbe.Prepare(c);
            await LauncherProbe.CheckStartupAsync(c, fixture);
        }),
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
            await LauncherProbe.CheckWorkerShortcutAsync(fixture);
            LauncherProbe.OpenShortcut(fixture);
            var report = await CommandProbe.ReadReportAsync(Path.Combine(fixture.Releases, NewId, "launch-report.json"));
            Equal(fixture.ReleaseExecutable(NewId), report.Executable, "The real Windows shortcut starts the verified release");
            Equal(fixture.Root, report.WorkingDirectory, "The shortcut uses its configured working directory");
        }),
        new("launcher-shortcut-icon-refresh", "UC40; existing shortcut icon updates", async c =>
        {
            var fixture = LauncherProbe.Prepare(c);
            var iconSource = Path.Combine(fixture.Root, "source.ico");
            TaskbarProbe.WriteSolidIcon(iconSource, 230, 40, 70);
            await LauncherProbe.InstallAsync(fixture, icon: iconSource);
            var oldIcon = TaskbarProbe.ReadShortcutIcon(fixture);
            var oldBytes = await File.ReadAllBytesAsync(oldIcon);
            var oldPixels = TaskbarProbe.ReadIconPixels(fixture.Shortcut);
            TaskbarProbe.CheckShortcut(fixture, oldIcon);
            Check(oldPixels.Any(value => value != 0), "Windows returns the initial shortcut icon pixels.");

            TaskbarProbe.WriteSolidIcon(iconSource, 40, 210, 230);
            await LauncherProbe.InstallAsync(fixture, icon: iconSource);
            var newIcon = TaskbarProbe.ReadShortcutIcon(fixture);
            var newPixels = TaskbarProbe.ReadIconPixels(fixture.Shortcut);
            Check(!string.Equals(oldIcon, newIcon, StringComparison.OrdinalIgnoreCase), "Changed icon pixels use a new cache path.");
            var retainedBytes = await File.ReadAllBytesAsync(oldIcon);
            Check(oldBytes.SequenceEqual(retainedBytes), "The previous icon stays available for existing pins.");
            TaskbarProbe.CheckShortcut(fixture, newIcon);
            Check(newPixels.Any(value => value != 0), "Windows returns icon pixels from the updated shortcut.");
            Check(!oldPixels.SequenceEqual(newPixels), "The Windows shortcut icon changes after installation.");
            await LauncherProbe.InstallAsync(fixture, icon: iconSource);
            Equal(newIcon, TaskbarProbe.ReadShortcutIcon(fixture), "The same icon keeps its cache path");
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

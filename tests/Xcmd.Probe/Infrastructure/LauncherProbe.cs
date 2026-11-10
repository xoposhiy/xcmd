using System.Diagnostics;
using System.Text.Json;
using Xcmd.Core;
using Xcmd.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Infrastructure;

public sealed record LauncherFixture(string Root, string Launcher, string Shortcut)
{
    public string Releases => Path.Combine(Root, "releases");
    public string ReleaseExecutable(string id) => Path.Combine(Releases, id, "Xcmd.Desktop.exe");
}

public static class LauncherProbe
{
    public static LauncherFixture Prepare(ProbeContext context)
    {
        var root = Path.Combine(context.FixturePath, "installed Xcmd 日本語");
        Directory.CreateDirectory(Path.Combine(root, "releases"));
        var launcher = Path.Combine(root, "Xcmd.exe");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "launcher", "Xcmd.exe"), launcher);
        return new(root, launcher, Path.Combine(root, "Xcmd.lnk"));
    }

    public static void AddEndpoint(LauncherFixture fixture, string id)
    {
        var directory = Path.GetDirectoryName(fixture.ReleaseExecutable(id))!;
        Directory.CreateDirectory(directory);
        foreach (var source in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "launch-probe")))
            File.Copy(source, Path.Combine(directory, Path.GetFileName(source)));
        File.Move(Path.Combine(directory, "Xcmd.LaunchProbe.exe"), fixture.ReleaseExecutable(id));
        WriteReleaseEvidence(fixture, id);
    }

    private static void WriteReleaseEvidence(LauncherFixture fixture, string id)
    {
        var directory = Path.GetDirectoryName(fixture.ReleaseExecutable(id))!;
        File.WriteAllText(Path.Combine(directory, "release.json"), JsonSerializer.Serialize(new { id, restartProtocol = 1 }));
        File.WriteAllText(Path.Combine(directory, "verification.json"), "{\"total\":1,\"failures\":0,\"nativeApplicationWindows\":0}");
    }

    public static void SelectRelease(LauncherFixture fixture, string id)
    {
        var marker = Path.Combine(fixture.Releases, "latest.txt");
        var temporary = marker + ".tmp";
        File.WriteAllText(temporary, id);
        File.Move(temporary, marker, overwrite: true);
    }

    public static async Task<int> RunAsync(LauncherFixture fixture, string workingDirectory, params string[] args)
    {
        var start = new ProcessStartInfo(fixture.Launcher) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = workingDirectory };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("The launcher fixture did not start.");
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        return process.ExitCode;
    }

    public static async Task InstallAsync(LauncherFixture fixture, string? source = null)
    {
        var project = new DirectoryInfo(AppContext.BaseDirectory);
        while (project != null && !File.Exists(Path.Combine(project.FullName, "scripts", "Install-Launcher.ps1"))) project = project.Parent;
        var script = Path.Combine(project?.FullName ?? throw new IOException("The install script was not found."), "scripts", "Install-Launcher.ps1");
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-NoProfile", "-File", script, "-Destination", fixture.Root, "-SourcePath", source ?? fixture.Launcher }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("The installer fixture did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        Equal(0, process.ExitCode, "The launcher install succeeds: " + await output + await error);
    }

    public static async Task CheckRejectedReleasesAsync(LauncherFixture fixture)
    {
        var errors = new List<string>();
        async Task RejectAsync()
        {
            Equal(1, await BootstrapProgram.RunAsync(fixture.Root, [], errors.Add), "An invalid release is rejected");
            Check(errors[^1].StartsWith("Xcmd could not start.", StringComparison.Ordinal), "Startup failures have a clear message.");
        }
        await RejectAsync();
        foreach (var id in new[] { "../outside", "20261109-000001-000-00000002/../outside", NewId })
        {
            SelectRelease(fixture, id);
            await RejectAsync();
        }
        AddEndpoint(fixture, NewId);
        var directory = Path.GetDirectoryName(fixture.ReleaseExecutable(NewId))!;
        foreach (var verification in new[] { "{}", "{\"total\":1,\"failures\":1}", "{\"total\":0,\"failures\":0}", "invalid JSON" })
        {
            File.WriteAllText(Path.Combine(directory, "verification.json"), verification);
            await RejectAsync();
        }
        WriteReleaseEvidence(fixture, NewId);
        File.WriteAllText(Path.Combine(directory, "release.json"), "{\"id\":\"wrong\",\"restartProtocol\":1}");
        await RejectAsync();
        File.WriteAllText(Path.Combine(directory, "release.json"), JsonSerializer.Serialize(new { id = NewId, restartProtocol = 2 }));
        await RejectAsync();
        Check(!File.Exists(Path.Combine(directory, "launch-report.json")), "Rejected releases never execute.");
        Equal(10, errors.Count, "Every rejected launch reports one error");
    }

    public static async Task CheckLockedInstallAsync(LauncherFixture fixture)
    {
        await InstallAsync(fixture);
        var original = await File.ReadAllBytesAsync(fixture.Launcher);
        var replacement = Path.Combine(fixture.Root, "replacement.exe");
        await File.WriteAllBytesAsync(replacement, original.Concat(new byte[] { 0 }).ToArray());
        using (var locked = new FileStream(fixture.Launcher, FileMode.Open, FileAccess.Read, FileShare.Read))
            await InstallAsync(fixture, replacement);
        var current = await File.ReadAllBytesAsync(fixture.Launcher);
        Check(original.SequenceEqual(current), "A busy launcher stays intact and does not block publishing.");
        Check(!Directory.EnumerateFiles(fixture.Root, "*.tmp").Any(), "A deferred launcher update leaves no temporary files.");
    }

    public static void OpenShortcut(LauncherFixture fixture)
    {
        using var process = Process.Start(new ProcessStartInfo(fixture.Shortcut) { UseShellExecute = true });
    }

    public static async Task CheckWorkerShortcutAsync(LauncherFixture fixture)
    {
        var worker = fixture with { Shortcut = Path.Combine(fixture.Root, "worker.lnk") };
        await Task.Run(() => WindowsShortcut.Create(worker.Shortcut, worker.Launcher));
        TaskbarProbe.CheckShortcut(worker);
    }

    public static async Task CheckStartupAsync(ProbeContext context, LauncherFixture fixture)
    {
        var executable = AddRelease(fixture.Releases, NewId, copyApplication: true);
        File.Copy(executable, fixture.ReleaseExecutable(NewId), overwrite: true);
        SelectRelease(fixture, NewId);
        var session = Path.Combine(fixture.Root, "session.json");
        var expected = context.Window.CaptureWorkspace();
        await new SessionStore(session).SaveAsync(expected);
        var reportPath = Path.Combine(fixture.Root, "startup.json");
        var timer = ProbeTimer.Start();
        try
        {
            Equal(0, await RunAsync(fixture, fixture.Root, "--startup-host", reportPath, session), "The startup launcher succeeds");
            await WaitAsync(() => File.Exists(reportPath));
            using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
            var state = report.RootElement.GetProperty("state").Deserialize<WorkspaceState>()!;
            Equal(expected.Left.DirectoryPath, state.Left.DirectoryPath, "Startup restores the left directory");
            Equal(expected.Right.DirectoryPath, state.Right.DirectoryPath, "Startup restores the right directory");
            Equal(expected.ActivePanel, state.ActivePanel, "Startup restores the active panel");
            Equal(0, report.RootElement.GetProperty("visibleApplicationWindows").GetInt32(), "The startup probe opens no visible windows");
            Check(File.Exists(reportPath + ".png"), "Startup retains the offscreen frame.");
            context.Measurements["startupProbeCompletedMs"] = timer.Elapsed.TotalMilliseconds;
        }
        finally { await CloseStartupHostAsync(reportPath); }
    }

    private static async Task CloseStartupHostAsync(string report)
    {
        if (!File.Exists(report + ".pid")) return;
        Process process;
        try { process = Process.GetProcessById(int.Parse(File.ReadAllText(report + ".pid"), System.Globalization.CultureInfo.InvariantCulture)); }
        catch (ArgumentException) { return; }
        using var host = new RunningApplication(process);
        try { await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Equal(0, host.ExitCode, "The startup host exits cleanly"); }
        finally { await host.StopAsync(); }
    }

    public static async Task CheckHeadlessHandoffAsync(ProbeContext context, LauncherFixture fixture)
    {
        var reports = Path.Combine(fixture.Root, "reports");
        var session = Path.Combine(fixture.Root, "session.json");
        await new SessionStore(session).SaveAsync(context.Window.CaptureWorkspace());
        var original = AddRelease(fixture.Releases, OldId, copyApplication: true);
        File.Copy(original, fixture.ReleaseExecutable(OldId), overwrite: true);
        SelectRelease(fixture, OldId);
        var start = new ProcessStartInfo(fixture.Launcher) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--lifecycle-host", "--session", session }) start.ArgumentList.Add(argument);
        start.Environment["XCMD_TEST_RELEASES"] = fixture.Releases;
        start.Environment["XCMD_TEST_REPORTS"] = reports;
        using var launcher = Process.Start(start) ?? throw new IOException("The headless launcher did not start.");
        await launcher.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Equal(0, launcher.ExitCode, "The launcher exits while the application keeps running");
        await WaitAsync(() => File.Exists(Path.Combine(reports, OldId + ".json")));
        using var sourceReport = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reports, OldId + ".json")));
        using var source = Process.GetProcessById(sourceReport.RootElement.GetProperty("processId").GetInt32());
        try
        {
            Check(!source.HasExited, "The application outlives its launcher.");
            AddRelease(fixture.Releases, NewId, copyApplication: true);
            SelectRelease(fixture, NewId);
            await source.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));
            await WaitAsync(() => File.Exists(Path.Combine(reports, NewId + ".json")));
            using var replacement = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reports, NewId + ".json")));
            Check(replacement.RootElement.GetProperty("parentExited").GetBoolean(), "The replacement waits for the source application.");
            Equal(0, replacement.RootElement.GetProperty("nativeApplicationWindows").GetInt32(), "The replacement stays headless");
            var state = replacement.RootElement.GetProperty("state").Deserialize<WorkspaceState>()!;
            Equal(context.LeftPath, state.Left.DirectoryPath, "The launcher and update keep the saved workspace");
        }
        finally { if (!source.HasExited) { source.Kill(entireProcessTree: true); await source.WaitForExitAsync(); } }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.CommandProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Scenarios;

public static class CommandScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("command-file-enter-and-double-click", "File opening; Windows default associations", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Enter);
            var request = c.Launcher.Requests.Single(); Equal(ExternalLaunchKind.File, request.Kind, "Enter requests Windows file opening");
            Equal(Path.Combine(c.LeftPath, "alpha.txt"), request.Target, "The file path is literal"); Equal(c.LeftPath, request.WorkingDirectory, "Opening uses the source directory");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Opening keeps the file cursor");
            await c.ClickAsync(c.Row("file2.log")); await c.ClickAsync(c.Row("file2.log"));
            Equal("file2.log", Path.GetFileName(c.Launcher.Requests.Last().Target), "Double-click requests the pointed file");
        }),
        new("command-file-held-enter-once", "File opening; repeated keys", async c =>
        {
            await c.SelectAsync("alpha.txt");
            c.Input.KeyDown(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            c.Input.KeyDown(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); await c.SettleAsync();
            Equal(1, c.Launcher.Requests.Count, "Holding Enter starts one application");
            c.Input.KeyUp(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await c.KeyAsync(Key.Enter); Equal(2, c.Launcher.Requests.Count, "A later Enter can open the file again");
        }),
        new("command-native-executable-opening", "File opening; real Windows shell execution", async c =>
        {
            var fixture = Prepare(c); await c.Window.Left.LoadAsync(fixture.Directory); await c.SelectAsync(Path.GetFileName(fixture.Executable));
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false); await c.KeyAsync(Key.Enter);
            var report = await ReadReportAsync(fixture.Report); Equal(fixture.Directory, report.WorkingDirectory, "Enter starts the real executable in its directory");
            Equal(0, report.Arguments.Length, "The literal file path is not parsed as command text");
        }),
        new("command-native-shortcut-association", "File opening; real Windows association", async c =>
        {
            var fixture = Prepare(c); var shortcut = Path.Combine(c.LeftPath, "shortcut & 日本語.lnk"); CreateShortcut(shortcut, fixture, c.RightPath);
            await c.KeyAsync(Key.R, RawInputModifiers.Control); await c.SelectAsync(Path.GetFileName(shortcut));
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false); await c.KeyAsync(Key.Enter);
            var report = await ReadReportAsync(fixture.Report); Equal(c.RightPath, report.WorkingDirectory, "Windows resolves the shortcut and its configured directory");
        })
    ];
}

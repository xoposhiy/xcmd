// @tests RestoreWorkspace, ApplicationLifecycle, ReleaseHandoff
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xcmd.Desktop;

namespace Xcmd.Probe.Infrastructure;

public static class WorkspaceProbe
{
    public const string OldId = "20261109-000000-000-00000001";
    public const string NewId = "20261109-000001-000-00000002";

    public static Button Button(CommanderWindow window, string name) => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    public static async Task WaitAsync(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("The workspace action did not complete.");
            await Task.Delay(20);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }

    public static async Task<JsonDocument> ReadWorkspaceReportAsync(string path)
    {
        JsonDocument? report = null;
        await WaitAsync(() =>
        {
            try
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                report = JsonDocument.Parse(input);
                return true;
            }
            catch (IOException error) when (error is FileNotFoundException or DirectoryNotFoundException || (error.HResult & 0xFFFF) is 32 or 33)
            { return false; }
        });
        return report!;
    }

    public static string AddRelease(string root, string id, bool copyApplication)
    {
        var directory = Path.Combine(root, id);
        Directory.CreateDirectory(directory);
        if (copyApplication)
        {
            foreach (var source in Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(directory, Path.GetRelativePath(AppContext.BaseDirectory, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }
        }
        else File.WriteAllText(Path.Combine(directory, "Xcmd.Probe.exe"), "Invalid executable fixture");
        File.WriteAllText(Path.Combine(directory, "release.json"), JsonSerializer.Serialize(new { id, restartProtocol = 1 }));
        File.WriteAllText(Path.Combine(directory, "verification.json"), "{\"total\":1,\"failures\":0}");
        return Path.Combine(directory, "Xcmd.Probe.exe");
    }

    public static RunningApplication StartHost(string executable, string session, string releases, string reports, string? failRelease = null)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
        start.ArgumentList.Add("--lifecycle-host"); start.ArgumentList.Add("--session"); start.ArgumentList.Add(session);
        start.Environment["XCMD_TEST_RELEASES"] = releases;
        start.Environment["XCMD_TEST_REPORTS"] = reports;
        if (failRelease != null) start.Environment["XCMD_TEST_FAIL_RELEASE"] = failRelease;
        return new(Process.Start(start) ?? throw new InvalidOperationException("The headless lifecycle host did not start."));
    }
}

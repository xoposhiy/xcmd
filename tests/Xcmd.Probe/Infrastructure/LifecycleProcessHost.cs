using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xcmd.Desktop;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public static class LifecycleProcessHost
{
    [STAThread]
    public static int Run(string[] args)
    {
        var settings = LaunchSettings.Parse(args);
        var root = Environment.GetEnvironmentVariable("XCMD_TEST_RELEASES")!;
        var reports = Environment.GetEnvironmentVariable("XCMD_TEST_REPORTS")!;
        var id = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var builder = AppBuilder.Configure(() => new App
        {
            LifecycleFactory = (window, launch) =>
            {
                var lifecycle = new WorkspaceLifecycle(window, new SessionStore(launch.SessionPath),
                    new ReleaseCatalog(root, id, "Xcmd.Probe.exe"), new RestartProcess("--lifecycle-host"));
                if (id == Environment.GetEnvironmentVariable("XCMD_TEST_FAIL_RELEASE"))
                    lifecycle.Initialized += () => throw new IOException("Controlled startup failure before handoff readiness.");
                var failureWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                failureWatch.Tick += async (_, _) =>
                {
                    if (lifecycle.UpdateError == null) return;
                    failureWatch.Stop();
                    var report = new { window.IsEnabled, state = window.CaptureWorkspace(), lifecycle.UpdateError };
                    await WriteReportAsync(Path.Combine(reports, id + "-failed-update.json"), report);
                    lifecycle.Dispose(); window.Close();
                };
                window.Closed += (_, _) => failureWatch.Stop();
                window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await Task.Delay(100);
                        window.RestoreView();
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        var handle = window.TryGetPlatformHandle();
                        ProbeContext.Check(handle?.Handle == IntPtr.Zero && handle.HandleDescriptor == "STUB", "The lifecycle host creates no native application window.");
                        Directory.CreateDirectory(reports);
                        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The restored application did not render.");
                        frame.Save(Path.Combine(reports, id + ".png"), PngBitmapEncoderOptions.Default);
                        var parentExited = true;
                        if (settings.WaitForProcess is { } previous)
                        {
                            try { using var process = Process.GetProcessById(previous); parentExited = process.HasExited; }
                            catch (ArgumentException) { }
                        }
                        var report = new { processId = Environment.ProcessId, parentExited, nativeApplicationWindows = 0, state = window.CaptureWorkspace() };
                        await WriteReportAsync(Path.Combine(reports, id + ".json"), report);
                        if (settings.WaitForProcess != null) { lifecycle.Dispose(); window.Close(); }
                        else failureWatch.Start();
                    }
                    catch (Exception error)
                    {
                        await File.WriteAllTextAsync(Path.Combine(reports, "host-error.txt"), error.ToString());
                        lifecycle.Dispose(); window.Close();
                    }
                });
                return lifecycle;
            }
        }).UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        return builder.StartWithClassicDesktopLifetime(args);
    }

    private static async Task WriteReportAsync(string path, object report)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report));
        File.Move(temporary, path, overwrite: true);
    }
}

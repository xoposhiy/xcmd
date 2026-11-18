// @tests RestoreWorkspace, ApplicationLifecycle, HeadlessVerification, IntegrationVerification
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xcmd.Desktop;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public static class StartupProcessHost
{
    [STAThread]
    public static int Run(string[] args)
    {
        var consoleAttached = ConsoleProbe.IsAttached;
        using var process = Process.GetCurrentProcess();
        var runtimeToMain = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalMilliseconds;
        var watch = Stopwatch.StartNew();
        var phases = new Dictionary<string, double> { ["runtimeToMainMs"] = runtimeToMain };
        File.WriteAllText(args[0] + ".pid", process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        void Mark(string phase) => phases[phase] = runtimeToMain + watch.Elapsed.TotalMilliseconds;
        var native = args.Contains("--native");
        WindowsTaskbar.ConfigureProcess();
        WorkspaceLifecycle? lifecycle = null;
        var builder = AppBuilder.Configure(() => new App
        {
            LifecycleFactory = (window, launch) =>
            {
                Mark("windowCreatedMs");
                lifecycle = new WorkspaceLifecycle(window, new SessionStore(launch.SessionPath), ReleaseCatalog.ForApplication());
                lifecycle.Initialized += () => Mark("workspaceReadyMs");
                return lifecycle;
            },
            WindowPresenter = window =>
            {
                Mark("showRequestedMs");
                var readyToShowTimestamp = Stopwatch.GetTimestamp();
                if (!native) window.Show();
                Dispatcher.UIThread.Post(async () =>
                {
                    var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                    try
                    {
                        long? frameReadyTimestamp = null;
                        if (native && window.IsVisible) throw new InvalidOperationException("The native startup probe must keep its window hidden.");
                        if (!native)
                        {
                            var handle = window.TryGetPlatformHandle();
                            ProbeContext.Check(handle?.Handle == 0 && handle.HandleDescriptor == "STUB", "The startup host uses a headless application window.");
                            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                            using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Startup did not render a frame.");
                            Mark("frameReadyMs");
                            frameReadyTimestamp = Stopwatch.GetTimestamp();
                            frame.Save(args[0] + ".png", PngBitmapEncoderOptions.Default);
                        }
                        var report = new { native, consoleAttached, visibleApplicationWindows = 0, readyToShowTimestamp, frameReadyTimestamp, phases, state = window.CaptureWorkspace() };
                        await File.WriteAllTextAsync(args[0] + ".tmp", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                        File.Move(args[0] + ".tmp", args[0]);
                        if (args.Contains("--wait-for-exit-signal"))
                            await WorkspaceProbe.WaitAsync(() => File.Exists(args[0] + ".exit"));
                        lifecycle!.Dispose();
                        desktop.Shutdown();
                    }
                    catch (Exception error)
                    {
                        await File.WriteAllTextAsync(args[0], error.ToString());
                        lifecycle?.Dispose();
                        desktop.Shutdown(1);
                    }
                });
            }
        });
        if (native) builder.UseDesktopPlatform();
        else builder.UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        builder.AfterSetup(_ => Mark("platformReadyMs"));
        return builder.StartWithClassicDesktopLifetime(["--session", args[1]]);
    }
}

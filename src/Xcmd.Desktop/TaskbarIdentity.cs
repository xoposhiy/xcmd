using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal static class TaskbarIdentity
{
    private static string? processError;
    public static void ConfigureProcess() => processError = WindowsTaskbar.ConfigureProcess();

    public static void Configure(CommanderWindow window)
    {
        if (processError != null) window.Trace.Add("Taskbar process identity: " + processError);
        var release = ReleaseCatalog.ForApplication();
        var handle = window.TryGetPlatformHandle();
        if (release == null || handle?.HandleDescriptor != "HWND" || handle.Handle == 0) return;
        var error = WindowsTaskbar.ConfigureWindow(handle.Handle, release.LauncherExecutable);
        if (error != null) window.Trace.Add("Taskbar window identity: " + error);
        var cleared = false;
        window.Closing += (_, e) =>
        {
            if (e.Cancel || cleared) return;
            var cleanupError = WindowsTaskbar.ClearWindow(handle.Handle);
            if (cleanupError != null) window.Trace.Add("Taskbar identity cleanup: " + cleanupError);
            cleared = true;
        };
    }
}

// @implements StableShortcut
using Avalonia.Controls;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Shared.Platform;

internal static class TaskbarIdentity
{
    private static string? processError;
    public static void ConfigureProcess() => processError = WindowsTaskbar.ConfigureProcess();

    public static void Configure(Window window, Action<string> report)
    {
        if (processError != null) report("Taskbar process identity: " + processError);
        var release = ReleaseCatalog.ForApplication();
        var handle = window.TryGetPlatformHandle();
        if (release == null || handle?.HandleDescriptor != "HWND" || handle.Handle == 0) return;
        var error = WindowsTaskbar.ConfigureWindow(handle.Handle, release.LauncherExecutable, release.ApplicationIcon);
        if (error != null) report("Taskbar window identity: " + error);
        var cleared = false;
        window.Closing += (_, e) =>
        {
            if (e.Cancel || cleared) return;
            var cleanupError = WindowsTaskbar.ClearWindow(handle.Handle);
            if (cleanupError != null) report("Taskbar identity cleanup: " + cleanupError);
            cleared = true;
        };
    }
}

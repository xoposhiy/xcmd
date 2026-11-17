// @implements WindowsIdentity
using System.Runtime.InteropServices;

namespace Xcmd.Infrastructure;

public static class WindowsTaskbar
{
    public static string? ConfigureProcess() => TryConfigure(() =>
    {
        if (OperatingSystem.IsWindows()) Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(WindowsShortcut.ApplicationId));
    });

    public static string? ConfigureWindow(nint window, string launcher, string? icon = null) => TryConfigure(() =>
    {
        using var properties = ShellProperties.ForWindow(window);
        properties.Set(2, '"' + Path.GetFullPath(launcher) + '"');
        properties.Set(4, "Xcmd");
        properties.Set(3, (icon ?? launcher) + ",0");
        properties.Set(5, WindowsShortcut.ApplicationId);
    });

    public static string? ClearWindow(nint window) => TryConfigure(() =>
    {
        using var properties = ShellProperties.ForWindow(window);
        foreach (var id in new uint[] { 5, 2, 4, 3 }) properties.Set(id, null);
    });

    private static string? TryConfigure(Action configure)
    {
        try { configure(); return null; }
        catch (Exception error) { return error.ToString(); }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
}

using System.Runtime.InteropServices;

namespace Xcmd.Probe.Infrastructure;

internal static class ConsoleProbe
{
    public static bool IsAttached => OperatingSystem.IsWindows() && GetConsoleWindow() != 0;

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint GetConsoleWindow();
}

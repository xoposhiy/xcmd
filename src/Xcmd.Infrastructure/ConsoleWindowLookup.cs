using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Xcmd.Infrastructure;

public sealed record ProcessWindow(long Handle, int ProcessId, string Title)
{
    public bool IsApplicationWindow { get; init; } = true;
}

public static class ConsoleWindowLookup
{
    public static async Task<ProcessWindow?> FindAsync(int processId, CancellationToken token = default)
    {
        var report = Path.Combine(Path.GetTempPath(), "xcmd-console-" + Guid.NewGuid().ToString("N") + ".json");
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Xcmd.Desktop.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var value in new[] { "--console-window", processId.ToString(CultureInfo.InvariantCulture), report }) start.ArgumentList.Add(value);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            using var helper = Process.Start(start) ?? throw new IOException("The console lookup could not start.");
            try { await helper.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            finally { if (!helper.HasExited) { helper.Kill(); await helper.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } }
            if (helper.ExitCode != 0 || !File.Exists(report)) return null;
            return JsonSerializer.Deserialize<ProcessWindow>(await File.ReadAllTextAsync(report, token).ConfigureAwait(false));
        }
        finally { File.Delete(report); }
    }

    public static int RunHelper(string processId, string report)
    {
        if (!uint.TryParse(processId, out var id)) return 1;
        // Console attachment changes process-wide state, so only this short-lived
        // helper attaches. The file manager keeps its own console and standard handles.
        if (!AttachConsole(id)) return 0;
        try
        {
            var console = GetConsoleWindow();
            var window = GetAncestor(console, 3);
            if (window == 0) window = console;
            if (window == 0) return 0;
            GetWindowThreadProcessId(window, out var owner);
            var title = new StringBuilder(1024);
            GetWindowText(window, title, title.Capacity);
            File.WriteAllText(report, JsonSerializer.Serialize(new ProcessWindow(window, (int)owner, title.ToString())
            { IsApplicationWindow = window != console || IsWindowVisible(window) }));
            return 0;
        }
        finally { FreeConsole(); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool FreeConsole();
    [DllImport("kernel32.dll")] private static extern nint GetConsoleWindow();
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
}

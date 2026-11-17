// @implements WindowsFileActions
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class WindowsFileUserActions : IFileUserActions
{
    public Task ShowWindowAsync(FileUser user) => Task.Run(async () =>
    {
        using var identity = FileUserProcess.Open(user) ?? throw new IOException("This process has already exited.");
        try
        {
            using var process = Process.GetProcessById(user.ProcessId);
            var window = await ApplicationWindowAsync(process).ConfigureAwait(false);
            if (identity.HasExited) throw new IOException("This process has already exited.");
            if (IsIconic(window)) ShowWindowAsync(window, 9);
            if (!SetForegroundWindow(window)) throw new IOException("Windows could not bring this application's window to the foreground.");
        }
        catch (ArgumentException error) { throw new IOException("This process has already exited.", error); }
    });

    public Task EndProcessAsync(FileUser user, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (user.ProcessId == Environment.ProcessId) throw new IOException("Xcmd cannot end its own process here.");
        using var process = FileUserProcess.Open(user, allowEnd: true);
        cancellationToken.ThrowIfCancellationRequested();
        process?.End();
    }, cancellationToken);

    public async Task WaitForExitAsync(FileUser user, CancellationToken cancellationToken)
    {
        using var process = FileUserProcess.Open(user);
        if (process != null) await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task<nint> ApplicationWindowAsync(Process process)
    {
        var window = process.MainWindowHandle;
        if (window == 0)
        {
            var console = await ConsoleWindowLookup.FindAsync(process.Id).ConfigureAwait(false);
            if (console is { IsApplicationWindow: true })
            {
                window = (nint)console.Handle;
                GetWindowThreadProcessId(window, out var host);
                if (!process.HasExited && host == console.ProcessId) return window;
            }
        }
        GetWindowThreadProcessId(window, out var owner);
        if (window == 0 || owner != process.Id) throw new IOException("This process has no application window.");
        return window;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(nint window, int command);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class FileUserProcess(SafeProcessHandle handle) : IDisposable
{
    public static FileUserProcess? Open(FileUser user, bool allowEnd = false)
    {
        var handle = OpenProcess(0x00101000u | (allowEnd ? 1u : 0u), false, user.ProcessId);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error == 87) return null;
            throw new IOException("Windows could not access this process. " + new Win32Exception(error).Message);
        }
        var process = new FileUserProcess(handle);
        try
        {
            if (!GetProcessTimes(handle, out var created, out _, out _, out _)) throw Failure();
            if (DateTime.FromFileTimeUtc(created) == user.StartedAtUtc && !process.HasExited) return process;
            process.Dispose();
            return null;
        }
        catch { process.Dispose(); throw; }
    }

    public bool HasExited => WaitForSingleObject(handle, 0) switch
    {
        0 => true,
        258 => false,
        _ => throw Failure()
    };

    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var signal = new ProcessSignal(handle);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => completion.TrySetResult(), null, Timeout.Infinite, executeOnlyOnce: true);
        using var cancelled = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        try { await completion.Task.ConfigureAwait(false); }
        finally { registration.Unregister(null); }
    }

    private sealed class ProcessSignal : WaitHandle
    {
        public ProcessSignal(SafeProcessHandle process)
        {
            // The wait owns a duplicate so cancellation cannot close a handle still used by its callback.
            if (!DuplicateHandle(-1, process, -1, out var signal, 0, false, 2)) throw Failure();
            SafeWaitHandle = signal;
        }
    }

    public void End()
    {
        if (HasExited) return;
        if (IsCritical()) throw new IOException("A critical Windows process cannot be ended here.");
        // The handle and creation time identify one process even if Windows reuses its PID.
        if (!TerminateProcess(handle, 1))
        {
            var error = Marshal.GetLastWin32Error();
            if (!HasExited) throw new IOException("Windows could not end this process. " + new Win32Exception(error).Message);
        }
    }

    private bool IsCritical()
    {
        if (!IsProcessCritical(handle, out var critical)) throw Failure();
        return critical;
    }

    public void Dispose() => handle.Dispose();
    private static IOException Failure() => new("Windows could not inspect this process. " + new Win32Exception().Message);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(nint sourceProcess, SafeProcessHandle source, nint targetProcess, out SafeWaitHandle target, uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessCritical(SafeProcessHandle process, [MarshalAs(UnmanagedType.Bool)] out bool critical);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);
}

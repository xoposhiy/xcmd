// @implements WindowsFileActions
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class WindowsFileUsers
{
    public static async Task<FileUser[]> FindAsync(string[] paths, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var token = timeout.Token;
        try
        {
            var users = await Task.Run(() => Find(paths, token), token).WaitAsync(token).ConfigureAwait(false);
            return await Task.WhenAll(users.Select(DescribeWindowAsync)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return []; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or NotSupportedException) { return []; }
    }

    private static FileUser[] Find(string[] paths, CancellationToken token)
    {
        var found = new Dictionary<int, FileUser>();
        var pending = new Queue<string>(paths.Distinct(StringComparer.OrdinalIgnoreCase));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var started = Stopwatch.GetTimestamp();
        while (pending.TryDequeue(out var path) && visited.Count < 2048 && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(1))
        {
            token.ThrowIfCancellationRequested();
            if (!visited.Add(path)) continue;
            foreach (var pid in ReadProcessIds(path))
            {
                if (pid == Environment.ProcessId || found.ContainsKey(pid)) continue;
                try
                {
                    using var process = Process.GetProcessById(pid);
                    found[pid] = new(pid, DisplayName(process), path, process.StartTime.ToUniversalTime()) { ExecutableName = process.ProcessName + ".exe" };
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception) { }
            }
            if (found.Count > 0) continue;
            AddChildren(path, pending, 2048 - visited.Count);
        }
        return found.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.ProcessId).ToArray();
    }

    private static async Task<FileUser> DescribeWindowAsync(FileUser user)
    {
        try
        {
            using var process = Process.GetProcessById(user.ProcessId);
            var window = process.MainWindowHandle != 0
                ? new ProcessWindow(process.MainWindowHandle, process.Id, process.MainWindowTitle)
                : await ConsoleWindowLookup.FindAsync(user.ProcessId).ConfigureAwait(false);
            if (window == null || !window.IsApplicationWindow || process.StartTime.ToUniversalTime() != user.StartedAtUtc) return user;
            using var host = Process.GetProcessById(window.ProcessId);
            return user with { ApplicationName = DisplayName(host), WindowTitle = window.Title, WindowKey = window.Handle + ":" + host.StartTime.ToUniversalTime().Ticks };
        }
        catch (Exception error) when (error is IOException or Win32Exception or ArgumentException or InvalidOperationException or OperationCanceledException) { return user; }
    }

    private static string DisplayName(Process process)
    {
        try { return process.MainModule?.FileVersionInfo.FileDescription is { Length: > 0 } name ? name : process.ProcessName; }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException) { return process.ProcessName; }
    }

    private static void AddChildren(string path, Queue<string> pending, int remaining)
    {
        try
        {
            if (!Directory.Exists(path) || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) return;
            foreach (var child in Directory.EnumerateFileSystemEntries(path).Take(Math.Max(0, remaining - pending.Count))) pending.Enqueue(child);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private static int[] ReadProcessIds(string path)
    {
        using var handle = CreateFile(path, 0x80, 7, 0, 3, 0x02000000, 0);
        if (handle.IsInvalid) return [];
        var buffer = new byte[32768];
        // This native information class is reserved by Windows; unsupported filesystems
        // return no result, so diagnosis must never replace the original operation error.
        var status = NtQueryInformationFile(handle, out _, buffer, (uint)buffer.Length, 47);
        if (status != 0) return [];
        var count = Math.Min(BitConverter.ToUInt32(buffer), (uint)(buffer.Length / IntPtr.Size - 1));
        var result = new List<int>();
        for (var index = 0; index < count; index++)
        {
            var pid = BitConverter.ToUInt64(buffer, (index + 1) * IntPtr.Size);
            if (pid > 0 && pid <= int.MaxValue) result.Add((int)pid);
        }
        return result.ToArray();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatus { public nint Status; public nuint Information; }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtQueryInformationFile(SafeFileHandle handle, out IoStatus status, [Out] byte[] information, uint length, int informationClass);
}

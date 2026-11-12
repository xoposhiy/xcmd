using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class FileCopyMetadata
{
    private delegate uint CopyProgress(long total, long transferred, long streamSize, long streamTransferred,
        uint stream, uint reason, nint source, nint destination, nint data);

    public static long Copy(string source, string temporary, IProgress<long> progress, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("File copying requires Windows.");
        long reported = 0;
        CopyProgress callback = (total, transferred, _, _, _, _, _, _, _) =>
        {
            if (token.IsCancellationRequested) return 1;
            if (reported == 0 || Stopwatch.GetElapsedTime(reported).TotalMilliseconds >= 80 || transferred == total)
            { progress.Report(transferred); reported = Stopwatch.GetTimestamp(); }
            return 0;
        };
        if (!CopyFileExW(source, temporary, callback, 0, 0, 1))
        {
            var error = Marshal.GetLastWin32Error();
            token.ThrowIfCancellationRequested();
            throw new IOException("Could not copy file and metadata: " + new Win32Exception(error).Message);
        }
        token.ThrowIfCancellationRequested();
        if (!Streams(source).SequenceEqual(Streams(temporary)))
            throw new IOException("The destination cannot preserve the file's additional data streams. The existing destination was kept.");
        // New copies inherit destination permissions; do not import explicit source ACL entries.
        var security = new FileSecurity();
        security.SetAccessRuleProtection(false, false);
        new FileInfo(temporary).SetAccessControl(security);
        return new FileInfo(temporary).Length;
    }

    public static void Apply(string source, string destination)
    {
        var directory = File.GetAttributes(source).HasFlag(FileAttributes.Directory);
        FileSystemInfo original = directory ? new DirectoryInfo(source) : new FileInfo(source);
        FileSystemInfo copy = directory ? new DirectoryInfo(destination) : new FileInfo(destination);
        copy.CreationTimeUtc = original.CreationTimeUtc;
        copy.LastWriteTimeUtc = original.LastWriteTimeUtc;
        copy.LastAccessTimeUtc = original.LastAccessTimeUtc;
        const FileAttributes copied = FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Archive;
        copy.Attributes = (copy.Attributes & ~copied) | (original.Attributes & copied);
    }

    public static string[] Streams(string path)
    {
        var handle = FindFirstStreamW(path, 0, out var data, 0);
        if (handle == -1)
        {
            var error = Marshal.GetLastWin32Error();
            if (error is 38 or 87 or 1 or 50) return [];
            throw new IOException("Could not inspect additional data streams: " + new Win32Exception(error).Message);
        }
        try
        {
            var streams = new List<string>();
            do { if (data.Name != "::$DATA") streams.Add(data.Name + "|" + data.Size); }
            while (FindNextStreamW(handle, out data));
            var error = Marshal.GetLastWin32Error();
            if (error != 38) throw new IOException("Could not inspect additional data streams: " + new Win32Exception(error).Message);
            return streams.Order(StringComparer.Ordinal).ToArray();
        }
        finally { FindClose(handle); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StreamData
    {
        public long Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CopyFileExW(string source, string destination, CopyProgress progress, nint data, nint cancel, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindFirstStreamW(string path, int level, out StreamData data, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(nint handle, out StreamData data);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(nint handle);
}

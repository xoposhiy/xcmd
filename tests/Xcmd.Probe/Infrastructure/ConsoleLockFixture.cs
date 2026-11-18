// @tests WindowsFileActions, IntegrationVerification, RecoverOperations
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Xcmd.Probe.Infrastructure;

public sealed class ConsoleLockFixture : IAsyncDisposable
{
    private nint console;
    private SafeFileHandle? input;
    private FileStream? output;
    private Task? drain;
    private Process? process;
    private const string ReadyFile = @"..\xcmd-console-ready.txt";

    public static async Task<ConsoleLockFixture> StartAsync(string directory)
    {
        var fixture = new ConsoleLockFixture();
        try
        {
            fixture.StartConsole(directory);
            var ready = new FileInfo(Path.Combine(directory, ReadyFile));
            await FileOperationProbe.WaitUntilAsync(() => { ready.Refresh(); return ready.Exists && ready.Length > 0; });
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private void StartConsole(string directory)
    {
        if (!CreatePipe(out var inputRead, out var inputWrite, 0, 0)) throw new Win32Exception();
        input = inputWrite;
        using (inputRead)
        {
            if (!CreatePipe(out var outputRead, out var outputWrite, 0, 0)) throw new Win32Exception();
            output = new FileStream(outputRead, FileAccess.Read);
            drain = Task.Run(async () => { try { await output.CopyToAsync(Stream.Null); } catch (IOException) { } });
            using (outputWrite) Marshal.ThrowExceptionForHR(CreatePseudoConsole(new Coord { X = 80, Y = 25 }, inputRead, outputWrite, 0, out console));
        }
        nuint size = 0;
        InitializeProcThreadAttributeList(0, 1, 0, ref size);
        var attributes = Marshal.AllocHGlobal((nint)size);
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)) throw new Win32Exception();
            try
            {
                if (!UpdateProcThreadAttribute(attributes, 0, 0x20016, console, (nuint)IntPtr.Size, 0, 0)) throw new Win32Exception();
                var startup = new StartupEx { Info = new Startup { Size = Marshal.SizeOf<StartupEx>() }, Attributes = attributes };
                var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                // Escape redirection so the nested shell writes the readiness file.
                var command = new StringBuilder("cmd.exe /d /q /k cmd.exe /d /q /k echo ready ^> " + ReadyFile);
                if (!CreateProcess(cmd, command, 0, 0, false, 0x80000, 0, directory, ref startup, out var created)) throw new Win32Exception();
                CloseHandle(created.Thread); CloseHandle(created.Process);
                process = Process.GetProcessById(created.ProcessId);
            }
            finally { DeleteProcThreadAttributeList(attributes); }
        }
        finally { Marshal.FreeHGlobal(attributes); }
    }

    public Task WaitForExitAsync() => process!.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));

    private void CloseConsole()
    {
        var handle = Interlocked.Exchange(ref console, 0);
        if (handle != 0) ClosePseudoConsole(handle);
    }

    public async ValueTask DisposeAsync()
    {
        if (process != null)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(); process.Dispose(); process = null;
        }
        CloseConsole();
        input?.Dispose();
        if (drain != null) await drain.WaitAsync(TimeSpan.FromSeconds(3));
        output?.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)] private struct Coord { public short X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Startup
    {
        public int Size; public nint Reserved, Desktop, Title; public uint X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
        public short Show, ReservedSize; public nint Reserved2, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct StartupEx { public Startup Info; public nint Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public nint Process, Thread; public int ProcessId, ThreadId; }
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, nint security, uint size);
    [DllImport("kernel32.dll")] private static extern int CreatePseudoConsole(Coord size, SafeFileHandle input, SafeFileHandle output, uint flags, out nint console);
    [DllImport("kernel32.dll")] private static extern void ClosePseudoConsole(nint console);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(nint list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(nint list, uint flags, nuint attribute, nint value, nuint size, nint previous, nint returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(nint list);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcess(string app, StringBuilder command, nint processSecurity, nint threadSecurity, bool inherit, uint flags, nint environment, string directory, ref StartupEx startup, out ProcessInfo process);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}

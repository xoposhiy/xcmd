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
    private SafeFileHandle? shellInput;
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
        using var consoleInput = inputRead;
        if (!CreatePipe(out var outputRead, out var outputWrite, 0, 0)) throw new Win32Exception();
        using var consoleOutput = outputWrite;
        output = new FileStream(outputRead, FileAccess.Read);
        drain = Task.Run(async () => { try { await output.CopyToAsync(Stream.Null); } catch (IOException) { } });
        Marshal.ThrowExceptionForHR(CreatePseudoConsole(new Coord { X = 80, Y = 25 }, consoleInput, consoleOutput, 0, out console));

        if (!CreatePipe(out var shellRead, out var shellWrite, 0, 0)) throw new Win32Exception();
        shellInput = shellWrite;
        using var standardInput = shellRead;
        const uint inheritHandle = 1;
        if (!SetHandleInformation(standardInput, inheritHandle, inheritHandle) ||
            !SetHandleInformation(consoleOutput, inheritHandle, inheritHandle)) throw new Win32Exception();
        StartShell(directory, standardInput.DangerousGetHandle(), consoleOutput.DangerousGetHandle());
    }

    private void StartShell(string directory, nint standardInput, nint standardOutput)
    {
        const uint useStandardHandles = 0x100;
        const uint extendedStartupInfoPresent = 0x80000;
        const uint pseudoConsoleAttribute = 0x20016;
        const uint handleListAttribute = 0x20002;
        nuint size = 0;
        InitializeProcThreadAttributeList(0, 2, 0, ref size);
        var attributes = Marshal.AllocHGlobal((nint)size);
        nint handles = 0;
        try
        {
            handles = Marshal.AllocHGlobal(2 * IntPtr.Size);
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref size)) throw new Win32Exception();
            try
            {
                Marshal.WriteIntPtr(handles, standardInput);
                Marshal.WriteIntPtr(handles, IntPtr.Size, standardOutput);
                if (!UpdateProcThreadAttribute(attributes, 0, pseudoConsoleAttribute, console, (nuint)IntPtr.Size, 0, 0) ||
                    !UpdateProcThreadAttribute(attributes, 0, handleListAttribute, handles, (nuint)(2 * IntPtr.Size), 0, 0))
                    throw new Win32Exception();

                // Keep both prompts waiting on the fixture's pipe even when the runner's input is closed.
                var startup = new StartupEx
                {
                    Info = new Startup
                    {
                        Size = Marshal.SizeOf<StartupEx>(),
                        Flags = useStandardHandles,
                        Input = standardInput,
                        Output = standardOutput,
                        Error = standardOutput
                    },
                    Attributes = attributes
                };
                var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                // Escape redirection so the nested shell writes the readiness file.
                var command = new StringBuilder("cmd.exe /d /q /k cmd.exe /d /q /k echo ready ^> " + ReadyFile);
                if (!CreateProcess(cmd, command, 0, 0, true, extendedStartupInfoPresent, 0, directory, ref startup, out var created))
                    throw new Win32Exception();
                try { process = Process.GetProcessById(created.ProcessId); }
                finally { CloseHandle(created.Thread); CloseHandle(created.Process); }
            }
            finally { DeleteProcThreadAttributeList(attributes); }
        }
        finally
        {
            Marshal.FreeHGlobal(handles);
            Marshal.FreeHGlobal(attributes);
        }
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
        shellInput?.Dispose();
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
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}

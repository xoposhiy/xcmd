// @implements DeleteEntries, WindowsFileActions
using System.Runtime.InteropServices;

namespace Xcmd.Infrastructure;

public static unsafe class WindowsRecycleBin
{
    public static Task SendAsync(string path, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Recycle Bin requires Windows.");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { cancellationToken.ThrowIfCancellationRequested(); Send(path, cancellationToken); completion.TrySetResult(); }
            catch (OperationCanceledException) { completion.TrySetCanceled(cancellationToken); }
            catch (Exception error) { completion.TrySetException(error); }
        })
        { IsBackground = true, Name = "Xcmd Recycle Bin" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static void Send(string path, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Recycle Bin requires Windows.");
        if (new DriveInfo(Path.GetPathRoot(path)!).DriveType != DriveType.Fixed)
            throw new IOException("Recycle Bin is supported only on local fixed drives. No permanent deletion was attempted.");
        Marshal.ThrowExceptionForHR(CoInitializeEx(0, 2));
        try { Recycle(path, cancellationToken); }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested && error is COMException or OperationCanceledException)
        { throw new IOException("The item could not be sent to the Recycle Bin: " + error.Message, error); }
        finally { CoUninitialize(); }
    }

    private static void Recycle(string path, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Recycle Bin requires Windows.");
        var classId = new Guid("3AD05575-8857-4850-9277-11B85BDB8E09");
        var operationId = new Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8");
        var itemId = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        Marshal.ThrowExceptionForHR(CoCreateInstance(in classId, 0, 1, in operationId, out var operation));
        try
        {
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, 0, in itemId, out var item));
            try
            {
                const uint flags = 0x0004 | 0x0010 | 0x0040 | 0x0400 | 0x00080000;
                var table = *(nint**)operation;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int>)table[5])(operation, flags));
                var sink = new RecycleProgressSink(cancellationToken);
                var progress = Marshal.GetComInterfaceForObject(sink, typeof(IRecycleProgressSink));
                try
                {
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)table[18])(operation, item, progress));
                    var result = ((delegate* unmanaged[Stdcall]<nint, int>)table[21])(operation);
                    if (sink.RefusedPermanentDeletion) throw new IOException("Windows cannot recycle this item. Permanent deletion was refused; the item was kept.");
                    Marshal.ThrowExceptionForHR(result);
                    Marshal.ThrowExceptionForHR(sink.Result);
                    var aborted = 0;
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int*, int>)table[22])(operation, &aborted));
                    if (aborted != 0 || !sink.Recycled) throw new IOException("Windows did not confirm recycling. Check the operation result and Recycle Bin availability.");
                }
                finally { Marshal.Release(progress); }
            }
            finally { Marshal.Release(item); }
        }
        finally { Marshal.Release(operation); }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(in Guid classId, nint outer, uint context, in Guid interfaceId, out nint instance);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHCreateItemFromParsingName(string path, nint context, in Guid interfaceId, out nint item);
}

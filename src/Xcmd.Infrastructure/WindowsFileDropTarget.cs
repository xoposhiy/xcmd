using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using static Xcmd.Infrastructure.FileDragNative;

namespace Xcmd.Infrastructure;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class WindowsFileDropTarget(nint owner, Func<ShellDropPoint, string?> locate, Action<bool> activity,
    Action<string> reportError, Action<uint> completed) : IShellDropTarget, IDisposable
{
    private IDataObject? data;
    private IShellDropTarget? target;
    private string? destination;
    private bool registered;

    public void Register()
    {
        Marshal.ThrowExceptionForHR(OleInitialize(0));
        // The file surface owns OLE drops for this window, including Shell-only formats.
        RevokeDragDrop(owner);
        var result = RegisterDragDrop(owner, this);
        if (result < 0) { OleUninitialize(); Marshal.ThrowExceptionForHR(result); }
        registered = true;
    }

    public int DragEnter(IDataObject value, uint keys, ShellDropPoint point, ref uint effect)
    {
        DragLeave(); data = value; activity(true);
        return DragOver(keys, point, ref effect);
    }

    public int DragOver(uint keys, ShellDropPoint point, ref uint effect)
    {
        try
        {
            var path = locate(point);
            if (!string.Equals(destination, path, StringComparison.OrdinalIgnoreCase))
            {
                LeaveTarget();
                if (path != null && data != null)
                {
                    target = Target(path, owner); destination = path;
                    Marshal.ThrowExceptionForHR(target.DragEnter(data, keys, point, ref effect));
                    return 0;
                }
            }
            if (target == null) effect = 0;
            else Marshal.ThrowExceptionForHR(target.DragOver(keys, point, ref effect));
        }
        catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException or ArgumentException)
        { effect = 0; LeaveTarget(); reportError("Drag and drop: " + error.Message); }
        return 0;
    }

    public int Drop(IDataObject value, uint keys, ShellDropPoint point, ref uint effect)
    {
        try
        {
            DragOver(keys, point, ref effect);
            if (target == null || effect == 0) effect = 0;
            else
            {
                var dropTarget = target;
                target = null; destination = null;
                try { Marshal.ThrowExceptionForHR(dropTarget.Drop(value, keys, point, ref effect)); }
                finally { Release(dropTarget); }
            }
            completed(effect);
        }
        catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException or ArgumentException)
        { effect = 0; reportError("Drag and drop: " + error.Message); }
        finally { DragLeave(); }
        return 0;
    }

    public int DragLeave()
    {
        LeaveTarget(); data = null; activity(false);
        return 0;
    }

    private void LeaveTarget()
    {
        if (target != null) { target.DragLeave(); Release(target); target = null; }
        destination = null;
    }

    public void Dispose()
    {
        DragLeave();
        if (registered) { RevokeDragDrop(owner); OleUninitialize(); registered = false; }
    }
}

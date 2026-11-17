// @implements WindowsFileActions
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Xcmd.Infrastructure;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct ShellDropPoint(int X, int Y);

[ComImport, Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellDropTarget
{
    [PreserveSig] int DragEnter(IDataObject data, uint keys, ShellDropPoint point, ref uint effect);
    [PreserveSig] int DragOver(uint keys, ShellDropPoint point, ref uint effect);
    [PreserveSig] int DragLeave();
    [PreserveSig] int Drop(IDataObject data, uint keys, ShellDropPoint point, ref uint effect);
}

internal static class FileDragNative
{
    [ComImport, Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDropSource
    {
        [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape, uint keys);
        [PreserveSig] int GiveFeedback(uint effect);
    }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellFolder
    {
        void ParseDisplayName();
        void EnumObjects();
        void BindToObject();
        void BindToStorage();
        void CompareIDs();
        void CreateViewObject();
        void GetAttributesOf();
        [PreserveSig] int GetUIObjectOf(nint owner, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] children, in Guid id, nint reserved, [MarshalAs(UnmanagedType.Interface)] out object value);
    }

    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemArray
    {
        [PreserveSig] int BindToHandler(nint context, in Guid handler, in Guid id, [MarshalAs(UnmanagedType.Interface)] out IDataObject data);
    }

    [DllImport("ole32.dll")] internal static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] internal static extern void OleUninitialize();
    [DllImport("ole32.dll")] internal static extern int RegisterDragDrop(nint window, IShellDropTarget target);
    [DllImport("ole32.dll")] internal static extern int RevokeDragDrop(nint window);
    [DllImport("ole32.dll")] internal static extern int DoDragDrop(IDataObject data, IDropSource source, uint allowed, out uint effect);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern int SHParseDisplayName(string path, nint context, out nint pidl, uint attributes, out uint resultAttributes);
    [DllImport("shell32.dll")] internal static extern int SHBindToParent(nint pidl, in Guid id, out IShellFolder folder, out nint child);
    [DllImport("shell32.dll")] internal static extern int SHCreateShellItemArrayFromIDLists(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] pidls, out IShellItemArray array);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);

    internal static void Release(object? value)
    {
        if (value != null && OperatingSystem.IsWindows() && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    internal static IShellDropTarget Target(string path, nint owner)
    {
        Marshal.ThrowExceptionForHR(SHParseDisplayName(path, 0, out var pidl, 0, out _));
        IShellFolder? folder = null;
        try
        {
            Marshal.ThrowExceptionForHR(SHBindToParent(pidl, typeof(IShellFolder).GUID, out folder, out var child));
            Marshal.ThrowExceptionForHR(folder.GetUIObjectOf(owner, 1, [child], typeof(IShellDropTarget).GUID, 0, out var target));
            return (IShellDropTarget)target;
        }
        finally { Release(folder); Marshal.FreeCoTaskMem(pidl); }
    }
}

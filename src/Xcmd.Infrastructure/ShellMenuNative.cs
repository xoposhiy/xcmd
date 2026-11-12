using System.Runtime.InteropServices;
using System.Text;

namespace Xcmd.Infrastructure;

internal static class ShellMenuNative
{
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
        [PreserveSig] int GetUIObjectOf(nint owner, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] children, in Guid id, nint reserved, [MarshalAs(UnmanagedType.Interface)] out IContextMenu menu);
        void GetDisplayNameOf();
        void SetNameOf();
    }

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(in InvokeInfo info);
        [PreserveSig] int GetCommandString(nuint command, uint flags, nint reserved, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, uint size);
    }

    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IContextMenu2
    {
        void QueryContextMenu();
        void InvokeCommand();
        void GetCommandString();
        [PreserveSig] int HandleMenuMsg(uint message, nint wParam, nint lParam);
    }

    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IContextMenu3
    {
        void QueryContextMenu();
        void InvokeCommand();
        void GetCommandString();
        void HandleMenuMsg();
        [PreserveSig] int HandleMenuMsg2(uint message, nint wParam, nint lParam, out nint result);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct InvokeInfo
    {
        public int Size;
        public uint Mask;
        public nint Owner, Verb, Parameters, Directory;
        public int Show;
        public uint HotKey;
        public nint Icon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ExecuteInfo
    {
        public int Size;
        public uint Mask;
        public nint Owner;
        [MarshalAs(UnmanagedType.LPWStr)] public string Verb;
        [MarshalAs(UnmanagedType.LPWStr)] public string File;
        public nint Parameters, Directory;
        public int Show;
        public nint Instance, IdList, Class;
        public nint ClassKey;
        public uint HotKey;
        public nint Icon, Process;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHParseDisplayName(string path, nint context, out nint pidl, uint attributes, out uint resultAttributes);
    [DllImport("shell32.dll")]
    internal static extern int SHBindToParent(nint pidl, in Guid id, out IShellFolder folder, out nint child);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShellExecuteExW(ref ExecuteInfo info);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint CreatePopupMenu();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")]
    internal static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AppendMenuW(nint menu, uint flags, nuint command, string? text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetMenuStringW(nint menu, uint command, StringBuilder text, int maximum, uint flags);
}


// @tests OpenExternalTool, ExternalLaunch, StableShortcut, WindowsIdentity
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Xcmd.Probe.Infrastructure;

public sealed record ShortcutDetails(string Target, string WorkingDirectory, string Arguments, string Icon, int IconIndex);

public static class ShortcutProbe
{
    public static void Create(string path, string target, string workingDirectory)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The shortcut fixture requires Windows.");
        var link = CreateLink();
        try
        {
            link.SetPath(target);
            link.SetWorkingDirectory(workingDirectory);
            ((IPersistFile)link).Save(path, true);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    public static ShortcutDetails Read(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The shortcut fixture requires Windows.");
        var link = CreateLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var target = new StringBuilder(32768);
            var directory = new StringBuilder(32768);
            var arguments = new StringBuilder(32768);
            var icon = new StringBuilder(32768);
            link.GetPath(target, target.Capacity, 0, 4);
            link.GetWorkingDirectory(directory, directory.Capacity);
            link.GetArguments(arguments, arguments.Capacity);
            link.GetIconLocation(icon, icon.Capacity, out var iconIndex);
            return new(target.ToString(), directory.ToString(), arguments.ToString(), icon.ToString(), iconIndex);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    private static IShellLinkW CreateLink()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The shortcut fixture requires Windows.");
        return (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(new("00021401-0000-0000-C000-000000000046"))!)!;
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, nint findData, uint flags);
        void GetIDList(out nint idList);
        void SetIDList(nint idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int capacity);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int capacity);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int capacity);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out ushort hotkey);
        void SetHotkey(ushort hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int capacity, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}

// @tests StableShortcut, IntegrationVerification
using System.ComponentModel;
using System.Runtime.InteropServices;
using Xcmd.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class TaskbarProbe
{
    public static void CheckShortcut(LauncherFixture fixture, string? icon = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The taskbar probe requires Windows.");
        var shortcut = ShortcutProbe.Read(fixture.Shortcut);
        dynamic explorer = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
        dynamic folder = explorer.NameSpace(fixture.Root);
        dynamic item = folder.ParseName("Xcmd.lnk");
        try
        {
            Equal(fixture.Launcher, shortcut.Target, "The shortcut uses the permanent launcher");
            Equal(fixture.Root, shortcut.WorkingDirectory, "The shortcut has a stable working directory");
            Equal("", shortcut.Arguments, "The shortcut does not select a fixed release");
            Equal(icon ?? fixture.Launcher, shortcut.Icon, "The shortcut uses the configured application icon");
            Equal(0, shortcut.IconIndex, "The shortcut uses the first icon");
            Equal("Xcmd.FileManager", (string)item.ExtendedProperty("System.AppUserModel.ID"), "The shortcut has the shared taskbar identity");
        }
        finally
        {
            foreach (object instance in new object[] { item, folder, explorer }) Marshal.FinalReleaseComObject(instance);
        }
    }

    public static string ReadShortcutIcon(LauncherFixture fixture) => ShortcutProbe.Read(fixture.Shortcut).Icon;

    public static byte[] ReadIconPixels(string path) => SystemIcons.Read(path, false).Bytes;

    public static void WriteSolidIcon(string path, byte red, byte green, byte blue)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
        writer.Write((byte)16); writer.Write((byte)16); writer.Write((ushort)0);
        writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(1128); writer.Write(22);
        writer.Write(40); writer.Write(16); writer.Write(32); writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(new byte[24]);
        for (var pixel = 0; pixel < 256; pixel++) { writer.Write(blue); writer.Write(green); writer.Write(red); writer.Write((byte)255); }
        writer.Write(new byte[64]);
    }

    public static void CheckWindowIdentity(string launcher)
    {
        var window = CreateWindowExW(0, "STATIC", "Xcmd shell fixture", 0, 0, 0, 1, 1, 0, 0, 0, 0);
        if (window == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            Check(!IsWindowVisible(window), "The native shell fixture stays invisible.");
            Equal<string?>(null, WindowsTaskbar.ConfigureProcess(), "Process identity setup succeeds");
            Marshal.ThrowExceptionForHR(GetCurrentProcessExplicitAppUserModelID(out var applicationId));
            try { Equal("Xcmd.FileManager", Marshal.PtrToStringUni(applicationId), "The process has the shared taskbar identity"); }
            finally { Marshal.FreeCoTaskMem(applicationId); }
            Equal<string?>(null, WindowsTaskbar.ConfigureWindow(window, launcher), "Window identity setup succeeds");
            Equal("Xcmd.FileManager", ReadProperty(window, 5), "The window shares the shortcut identity");
            Equal('"' + launcher + '"', ReadProperty(window, 2), "Pinning a release window uses the permanent launcher");
            Equal("Xcmd", ReadProperty(window, 4), "The pinned application has a name");
            Equal(launcher + ",0", ReadProperty(window, 3), "The pinned application has a stable icon");
            var icon = Path.Combine(Path.GetDirectoryName(launcher)!, "app.ico");
            Equal<string?>(null, WindowsTaskbar.ConfigureWindow(window, launcher, icon), "A released window accepts its own icon");
            Equal(icon + ",0", ReadProperty(window, 3), "Pinning a released window uses the released icon");
            Equal<string?>(null, WindowsTaskbar.ClearWindow(window), "Window identity cleanup succeeds");
            foreach (var id in new uint[] { 2, 3, 4, 5 }) Equal<string?>(null, ReadProperty(window, id), "Window properties are cleared before destruction");
            Check(WindowsTaskbar.ConfigureWindow(0, launcher) != null, "A shell setup failure returns diagnostics without aborting startup.");
            Check(WindowsTaskbar.ClearWindow(0) != null, "A shell cleanup failure cannot abort application close.");
        }
        finally { DestroyWindow(window); }
    }

    private static string? ReadProperty(nint window, uint id)
    {
        var interfaceId = typeof(IPropertyStore).GUID;
        Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window, in interfaceId, out var store));
        var key = new PropertyKey { Format = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = id };
        try
        {
            Marshal.ThrowExceptionForHR(store.GetValue(in key, out var value));
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Text) : null; }
            finally { PropVariantClear(ref value); }
        }
        finally { if (OperatingSystem.IsWindows()) Marshal.FinalReleaseComObject(store); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropertyVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Text; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(in PropertyKey key, out PropertyVariant value);
        [PreserveSig] int SetValue(in PropertyKey key, in PropertyVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("shell32.dll")] private static extern int GetCurrentProcessExplicitAppUserModelID(out nint id);
    [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(nint window, in Guid id, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropertyVariant value);
}

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Xcmd.Infrastructure;

public static class WindowsShortcut
{
    public const string ApplicationId = "Xcmd.FileManager";

    public static void Create(string path, string launcher)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Xcmd shortcuts require Windows.");
        launcher = Path.GetFullPath(launcher);
        var type = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), throwOnError: true)!;
        var instance = Activator.CreateInstance(type)!;
        try
        {
            var link = (IShellLink)instance;
            link.SetPath(launcher);
            link.SetWorkingDirectory(Path.GetDirectoryName(launcher)!);
            link.SetDescription("Open the latest verified Xcmd release");
            link.SetIconLocation(launcher, 0);
            link.SetShowCmd(1);
            using (var properties = new ShellProperties((ShellProperties.IPropertyStore)instance, ownsStore: false))
            {
                properties.Set(5, ApplicationId);
                properties.Commit();
                ((IPersistFile)instance).Save(Path.GetFullPath(path), true);
            }
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, nint data, uint flags);
        void GetIDList(out nint list);
        void SetIDList(nint list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short key);
        void SetHotkey(short key);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}

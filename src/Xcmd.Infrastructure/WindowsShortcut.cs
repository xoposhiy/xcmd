using System.Runtime.InteropServices;

namespace Xcmd.Infrastructure;

public static unsafe class WindowsShortcut
{
    public const string ApplicationId = "Xcmd.FileManager";

    public static void Create(string path, string launcher)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Xcmd shortcuts require Windows.");
        var apartment = CoInitializeEx(0, 2);
        if (apartment < 0 && apartment != unchecked((int)0x80010106)) Marshal.ThrowExceptionForHR(apartment);
        try { CreateLink(path, launcher); }
        finally { if (apartment >= 0) CoUninitialize(); }
    }

    private static void CreateLink(string path, string launcher)
    {
        launcher = Path.GetFullPath(launcher);
        var classId = new Guid("00021401-0000-0000-C000-000000000046");
        var linkId = new Guid("000214F9-0000-0000-C000-000000000046");
        Marshal.ThrowExceptionForHR(CoCreateInstance(in classId, 0, 1, in linkId, out var link));
        try
        {
            SetText(link, 20, launcher);
            SetText(link, 9, Path.GetDirectoryName(launcher)!);
            SetText(link, 7, "Open the latest verified Xcmd release");
            fixed (char* icon = launcher)
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, int, int>)Table(link)[17])(link, icon, 0));
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Table(link)[15])(link, 1));
            SetApplicationId(link);
            Save(link, Path.GetFullPath(path));
        }
        finally { Marshal.Release(link); }
    }

    // Fixed COM vtables keep shortcut creation compatible with the NativeAOT launcher.
    private static nint* Table(nint instance) => *(nint**)instance;

    private static void SetText(nint link, int slot, string text)
    {
        fixed (char* value = text)
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, int>)Table(link)[slot])(link, value));
    }

    private static void SetApplicationId(nint link)
    {
        var id = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(link, in id, out var store));
        try
        {
            var key = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
            fixed (char* text = ApplicationId)
            {
                var value = new PropertyVariant { Type = 31, Text = (nint)text };
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, PropertyKey*, PropertyVariant*, int>)Table(store)[6])(store, &key, &value));
            }
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int>)Table(store)[7])(store));
        }
        finally { Marshal.Release(store); }
    }

    private static void Save(nint link, string path)
    {
        var id = new Guid("0000010b-0000-0000-C000-000000000046");
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(link, in id, out var file));
        try
        {
            fixed (char* text = path)
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, int, int>)Table(file)[6])(file, text, 1));
        }
        finally { Marshal.Release(file); }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(in Guid classId, nint outer, uint context, in Guid interfaceId, out nint instance);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(nint reserved, uint flags);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();
}

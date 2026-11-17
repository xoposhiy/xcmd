// @implements WindowsIdentity
using System.Runtime.InteropServices;

namespace Xcmd.Infrastructure;

internal sealed class ShellProperties(ShellProperties.IPropertyStore store, bool ownsStore = true) : IDisposable
{
    private static readonly Guid ApplicationProperties = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");

    public static ShellProperties ForWindow(nint window)
    {
        var id = typeof(IPropertyStore).GUID;
        Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window, in id, out var store));
        return new(store);
    }

    public void Set(uint id, string? text)
    {
        var key = new PropertyKey(ApplicationProperties, id);
        var value = new PropertyVariant { Type = text == null ? (ushort)0 : (ushort)31, Text = text == null ? 0 : Marshal.StringToCoTaskMemUni(text) };
        try { Marshal.ThrowExceptionForHR(store.SetValue(in key, in value)); }
        finally { Marshal.FreeCoTaskMem(value.Text); }
    }

    public void Commit() => Marshal.ThrowExceptionForHR(store.Commit());
    public void Dispose()
    {
        if (ownsStore && OperatingSystem.IsWindows()) Marshal.FinalReleaseComObject(store);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(in PropertyKey key, out PropertyVariant value);
        [PreserveSig] int SetValue(in PropertyKey key, in PropertyVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetPropertyStoreForWindow(nint window, in Guid id, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
}

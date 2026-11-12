using System.ComponentModel;
using System.Runtime.InteropServices;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed record SystemIconPixels(int Size, byte[] Bytes);

public static class SystemIcons
{
    public static SystemIconPixels Read(string path, bool directory, int minimumSize = 16, bool useFileAttributes = false)
    {
        var initialized = CoInitializeEx(0, 0) >= 0;
        var info = new ShellFileInfo();
        try
        {
            var attributesOnly = useFileAttributes || ArchivePath.IsEntry(path) || !directory && Path.GetExtension(path).ToLowerInvariant() is not (".exe" or ".lnk" or ".ico");
            var flags = 0x100u | 1u | (attributesOnly ? 0x10u : 0);
            var shellName = ShellName(path, directory, attributesOnly);
            if (SHGetFileInfo(shellName, directory ? 0x10u : 0x80u, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), flags) == 0 || info.Icon == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the system icon.");
            var size = NativeSize(info.Icon);
            if (size < minimumSize)
            {
                DestroyIcon(info.Icon); info.Icon = 0;
                if (SHGetFileInfo(shellName, directory ? 0x10u : 0x80u, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), flags & ~1u) == 0 || info.Icon == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the system icon.");
            }
            // Shell sizes are discrete (often 16 or 32). Render to the requested
            // physical size without changing the icon's 16-DIP layout slot.
            return Render(info.Icon, minimumSize);
        }
        finally { if (info.Icon != 0) DestroyIcon(info.Icon); if (initialized) CoUninitialize(); }
    }

    private static string ShellName(string path, bool directory, bool attributesOnly)
    {
        if (!attributesOnly) return path;
        if (directory) return "folder";
        return Path.GetExtension(path) is { Length: > 0 } extension ? extension : "file";
    }

    private static int NativeSize(nint icon)
    {
        if (!GetIconInfo(icon, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var bitmap = new NativeBitmap();
            if (GetObject(info.Color != 0 ? info.Color : info.Mask, Marshal.SizeOf<NativeBitmap>(), ref bitmap) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return bitmap.Width;
        }
        finally { if (info.Color != 0) DeleteObject(info.Color); if (info.Mask != 0) DeleteObject(info.Mask); }
    }

    private static SystemIconPixels Render(nint icon, int size)
    {
        var dc = CreateCompatibleDC(0);
        if (dc == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var bitmapInfo = new BitmapInfo { HeaderSize = 40, Width = size, Height = -size, Planes = 1, Bits = 32 };
        var bitmap = CreateDIBSection(dc, ref bitmapInfo, 0, out var pixels, 0, 0);
        if (bitmap == 0) { DeleteDC(dc); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        var previous = SelectObject(dc, bitmap);
        try
        {
            var bytes = new byte[size * size * 4]; Marshal.Copy(bytes, 0, pixels, bytes.Length);
            if (!DrawIconEx(dc, 0, 0, icon, size, size, 0, 0, 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
            GdiFlush(); Marshal.Copy(pixels, bytes, 0, bytes.Length);
            return new(size, bytes);
        }
        finally { SelectObject(dc, previous); DeleteObject(bitmap); DeleteDC(dc); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon; public int Index; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string? DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string? TypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint HeaderSize; public int Width; public int Height; public ushort Planes; public ushort Bits;
        public uint Compression; public uint ImageSize; public int XPixels; public int YPixels; public uint UsedColors; public uint ImportantColors; public uint Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo { public int Icon; public uint X; public uint Y; public nint Mask; public nint Color; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap { public int Type; public int Width; public int Height; public int WidthBytes; public ushort Planes; public ushort Bits; public nint Data; }

    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetIconInfo(nint icon, out IconInfo info);
    [DllImport("gdi32.dll", EntryPoint = "GetObjectW", SetLastError = true)] private static extern int GetObject(nint bitmap, int size, ref NativeBitmap info);

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW", SetLastError = true)] private static extern nint SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DrawIconEx(nint dc, int x, int y, nint icon, int width, int height, uint step, nint brush, uint flags);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint pixels, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GdiFlush();
}

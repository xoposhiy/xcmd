// @implements WindowsIdentity
using System.Runtime.InteropServices;

namespace Xcmd.Infrastructure;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct PropertyKey(Guid format, uint id)
{
    public readonly Guid Format = format;
    public readonly uint Id = id;
}

[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropertyVariant
{
    [FieldOffset(0)] public ushort Type;
    [FieldOffset(8)] public nint Text;
}

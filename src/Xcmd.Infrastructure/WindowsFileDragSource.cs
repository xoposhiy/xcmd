// @implements WindowsFileActions
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using static Xcmd.Infrastructure.FileDragNative;

namespace Xcmd.Infrastructure;

public interface IFileDragSource
{
    (int Width, int Height) DragSize { get; }
    uint Drag(string[] paths, bool rightButton);
}

public sealed class WindowsFileDragSource : IFileDragSource
{
    public (int Width, int Height) DragSize => (GetSystemMetrics(68), GetSystemMetrics(69));

    public uint Drag(string[] paths, bool rightButton)
    {
        Marshal.ThrowExceptionForHR(OleInitialize(0));
        try
        {
            using var data = new ShellDragData(paths);
            Marshal.ThrowExceptionForHR(DoDragDrop(data.Data, new Source(rightButton), 7, out var effect));
            return effect;
        }
        catch (COMException error) { throw new IOException("Windows could not drag the selected files.", error); }
        finally { OleUninitialize(); }
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Source(bool rightButton) : IDropSource
    {
        public int QueryContinueDrag(bool escape, uint keys) => escape ? 0x40101 : (keys & (rightButton ? 2u : 1u)) == 0 ? 0x40100 : 0;
        public int GiveFeedback(uint effect) => 0x40102;
    }
}

public sealed class ShellDragData : IDisposable
{
    public IDataObject Data { get; }

    public ShellDragData(string[] paths)
    {
        if (paths.Length == 0) throw new ArgumentException("Select files to drag.", nameof(paths));
        var pidls = new List<nint>();
        IShellItemArray? array = null;
        try
        {
            foreach (var path in paths)
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(path, 0, out var pidl, 0, out _));
                pidls.Add(pidl);
            }
            Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)pidls.Count, pidls.ToArray(), out array));
            var handler = new Guid("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");
            Marshal.ThrowExceptionForHR(array.BindToHandler(0, handler, typeof(IDataObject).GUID, out var data));
            Data = data;
        }
        finally
        {
            Release(array);
            foreach (var pidl in pidls) Marshal.FreeCoTaskMem(pidl);
        }
    }

    public void Dispose() => Release(Data);
}

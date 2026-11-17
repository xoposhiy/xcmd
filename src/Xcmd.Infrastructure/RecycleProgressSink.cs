// @implements DeleteEntries, WindowsFileActions
using System.Runtime.InteropServices;

namespace Xcmd.Infrastructure;

[ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRecycleProgressSink
{
    [PreserveSig] int StartOperations();
    [PreserveSig] int FinishOperations(int result);
    [PreserveSig] int PreRenameItem(uint flags, nint item, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostRenameItem(uint flags, nint item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, nint created);
    [PreserveSig] int PreMoveItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostMoveItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, nint created);
    [PreserveSig] int PreCopyItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostCopyItem(uint flags, nint item, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, nint created);
    [PreserveSig] int PreDeleteItem(uint flags, nint item);
    [PreserveSig] int PostDeleteItem(uint flags, nint item, int result, nint created);
    [PreserveSig] int PreNewItem(uint flags, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostNewItem(uint flags, nint destination, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attributes, int result, nint created);
    [PreserveSig] int UpdateProgress(uint total, uint completed);
    [PreserveSig] int ResetTimer();
    [PreserveSig] int PauseTimer();
    [PreserveSig] int ResumeTimer();
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class RecycleProgressSink(CancellationToken cancellationToken) : IRecycleProgressSink
{
    public bool RefusedPermanentDeletion { get; private set; }
    public int Result { get; private set; }
    public bool Recycled { get; private set; }
    public int StartOperations() => 0;
    public int FinishOperations(int result) { if (result < 0) Result = result; return 0; }
    public int PreDeleteItem(uint flags, nint item)
    {
        if (cancellationToken.IsCancellationRequested) return unchecked((int)0x800704C7);
        // The shell removes this flag when an item would be deleted permanently.
        RefusedPermanentDeletion |= (flags & 0x80) == 0;
        return RefusedPermanentDeletion ? unchecked((int)0x80004005) : 0;
    }
    public int PostDeleteItem(uint flags, nint item, int result, nint created)
    { Result = result; Recycled = result >= 0 && created != 0; return 0; }
    public int PreRenameItem(uint flags, nint item, string name) => 0;
    public int PostRenameItem(uint flags, nint item, string name, int result, nint created) => 0;
    public int PreMoveItem(uint flags, nint item, nint destination, string name) => 0;
    public int PostMoveItem(uint flags, nint item, nint destination, string name, int result, nint created) => 0;
    public int PreCopyItem(uint flags, nint item, nint destination, string name) => 0;
    public int PostCopyItem(uint flags, nint item, nint destination, string name, int result, nint created) => 0;
    public int PreNewItem(uint flags, nint destination, string name) => 0;
    public int PostNewItem(uint flags, nint destination, string name, string template, uint attributes, int result, nint created) => 0;
    public int UpdateProgress(uint total, uint completed) => 0;
    public int ResetTimer() => 0;
    public int PauseTimer() => 0;
    public int ResumeTimer() => 0;
}

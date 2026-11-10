using System.Runtime.InteropServices;

namespace Xcmd.Probe.Infrastructure;

public sealed class KeyboardLayoutFixture : IDisposable
{
    private readonly List<nint> added = [];
    public KeyboardLayoutFixture()
    {
        var loaded = new nint[GetKeyboardLayoutList(0, null)]; GetKeyboardLayoutList(loaded.Length, loaded);
        foreach (var name in new[] { "00000409", "00000419" })
        {
            var layout = LoadKeyboardLayout(name, 0x80);
            ProbeContext.Check(layout != 0, "The native keyboard-layout fixture can load " + name);
            if (!loaded.Contains(layout)) added.Add(layout);
        }
    }
    public void Dispose() { foreach (var layout in added) UnloadKeyboardLayout(layout); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadKeyboardLayoutW")] private static extern nint LoadKeyboardLayout(string name, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnloadKeyboardLayout(nint layout);
    [DllImport("user32.dll")] private static extern int GetKeyboardLayoutList(int count, [Out] nint[]? layouts);
}

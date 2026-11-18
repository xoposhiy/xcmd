// @tests DragFiles, WindowsFileActions, IntegrationVerification
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public sealed class RecordingFileDragSource : IFileDragSource
{
    public (int Width, int Height) DragSize => (4, 4);
    public List<(string[] Paths, bool Right)> Drags { get; } = [];
    public Action? DuringDrag { get; set; }
    public uint Drag(string[] paths, bool rightButton)
    {
        Drags.Add((paths, rightButton)); DuringDrag?.Invoke(); return 0;
    }
}

public static class FileDragProbe
{
    public static void Drag(ProbeContext c, string name, MouseButton button = MouseButton.Left)
    {
        var row = c.Row(name);
        var point = row.TranslatePoint(new Point(100, row.Bounds.Height / 2), c.Window)!.Value;
        c.Input.MovePointer(point);
        c.Input.PressPointer(point, button);
        c.Input.MovePointer(point + new Vector(20, 12), button == MouseButton.Right ? RawInputModifiers.RightMouseButton : RawInputModifiers.LeftMouseButton);
        c.Input.ReleasePointer(point + new Vector(20, 12), button);
    }

    public static PixelPoint RowPoint(ProbeContext c, string name) => c.Row(name).PointToScreen(new Point(100, 14));

    public static Task NativeAsync(string[] paths, string destination, uint keys, uint expected)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Shell drag tests require Windows.");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Native(paths, destination, keys, expected); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private static void Native(string[] paths, string destination, uint keys, uint expected)
    {
        Marshal.ThrowExceptionForHR(OleInitialize(0));
        var window = CreateWindowExW(0, "STATIC", "Xcmd hidden drop probe", 0, 0, 0, 100, 100, 0, 0, 0, 0);
        if (window == 0) throw new InvalidOperationException("The hidden drop target could not be created.");
        try
        {
            var errors = new List<string>();
            var busy = false;
            using var target = new WindowsFileDropTarget(window, _ => destination, value => busy = value, errors.Add, _ => { });
            target.Register();
            using var data = new ShellDragData(paths);
            uint effect = 7;
            target.DragEnter(data.Data, keys | 1, default, ref effect);
            ProbeContext.Check(busy, "The receiver blocks release handoff during a drag");
            ProbeContext.Equal(expected, effect, "Windows chooses the requested drop effect");
            effect = 7;
            target.Drop(data.Data, keys, default, ref effect);
            ProbeContext.Check(!busy, "The receiver clears drag state after dropping");
            ProbeContext.Check(errors.Count == 0, "The native Shell drop succeeds: " + string.Join("; ", errors));
            ProbeContext.Check(!IsWindowVisible(window), "The native drop target stays hidden");
        }
        finally { DestroyWindow(window); OleUninitialize(); }
    }

    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowExW(uint extended, string className, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
}

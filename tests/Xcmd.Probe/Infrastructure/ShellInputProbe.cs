using Avalonia;
using Avalonia.Input;

namespace Xcmd.Probe.Infrastructure;

public static class ShellInputProbe
{
    public static async Task RightClickAsync(ProbeContext context, string name, bool opposite = false, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var panel = opposite ? context.Window.OppositePanel : context.Window.ActivePanel;
        var row = context.Row(name, panel);
        var point = row.TranslatePoint(new Point(85, row.Bounds.Height / 2), context.Window)!.Value;
        context.Input.MovePointer(point, modifiers);
        context.Input.PressPointer(point, MouseButton.Right, modifiers);
        context.Input.ReleasePointer(point, MouseButton.Right, modifiers);
        await context.SettleAsync();
    }
}

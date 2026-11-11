using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Xcmd.Probe.Infrastructure;

public static class SearchFeedbackProbe
{
    public static async Task ResizeColumnAsync(ProbeContext context, Window window, TableView table, int index, double distance)
    {
        var header = table.GetVisualDescendants().OfType<TableViewColumnHeader>().ElementAt(index);
        var thumb = header.GetVisualDescendants().OfType<Thumb>().First();
        var origin = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
        var input = new InputDriver(window);
        input.MovePointer(origin); input.PressPointer(origin, MouseButton.Left);
        input.MovePointer(origin + new Vector(distance, 0), RawInputModifiers.LeftMouseButton);
        input.ReleasePointer(origin + new Vector(distance, 0), MouseButton.Left);
        await context.SettleAsync();
    }

    public static async Task JumpAsync(ProbeContext context, string text)
    {
        await context.KeyAsync(Key.Escape);
        context.Window.Left.FocusCurrent();
        context.Input.KeyDown(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
        await context.TextAsync(text);
        context.Input.KeyUp(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null);
        await context.SettleAsync();
    }
}

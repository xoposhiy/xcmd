// @tests QueueOperations, SharedControls
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class OperationInputProbe
{
    public static async Task ClickActionAsync(ProbeContext context, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), context.Window)!.Value;
        context.Input.MovePointer(point);
        context.Input.PressPointer(point, MouseButton.Left);
        context.Input.ReleasePointer(point, MouseButton.Left);
        await DrainRenderingAsync(context);
    }

    public static async Task FocusByTabAsync(ProbeContext context, Window window, Control control)
    {
        for (var step = 0; step < 20 && !control.IsFocused; step++)
        {
            context.PressKey(Key.Tab, target: window);
            await DrainRenderingAsync(context);
            var focused = window.FocusManager?.GetFocusedElement() as Control;
            context.InputTrace.Add($"focus: {focused?.GetType().Name} {focused?.Name}; queue: {context.Window.OperationsView.IsKeyboardFocusWithin}");
        }
        Check(control.IsFocused, "Tab reaches the requested action.");
    }

    public static MenuItem OperationHistoryCommand(ProbeContext context)
    {
        var mainMenu = context.Window.GetVisualDescendants().OfType<Menu>().Single();
        var view = mainMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "View"));
        return view.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "File operation history"));
    }

    public static async Task OpenOperationsMenuAsync(ProbeContext context)
    {
        var menu = context.Window.GetVisualDescendants().OfType<Menu>().Single();
        var view = (MenuItem)menu.Items[3]!;
        var point = view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height / 2), context.Window)!.Value;
        context.Input.MovePointer(point); context.Input.PressPointer(point, MouseButton.Left); context.Input.ReleasePointer(point, MouseButton.Left);
        context.PressKey(Key.Down); context.PressKey(Key.End); context.PressKey(Key.Enter);
        await DrainRenderingAsync(context);
    }
}

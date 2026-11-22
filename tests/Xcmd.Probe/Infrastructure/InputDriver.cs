// @implements HeadlessVerification
using System.Reflection;
using Avalonia.Platform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Xcmd.Probe.Infrastructure;

public sealed class InputDriver(TopLevel window)
{
    internal static void Deactivate(Window window)
    {
        var notification = typeof(IWindowBaseImpl).GetProperty("Deactivated", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (notification?.GetValue(window.PlatformImpl) is not Action deactivate)
            throw new InvalidOperationException("The headless window has no deactivation callback.");
        deactivate();
    }

    public void KeyDown(Key key, RawInputModifiers modifiers, PhysicalKey physical, string? symbol) => window.KeyPress(key, modifiers, physical, symbol);
    public void KeyUp(Key key, RawInputModifiers modifiers, PhysicalKey physical, string? symbol) => window.KeyRelease(key, modifiers, physical, symbol);
    public void Text(string text) => window.KeyTextInput(text);
    public bool TypeKey(Key key, PhysicalKey physical, string text)
    {
        KeyEventArgs? pressed = null;
        EventHandler<KeyEventArgs> observe = (_, e) => pressed = e;
        window.AddHandler(InputElement.KeyDownEvent, observe, RoutingStrategies.Bubble, handledEventsToo: true);
        try
        {
            KeyDown(key, RawInputModifiers.None, physical, text);
            if (pressed == null) throw new InvalidOperationException("The key did not reach the window input route.");
            if (!pressed.Handled) Text(text);
            return !pressed.Handled;
        }
        finally
        {
            KeyUp(key, RawInputModifiers.None, physical, text);
            window.RemoveHandler(InputElement.KeyDownEvent, observe);
        }
    }
    public void MovePointer(Point point, RawInputModifiers modifiers = RawInputModifiers.None) => window.MouseMove(point, modifiers);
    public void PressPointer(Point point, MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None) => window.MouseDown(point, button, modifiers);
    public void ReleasePointer(Point point, MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None) => window.MouseUp(point, button, modifiers);
}

public static class RenderScaling
{
    public static void SetScaleForTest(this TopLevel window, double scale) => window.SetRenderScaling(scale);
}

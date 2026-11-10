using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Xcmd.Probe.Infrastructure;

public sealed class InputDriver(TopLevel window)
{
    public void KeyDown(Key key, RawInputModifiers modifiers, PhysicalKey physical, string? symbol) => window.KeyPress(key, modifiers, physical, symbol);
    public void KeyUp(Key key, RawInputModifiers modifiers, PhysicalKey physical, string? symbol) => window.KeyRelease(key, modifiers, physical, symbol);
    public void Text(string text) => window.KeyTextInput(text);
    public void MovePointer(Point point, RawInputModifiers modifiers = RawInputModifiers.None) => window.MouseMove(point, modifiers);
    public void PressPointer(Point point, MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None) => window.MouseDown(point, button, modifiers);
    public void ReleasePointer(Point point, MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None) => window.MouseUp(point, button, modifiers);
}

public static class RenderScaling
{
    public static void SetScaleForTest(this TopLevel window, double scale) => window.SetRenderScaling(scale);
}

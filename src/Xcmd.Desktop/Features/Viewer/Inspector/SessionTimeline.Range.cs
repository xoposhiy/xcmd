// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class SessionTimeline
{
    private const double HandleWidth = 24;
    private readonly Canvas handles = new Canvas().WithFeedbackId("SessionInspector.rangeHandles");
    private readonly Border startHandle, endHandle;

    private Border AddRangeHandle(bool start)
    {
        var name = start ? "Range start" : "Range end";
        var arrow = Text(start ? "◂" : "▸", 16, Accent, true);
        arrow.HorizontalAlignment = HorizontalAlignment.Center;
        var handle = new Border
        {
            Width = HandleWidth,
            Height = 24,
            Focusable = true,
            Background = AccentSurface,
            BorderBrush = Accent,
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            Child = arrow
        }.WithFeedbackId("SessionInspector.rangeHandle");
        AutomationProperties.SetName(handle, name);
        ToolTip.SetTip(handle, name + " (drag or use arrow keys)");
        handle.GotFocus += (_, _) => handle.BorderBrush = UiPalette.Focus;
        handle.LostFocus += (_, _) => handle.BorderBrush = Accent;
        handle.KeyDown += (_, e) => StepRangeHandle(start, e);
        handles.Children.Add(handle);
        return handle;
    }

    internal void UpdateRangeHandles()
    {
        UpdateBars();
        Canvas.SetLeft(startHandle, RangeX(true) - HandleWidth);
        Canvas.SetLeft(endHandle, RangeX(false));
        InvalidateVisual();
    }

    private void StepRangeHandle(bool start, KeyEventArgs e)
    {
        var min = start ? ZoomStart : Range.From;
        var max = start ? Range.To : ZoomEnd;
        var page = CostAxis ? 5 : 10;
        var step = e.Key switch { Key.Left or Key.Down => -1, Key.Right or Key.Up => 1, Key.PageUp => page, Key.PageDown => -page, _ => 0 };
        if (step == 0 && e.Key is not (Key.Home or Key.End)) return;
        var at = start ? Range.From : Range.To;
        if (e.KeyModifiers == KeyModifiers.Shift) step *= 5;
        var next = e.Key switch
        {
            Key.Home => min,
            Key.End => max,
            _ when CostAxis => Math.Clamp(at + step, min, max),
            _ => Math.Clamp(at + step * 1000L, min, max)
        };
        if (start) SelectRange(next, Range.To); else SelectRange(Range.From, next);
        e.Handled = true;
        changed();
    }
}

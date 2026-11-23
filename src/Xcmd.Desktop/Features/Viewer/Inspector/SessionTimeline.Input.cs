// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Controls;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class SessionTimeline
{
    private enum Grip { None, New, Start, End }
    private Grip grip;
    private Point pressed;
    private long anchor;
    private Bar? keyboardBar;
    private bool dragged;
    internal event Action<string>? EventSelected;
    internal event Action? RangeMoving;

    private long TimeFor(double x)
    {
        var f = Math.Clamp((x - Rail) / PlotWidth, 0, 1);
        if (f <= 0) return selection.ZoomFrom;
        if (f >= 1) return selection.ZoomTo;
        var value = AxisFrom + f * (AxisTo - AxisFrom);
        return Tokens ? Math.Clamp(Trace.TimeAtTokens(value), selection.ZoomFrom, selection.ZoomTo) : (long)Math.Round(value);
    }

    private Bar? Hit(Point p)
    {
        var lane = (int)(p.Y / Row);
        if (p.X < Rail) return null;
        for (var i = bars.Count - 1; i >= 0; i--)
        {
            var bar = bars[i];
            if (bar.Lane != lane || !VisibleBar(bar)) continue;
            if (BarBounds(bar).Inflate(new Thickness(1, 5)).Contains(p)) return bar;
        }
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
        var p = e.GetPosition(this);
        if (p.X < Rail)
        {
            var index = (int)(p.Y / Row) - 3;
            if (index >= 0 && index < Trace.Agents.Count) filter(selection.Agent == Trace.Agents[index] ? null : Trace.Agents[index]);
            return;
        }
        Focus();
        e.Pointer.Capture(this);
        pressed = p;
        dragged = false;
        anchor = TimeFor(p.X);
        grip = Math.Abs(p.X - At(selection.From)) <= 5 ? Grip.Start : Math.Abs(p.X - At(selection.To)) <= 5 ? Grip.End : Grip.New;
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (grip == Grip.None)
        {
            var hit = Hit(p);
            var tip = hit is { } b ? $"{b.Title}\n{Time(Trace, b.Start)} – {Time(Trace, b.End)}" : null;
            if (!Equals(ToolTip.GetTip(this), tip)) ToolTip.SetTip(this, tip);
            return;
        }
        dragged |= Math.Abs(p.X - pressed.X) > 3;
        if (dragged) MoveGrip(TimeFor(p.X));
    }

    private void MoveGrip(long time)
    {
        if (grip == Grip.Start) selection.Select(Math.Min(time, selection.To), selection.To);
        else if (grip == Grip.End) selection.Select(selection.From, Math.Max(time, selection.From));
        else selection.Select(anchor, time);
        RangeMoving?.Invoke();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (grip == Grip.None) return;
        if (dragged) { MoveGrip(TimeFor(e.GetPosition(this).X)); changed(); }
        else if (Hit(pressed) is { } hit) Choose(hit);
        else if (grip == Grip.New) { selection.Select(selection.ZoomFrom, anchor); changed(); }
        grip = Grip.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (grip != Grip.None && dragged) changed();
        grip = Grip.None;
    }

    private void Choose(Bar bar)
    {
        keyboardBar = bar;
        Announce(bar);
        var from = Tokens && bar.Agent != null ? Trace.TimeAtTokens(bar.TokenStart) : bar.Start;
        if (from == bar.End && from > Trace.Start) from--;
        selection.Select(from, bar.End);
        changed();
        EventSelected?.Invoke(bar.Title);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyModifiers != KeyModifiers.None || bars.Count == 0) return;
        if (e.Key == Key.Enter && keyboardBar is { } selected && VisibleBar(selected)) Choose(selected);
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            var lanes = bars.Where(VisibleBar)
                .GroupBy(b => b.Lane).OrderBy(g => g.Key).ToArray();
            if (lanes.Length == 0) return;
            var i = Math.Max(0, Array.FindIndex(lanes, g => g.Key == keyboardBar?.Lane));
            if (e.Key == Key.Up) i = Math.Max(0, i - 1);
            if (e.Key == Key.Down) i = Math.Min(lanes.Length - 1, i + 1);
            var row = lanes[i].ToArray();
            var index = Array.FindIndex(row, b => b == keyboardBar);
            index = e.Key switch { Key.End => row.Length - 1, Key.Home => 0, Key.Left => index - 1, Key.Right => index + 1, _ => index };
            keyboardBar = row[Math.Clamp(index, 0, row.Length - 1)];
            Announce(keyboardBar.Value);
            EventSelected?.Invoke(keyboardBar.Value.Title + "  ·  Enter to select");
            InvalidateVisual();
        }
        else return;
        e.Handled = true;
    }

    private void Announce(Bar bar)
    {
        var label = bar.Agent ?? (bar.Lane == 1 ? "Task" : "Work phase");
        AutomationProperties.SetName(this, $"{label}: {bar.Title}. {Time(Trace, bar.Start)} to {Time(Trace, bar.End)}.");
    }
}

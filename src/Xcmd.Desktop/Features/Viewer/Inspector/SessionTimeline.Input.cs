// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class SessionTimeline
{
    private enum Grip { None, New, Start, End }
    private Grip grip;
    private Point pressed;
    private long anchor;
    private double gripOffset;
    private Bar? keyboardBar;
    private Bar? hoveredBar;
    private IDisposable? hoverTimer;
    private bool dragged;
    private bool doubleClick;
    internal event Action<string>? EventSelected;
    internal event Action<SessionCallBlock>? BlockSelected;
    internal event Action<ResponseHit>? ResponseSelected;

    private long PositionFor(double x)
    {
        if (CostAxis) return Projection.Nearest(x - Rail);
        var f = Math.Clamp((x - Rail) / PlotWidth, 0, 1);
        return selection.ZoomFrom + (long)Math.Round(f * (selection.ZoomTo - selection.ZoomFrom));
    }

    private (long From, long To) Range => CostAxis ? (selection.FirstResponse, selection.LastResponse) : (selection.From, selection.To);
    private long ZoomStart => CostAxis ? selection.ZoomFirstResponse : selection.ZoomFrom;
    private long ZoomEnd => CostAxis ? selection.ZoomLastResponse : selection.ZoomTo;
    private void SelectRange(long from, long to)
    {
        if (CostAxis) selection.SelectResponses((int)from, (int)to);
        else selection.Select(from, to);
    }

    private Bar? Hit(Point p)
    {
        var lane = LaneAt(p.Y);
        if (p.X < Rail) return null;
        Bar? nearby = null;
        for (var i = bars.Count - 1; i >= 0; i--)
        {
            var bar = bars[i];
            if (bar.Lane != lane || !VisibleBar(bar)) continue;
            var rect = BarBounds(bar);
            if (rect.Contains(p)) return bar;
            if (nearby == null && p.X >= rect.Left - 1 && p.X <= rect.Right + 1 && p.Y >= rect.Top && p.Y <= rect.Bottom)
                nearby = bar;
        }
        return nearby;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
        var cursor = e.GetCurrentPoint(this);
        var p = cursor.Position;
        if (cursor.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            ResetSpanZoom(p, e);
            return;
        }
        if (!cursor.Properties.IsLeftButtonPressed) return;
        UpdateHover(null);
        var handle = startHandle.Bounds.Contains(p) ? startHandle : endHandle.Bounds.Contains(p) ? endHandle : null;
        if (handle == null && p.X < Rail)
        {
            if (TrackAt(LaneAt(p.Y)) is { } track) filter(selection.Agent == track.Agent ? null : track.Agent);
            return;
        }
        if (handle != null) handle.Focus(); else Focus();
        e.Pointer.Capture(this);
        pressed = p;
        dragged = false;
        doubleClick = e.ClickCount == 2;
        anchor = PositionFor(p.X);
        grip = handle == startHandle ? Grip.Start : handle == endHandle ? Grip.End :
            Math.Abs(p.X - RangeX(true)) <= 5 ? Grip.Start : Math.Abs(p.X - RangeX(false)) <= 5 ? Grip.End : Grip.New;
        gripOffset = handle == null ? 0 : p.X - RangeX(grip == Grip.Start);
        e.Handled = true;
    }

    private void ResetSpanZoom(Point point, PointerPressedEventArgs e)
    {
        if (!selection.Zoomed || Hit(point)?.Lane is not (1 or 2)) return;
        UpdateHover(null);
        selection.ResetZoom();
        changed();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (grip == Grip.None)
        {
            UpdateHover(Hit(p));
            return;
        }
        dragged |= Math.Abs(p.X - pressed.X) > 3;
        if (dragged) MoveGrip(PositionFor(p.X - gripOffset));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateHover(null);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UpdateHover(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateHover(Bar? bar)
    {
        focus.Hover(bar?.Response);
        if (ReferenceEquals(bar, hoveredBar)) return;
        hoveredBar = bar;
        hoverTimer?.Dispose();
        hoverTimer = null;
        ToolTip.SetIsOpen(this, false);
        if (bar == null)
        {
            ToolTip.SetTip(this, null);
            return;
        }
        ToolTip.SetTip(this, bar.Block is { } block ? TimelineCallTip.Build(block) : BarDetails(bar));
        // Entering a bar can set the tip after the timeline receives PointerEntered.
        hoverTimer = DispatcherTimer.RunOnce(() =>
        {
            hoverTimer = null;
            ToolTip.SetIsOpen(this, true);
        }, TimeSpan.FromMilliseconds(ToolTip.GetShowDelay(this)));
    }

    private void MoveGrip(long position)
    {
        var range = Range;
        if (grip == Grip.Start) SelectRange(Math.Min(position, range.To), range.To);
        else if (grip == Grip.End) SelectRange(range.From, Math.Max(position, range.From));
        else SelectRange(anchor, position);
        if (range != Range) changed();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (grip == Grip.None) return;
        if (dragged) MoveGrip(PositionFor(e.GetPosition(this).X - gripOffset));
        else if (Hit(pressed) is { } hit)
        {
            if (doubleClick && hit.Lane is 1 or 2) ZoomToSpan(hit);
            else Choose(hit);
        }
        else if (grip == Grip.New) { SelectRange(ZoomStart, anchor); changed(); }
        grip = Grip.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ZoomToSpan(Bar bar)
    {
        SelectRange(bar.Start, bar.End);
        selection.ZoomToRange();
        changed();
        Announce(bar);
        EventSelected?.Invoke(bar.Title);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        grip = Grip.None;
    }

    private void Choose(Bar bar)
    {
        keyboardBar = bar;
        Announce(bar);
        if (bar.Response is { } response)
        {
            ResponseSelected?.Invoke(new(response, bar.Block?.Calls[0]));
            return;
        }
        var from = bar.Start;
        if (!CostAxis && from == bar.End && from > Trace.Start) from--;
        SelectRange(Math.Max(ZoomStart, from), Math.Min(ZoomEnd, bar.End));
        changed();
        if (bar.Block != null) BlockSelected?.Invoke(bar.Block);
        else EventSelected?.Invoke(bar.Title);
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
            var row = lanes[i].OrderBy(bar => bar.Start).ThenBy(bar => bar.Offset).ToArray();
            var index = Array.FindIndex(row, b => b == keyboardBar);
            index = e.Key switch { Key.End => row.Length - 1, Key.Home => 0, Key.Left => index - 1, Key.Right => index + 1, _ => index };
            keyboardBar = row[Math.Clamp(index, 0, row.Length - 1)];
            Announce(keyboardBar);
            EventSelected?.Invoke(keyboardBar.Title + "  ·  Enter to select");
            InvalidateVisual();
        }
        else return;
        e.Handled = true;
    }

    private void Announce(Bar bar)
    {
        var label = bar.Agent ?? (bar.Lane == 1 ? "Task" : "Work phase");
        var details = bar.Block is { } block ? TimelineCallTip.Describe(block) : BarDetails(bar);
        AutomationProperties.SetName(this, $"{label}: {details}");
    }

    private string BarDetails(Bar bar) => bar.Response is { } response
        ? CostDetails(response) : $"{bar.Title}\n{BarRange(bar)}";

    private string BarRange(Bar bar) => CostAxis ? $"{Usd(Trace.CostAxis.At((int)bar.Start).Usd)} – {Usd(Trace.CostAxis.At((int)bar.End).Usd)}" :
        $"{Time(Trace, bar.Start)} – {Time(Trace, bar.End)}";
}

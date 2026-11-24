// @implements InspectAgentSession
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed class ResourceTimeline : Control
{
    internal static readonly (string Action, string Caption)[] Actions =
        [("read", "Read"), ("write", "Write"), ("exec", "Execute"), ("other", "Other")];

    private readonly (long Start, long End, int Color)[] spans;
    private readonly IBrush[] palette;
    private readonly TimelineMark[] marks;
    private readonly (long From, long To) range;
    private readonly SessionSelection selection;
    private readonly ResponseFocus focus;
    private readonly Action<ResponseHit> selected;
    private readonly ResourceResponse[] responses;
    private sealed record ResourceResponse(SessionResponse Response, TraceCall[] Calls, IBrush[] Colors, TimelineMark[] Marks);

    internal ResourceTimeline(ResourceInterval[] intervals, SessionSelection selection, ResponseFocus focus,
        Action<ResponseHit> selected)
    {
        this.selection = selection;
        this.focus = focus;
        this.selected = selected;
        range = (selection.From, selection.To);
        responses = selection.CostAxis ? intervals.GroupBy(interval => selection.Trace.ResponseFor(interval.Call)!)
            .OrderBy(group => group.Key.Index).Select(group =>
                new ResourceResponse(group.Key, group.Select(interval => interval.Call).Distinct().OrderBy(call => call.EmissionOrder).ToArray(),
                    group.Select(Style).Distinct().OrderBy(style => style.Vivid)
                        .Select(style => InspectorStyle.Color(style.Target, style.Vivid)).ToArray(), Marks(group))).ToArray() : [];
        var colors = intervals.Select(Style).Distinct().OrderBy(style => style.Vivid).ToArray();
        palette = colors.Select(style => InspectorStyle.Color(style.Target, style.Vivid)).ToArray();
        spans = intervals.Select(i => (i.Call.Start, i.Call.End, Array.IndexOf(colors, Style(i)))).Distinct().ToArray();
        marks = Marks(intervals);
        Height = 16;
        ClipToBounds = true;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    private static (string Target, bool Vivid) Style(ResourceInterval interval) =>
        (interval.Label.Target, interval.Label.Action is "write" or "exec");
    private static TimelineMark[] Marks(IEnumerable<ResourceInterval> intervals) =>
        intervals.Where(i => Style(i).Vivid).GroupBy(i => i.Call)
            .Select(group => new TimelineMark(group.Key, group.Any(i => i.Label.Action == "write"),
                group.Any(i => i.Label.Action == "exec"))).ToArray();

    private double X(long value) => Math.Clamp((double)(value - range.From) / Math.Max(1, range.To - range.From), 0, 1) * Bounds.Width;
    private CostProjection Projection => new(selection.Trace.CostAxis, selection.FirstResponse, selection.LastResponse, Bounds.Width);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        if (width < 1) return;
        context.FillRectangle(InspectorStyle.Subtle, new Rect(Bounds.Size));
        if (selection.CostAxis) { DrawResponses(context); return; }
        var edges = new List<(double X, int Color, int Change)>(spans.Length * 2);
        foreach (var (start, end, color) in spans)
        {
            var left = Math.Min(X(start), Math.Max(0, width - 2));
            edges.Add((left, color, 1));
            edges.Add((Math.Min(width, Math.Max(left + 2, X(end))), color, -1));
        }
        var active = new int[palette.Length];
        double previous = 0;
        foreach (var edge in edges.OrderBy(e => e.X))
        {
            if (edge.X > previous) DrawColors(context, palette.Where((_, i) => active[i] > 0).ToArray(), previous, edge.X);
            active[edge.Color] += edge.Change;
            previous = edge.X;
        }
        foreach (var mark in marks)
        {
            var markWidth = TimelineCaption.MarkWidth(mark);
            if (width < markWidth) continue;
            DrawMark(context, mark, (X(mark.Call.Start) + X(mark.Call.End)) / 2);
        }
    }

    private void DrawResponses(DrawingContext context)
    {
        var projection = Projection;
        for (var i = 0; i < responses.Length; i++)
        {
            var row = responses[i];
            var response = row.Response;
            var left = projection.At(response.Index);
            var right = projection.At(response.Index + 1);
            var joined = i + 1 < responses.Length && responses[i + 1].Response.Joins(response);
            var width = right - left;
            var fill = joined ? width + 1 : width - Math.Min(1, width / 3);
            DrawColors(context, row.Colors, left, left + fill);
            foreach (var mark in row.Marks)
            {
                var position = Array.IndexOf(row.Calls, mark.Call);
                DrawMark(context, mark, left + width * (position + 0.5) / row.Calls.Length);
            }
            var outline = new Rect(left, 0, Math.Max(1, width), Bounds.Height);
            if (response == focus.Hovered || response == focus.Selected)
                context.DrawRectangle(null, new Pen(UiPalette.Text, 1), outline.Deflate(response.Cost.Usd == null && width > 3 ? 1.5 : 0.5));
            if (response.Cost.Usd == null)
                context.DrawRectangle(null, new Pen(UiPalette.Error, 1), outline.Deflate(0.5));
        }
    }

    private void DrawMark(DrawingContext context, TimelineMark mark, double center)
    {
        var width = TimelineCaption.MarkWidth(mark);
        if (Bounds.Width < width) return;
        var left = Math.Clamp(center - width / 2, 0, Bounds.Width - width);
        TimelineCaption.Draw(context, new Rect(left, 0, width, Bounds.Height), mark);
    }

    private ResourceResponse? Hit(double x)
    {
        var projection = Projection;
        var boundary = projection.Nearest(x);
        var index = x < projection.At(boundary) ? boundary - 1 : boundary;
        var first = 0;
        var last = responses.Length;
        while (first < last)
        {
            var middle = first + (last - first) / 2;
            if (responses[middle].Response.Index == index) return responses[middle];
            if (responses[middle].Response.Index < index) first = middle + 1;
            else last = middle;
        }
        return null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        focus.Changed += InvalidateVisual;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        focus.Changed -= InvalidateVisual;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!selection.CostAxis) return;
        var response = Hit(e.GetPosition(this).X)?.Response;
        focus.Hover(response);
        var tip = response == null ? null : InspectorStyle.CostDetails(response);
        if (!Equals(ToolTip.GetTip(this), tip)) ToolTip.SetTip(this, tip);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (selection.CostAxis) focus.Hover(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!selection.CostAxis || e.KeyModifiers.HasFlag(KeyModifiers.Alt) || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Hit(e.GetPosition(this).X) is not { } row) return;
        selected(new(row.Response, row.Calls[0]));
        e.Handled = true;
    }

    private void DrawColors(DrawingContext context, IBrush[] colors, double left, double right)
    {
        if (colors.Length == 0 || right <= left) return;
        // Draw overlapping target colors and shades at full height.
        var stripe = colors.Length == 1 ? right - left : Math.Min(4, (right - left) / colors.Length);
        for (var i = 0; left + i * stripe < right; i++)
        {
            var x = left + i * stripe;
            context.FillRectangle(colors[i % colors.Length], new Rect(x, 0, Math.Min(stripe, right - x), Bounds.Height));
        }
    }
}

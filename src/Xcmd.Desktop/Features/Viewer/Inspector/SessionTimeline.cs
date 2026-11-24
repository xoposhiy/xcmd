// @implements InspectAgentSession, OpenViewers
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class SessionTimeline : Decorator
{
    private const double Rail = 112;
    private const double Row = 30;
    private const double TrackHeight = 16;
    private const int FirstTrack = 3;
    private SessionActivityTrack[] tracks = [];
    private readonly SessionSelection selection;
    private readonly ResponseFocus focus;
    private readonly Action changed;
    private readonly Action<string?> filter;
    private readonly List<Bar> bars = [];
    private readonly Dictionary<(int Lane, int Index), SessionResponse> responseRows = [];
    private (bool Cost, long From, long To, int First, int Last)? drawnAxis;

    private sealed record Bar(int Lane, long Start, long End, string Title, IBrush Fill,
        SessionCallBlock? Block = null, TimelineCategory[]? Categories = null, SessionResponse? Response = null,
        double Offset = 0, double Share = 1, int Band = 0, int Bands = 1)
    {
        internal string? Agent => Response?.Agent ?? Block?.Calls[0].Agent;
        internal bool Message => Response?.Calls.Count == 0;
    }

    internal SessionCallBlock? SelectedBlock { get; set; }

    internal SessionTimeline(SessionSelection selection, ResponseFocus focus, Action changed, Action<string?> filter)
    {
        this.selection = selection;
        this.focus = focus;
        this.changed = changed;
        this.filter = filter;
        Focusable = true;
        ClipToBounds = true;
        Child = handles;
        startHandle = AddRangeHandle(true);
        endHandle = AddRangeHandle(false);
        SizeChanged += (_, _) => UpdateRangeHandles();
        focus.Changed += InvalidateVisual;
        AutomationProperties.SetName(this, "Session timeline. Use arrow keys to browse events and Enter to select.");
        UpdateBars();
    }

    private int RangeLane => FirstTrack + tracks.Length;
    private static double LaneTop(int lane) => Math.Min(lane, FirstTrack) * Row + Math.Max(0, lane - FirstTrack) * TrackHeight;
    private int LaneAt(double y) => y < FirstTrack * Row ? (int)(y / Row) :
        Math.Min(RangeLane, FirstTrack + (int)((y - FirstTrack * Row) / TrackHeight));
    private SessionActivityTrack? TrackAt(int lane) => lane >= FirstTrack && lane < RangeLane ? tracks[lane - FirstTrack] : null;
    private SessionTrace Trace => selection.Trace;
    private bool CostAxis => selection.CostAxis;
    private double PlotWidth => Math.Max(1, Bounds.Width - Rail - HandleWidth - 2);
    private CostProjection Projection => new(Trace.CostAxis, selection.ZoomFirstResponse, selection.ZoomLastResponse, PlotWidth);
    private double TimeX(long value) => Rail + Math.Clamp((double)(value - selection.ZoomFrom) / Math.Max(1, selection.ZoomTo - selection.ZoomFrom), 0, 1) * PlotWidth;
    private double CostX(int boundary) => Rail + Projection.At(boundary);
    private double RangeX(bool start) => CostAxis
        ? CostX(start ? selection.FirstResponse : selection.LastResponse) : TimeX(start ? selection.From : selection.To);

    private void UpdateBars()
    {
        var axis = (CostAxis, selection.ZoomFrom, selection.ZoomTo, selection.ZoomFirstResponse, selection.ZoomLastResponse);
        if (drawnAxis == axis) return;
        if (drawnAxis?.Cost != CostAxis)
        {
            tracks = SessionActivityTrack.OnAxis(Trace, CostAxis);
            Height = LaneTop(RangeLane) + Row;
            Canvas.SetTop(startHandle, Height - (Row + startHandle.Height) / 2);
            Canvas.SetTop(endHandle, Height - (Row + endHandle.Height) / 2);
        }
        drawnAxis = axis;
        keyboardBar = null;
        bars.Clear();
        responseRows.Clear();
        AddSpans(Trace.Tasks, 1, AccentSurface);
        AddSpans(Trace.Phases, 2, Subtle);
        if (CostAxis) AddResponses();
        else AddCalls();
    }

    private void AddSpans(IReadOnlyList<TraceSpan> spans, int lane, IBrush fill)
    {
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            var from = CostAxis ? Trace.BoundaryAfter(span.Start, inclusive: false) : span.Start;
            var to = CostAxis ? index == spans.Count - 1 ? Trace.Responses.Count : Trace.BoundaryAfter(span.End, inclusive: false) : span.End;
            bars.Add(new(lane, from, to, span.Title, fill));
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(UiPalette.Surface, new Rect(Bounds.Size));
        var line = new Pen(GridLine, 1);
        for (var lane = 0; lane <= RangeLane; lane++) DrawLane(context, line, lane);
        DrawTicks(context, line);
        foreach (var bar in bars) DrawBar(context, bar);
        DrawMessageCaptions(context);
        DrawRange(context);
        foreach (var bar in bars) DrawOverlay(context, bar);
    }

    private void DrawTicks(DrawingContext context, Pen line)
    {
        var ticks = Bounds.Width < 780 ? 4 : 6;
        var projection = Projection;
        var width = CostAxis ? projection.At(selection.ZoomLastResponse) : PlotWidth;
        string? previous = null;
        for (var i = 0; i <= ticks; i++)
        {
            var dx = width * i / ticks;
            var title = CostAxis ? Usd(projection.UsdAt(dx)) :
                Time(Trace, selection.ZoomFrom + (long)((selection.ZoomTo - selection.ZoomFrom) * (double)i / ticks));
            if (title == previous) continue;
            previous = title;
            var x = Rail + dx;
            context.DrawLine(line, new Point(x, Row), new Point(x, Height - Row));
            var label = Formatted(title, UiPalette.Secondary);
            context.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, Rail, Math.Max(Rail, Bounds.Width - label.Width - 8)), 5));
        }
    }

    private void DrawLane(DrawingContext context, Pen line, int lane)
    {
        var top = LaneTop(lane);
        context.DrawLine(line, new Point(0, top), new Point(Bounds.Width, top));
        var track = TrackAt(lane);
        var name = track?.Name ?? (lane switch { 0 => CostAxis ? "API cost" : "Elapsed", 1 => "Tasks", 2 => "Work phases", _ => "Range" });
        var color = track != null && track.Value.Agent == selection.Agent ? Accent : UiPalette.Secondary;
        var showAgent = track is { } row && row.Name != row.Agent;
        var y = top + (track == null ? 6 : 0);
        if (showAgent && TrackAt(lane - 1)?.Agent != track!.Value.Agent) ShortLabel(context, track.Value.Agent, 5, y, 64, color);
        var x = showAgent ? 70 : 10;
        ShortLabel(context, name, x, y, Rail - x - 2, color);
    }

    private bool Muted(Bar bar) => bar.Agent != null && selection.Agent != null && bar.Agent != selection.Agent;

    private void DrawBar(DrawingContext context, Bar bar)
    {
        if (!VisibleBar(bar)) return;
        var rect = BarBounds(bar);
        context.DrawRectangle(Muted(bar) ? GridLine : bar.Fill, bar.Lane == 1 ? new Pen(AccentEdge, 1) : null, rect);
        if (!Muted(bar) && bar.Categories is { Length: > 0 } categories)
        {
            var height = rect.Height / categories.Length;
            for (var i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                context.FillRectangle(Color(category.Target, category.Marks.Length > 0), new Rect(rect.X, rect.Y + height * i, rect.Width, height));
            }
        }
        if (bar.Lane < FirstTrack && rect.Width >= 48)
            ShortLabel(context, bar.Title, rect.X + 5, rect.Y + 1, rect.Width - 10, bar.Lane == 1 ? Accent : UiPalette.Text);
    }

    private void DrawOverlay(DrawingContext context, Bar bar)
    {
        if (!VisibleBar(bar)) return;
        var rect = BarBounds(bar);
        var marks = bar.Categories?.SelectMany(category => category.Marks).ToArray();
        if (!Muted(bar) && marks is { Length: > 0 })
        {
            var mark = new TimelineMark(marks[0].Call, marks.Any(mark => mark.Edit), marks.Any(mark => mark.Run));
            var width = TimelineCaption.MarkWidth(mark);
            var left = Math.Clamp(rect.Center.X - width / 2, Rail, Math.Max(Rail, Rail + PlotWidth - width));
            TimelineCaption.Draw(context, new Rect(left, rect.Y, width, rect.Height), mark);
        }
        if (bar.Offset == 0 && bar.Response is { } response && (response == focus.Hovered || response == focus.Selected))
        {
            var outline = ResponseBounds(bar);
            context.DrawRectangle(null, new Pen(UiPalette.Text, 1), outline.Deflate(response.Cost.Usd == null && outline.Width > 3 ? 1.5 : 0.5));
        }
        else if (bar.Block != null && bar.Block == SelectedBlock)
            context.DrawRectangle(null, new Pen(UiPalette.Text, 1), rect.Deflate(0.5));
        if (bar.Offset == 0 && bar.Response is { Cost.Usd: null })
            context.DrawRectangle(null, new Pen(UiPalette.Error, 1), ResponseBounds(bar).Deflate(0.5));
        if (keyboardBar == bar && IsFocused) context.DrawRectangle(null, new Pen(UiPalette.Focus, 2), rect);
    }

    private Rect ResponseBounds(Bar bar)
    {
        var left = CostX((int)bar.Start);
        return new(left, LaneTop(bar.Lane), Math.Max(1, CostX((int)bar.End) - left), TrackHeight);
    }

    private Rect BarBounds(Bar bar)
    {
        var tool = bar.Agent != null;
        var left = CostAxis ? CostX((int)bar.Start) : TimeX(bar.Start);
        var right = CostAxis ? CostX((int)bar.End) : TimeX(bar.End);
        var width = right - left;
        var start = Math.Min(left + width * bar.Offset, Rail + PlotWidth - 1);
        var gap = 0d;
        if (CostAxis && bar.Offset + bar.Share >= 0.99999)
            gap = JoinsNext(bar) ? -1 : Math.Min(1, width * bar.Share / 3);
        var height = TrackHeight / bar.Bands;
        return new(start, LaneTop(bar.Lane) + (tool ? bar.Band * height : 5),
            Math.Max(CostAxis && tool ? 0.25 : 2, width * bar.Share - gap), tool ? height : Row - 10);
    }

    private void DrawRange(DrawingContext ctx)
    {
        var left = RangeX(true);
        var right = RangeX(false);
        var top = Row;
        var bottom = Height - Row / 2;
        var pen = new Pen(Accent, 1.5);
        ctx.FillRectangle(Brush.Parse("#12BFA7FF"), new Rect(left, top, right - left, bottom - top));
        foreach (var edge in new[] { left, right })
            ctx.DrawLine(pen, new Point(edge, top), new Point(edge, bottom));
        ctx.DrawLine(pen, new Point(left, bottom), new Point(right, bottom));
        if (IsKeyboardFocusWithin) ctx.DrawRectangle(null, new Pen(UiPalette.Focus, 1), new Rect(Bounds.Size).Deflate(1));
    }

    private static FormattedText Formatted(string text, IBrush color) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, color);

    private bool VisibleBar(Bar bar) => CostAxis
        ? bar.End > selection.ZoomFirstResponse && bar.Start < selection.ZoomLastResponse && bar.Start < bar.End
        : bar.End >= selection.ZoomFrom && bar.Start <= selection.ZoomTo;

    private static void ShortLabel(DrawingContext ctx, string text, double x, double y, double width, IBrush color)
    {
        var label = Formatted(text, color);
        label.MaxTextWidth = width;
        label.MaxLineCount = 1;
        label.Trimming = TextTrimming.CharacterEllipsis;
        ctx.DrawText(label, new Point(x, y));
    }
}

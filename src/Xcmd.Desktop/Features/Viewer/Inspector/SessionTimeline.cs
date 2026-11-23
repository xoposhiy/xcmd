// @implements InspectAgentSession, OpenViewers
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class SessionTimeline : Control
{
    private const double Rail = 112;
    private const double Row = 30;
    private readonly SessionSelection selection;
    private readonly List<Bar> bars = [];
    private readonly Action changed;
    private readonly Action<string?> filter;
    private readonly record struct Bar(int Lane, long Start, long End, long TokenStart, long TokenEnd, string Title, IBrush Fill, string? Agent);
    private bool tokens;
    internal bool Tokens
    {
        get => tokens;
        set { tokens = value; InvalidateVisual(); }
    }

    internal SessionTimeline(SessionSelection selection, Action changed, Action<string?> filter)
    {
        this.selection = selection;
        this.changed = changed;
        this.filter = filter;
        Focusable = true;
        ClipToBounds = true;
        Height = Row * (selection.Trace.Agents.Count + 4);
        AutomationProperties.SetName(this, "Session timeline. Use arrow keys to browse events and Enter to select.");
        var trace = selection.Trace;
        tokens = trace.TotalTokens > 0;
        AddSpans(trace.Tasks, 1, AccentSurface);
        AddSpans(trace.Phases, 2, Subtle);
        var lanes = trace.Agents.Select((agent, i) => (agent, lane: i + 3)).ToDictionary(x => x.agent, x => x.lane);
        long edge = 0;
        foreach (var call in trace.Calls)
        {
            bars.Add(new(lanes[call.Agent], call.Start, call.End, edge, edge + call.EstimatedTokens,
                call.Classification?.Summary ?? call.Title, Color(call.Labels.First().Target), call.Agent));
            edge += call.EstimatedTokens;
        }
        void AddSpans(IEnumerable<TraceSpan> spans, int lane, IBrush fill) =>
            bars.AddRange(spans.Select(s => new Bar(lane, s.Start, s.End, trace.TokensAt(s.Start), trace.TokensAt(s.End), s.Title, fill, null)));
    }

    private SessionTrace Trace => selection.Trace;
    private long Axis(long time) => Tokens ? Trace.TokensAt(time) : time;
    private long AxisFrom => Tokens && selection.ZoomFrom == Trace.Start ? 0 : Axis(selection.ZoomFrom);
    private long AxisTo => Axis(selection.ZoomTo);
    private double PlotWidth => Math.Max(1, Bounds.Width - Rail - 12);
    private double X(double value) => Rail + Math.Clamp((value - AxisFrom) / Math.Max(1, AxisTo - AxisFrom), 0, 1) * PlotWidth;
    private double At(long time) => X(time == Trace.Start && Tokens ? 0 : Axis(time));

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(UiPalette.Surface, new Rect(Bounds.Size));
        var line = new Pen(GridLine, 1);
        for (var lane = 0; lane < Height / Row; lane++)
        {
            context.DrawLine(line, new Point(0, lane * Row), new Point(Bounds.Width, lane * Row));
            var name = lane switch
            {
                0 => Tokens ? "Est. tokens" : "Elapsed",
                1 => "Tasks",
                2 => "Work phases",
                _ when lane - 3 < Trace.Agents.Count => Trace.Agents[lane - 3],
                _ => "Range"
            };
            var active = name == selection.Agent;
            ShortLabel(context, name, 10, lane * Row + 6, Rail - 17, active ? Accent : UiPalette.Secondary);
        }
        var ticks = Bounds.Width < 780 ? 4 : 6;
        for (var i = 0; i <= ticks; i++)
        {
            var fraction = (double)i / ticks;
            var x = Rail + fraction * PlotWidth;
            var value = AxisFrom + (long)((AxisTo - AxisFrom) * fraction);
            context.DrawLine(line, new Point(x, Row), new Point(x, Height - Row));
            var title = Tokens ? Number(value) : Time(Trace, value);
            var label = Formatted(title, UiPalette.Secondary);
            context.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, Rail, Math.Max(Rail, Bounds.Width - label.Width - 8)), 5));
        }
        foreach (var bar in bars) DrawBar(context, bar);
        DrawRange(context);
    }

    private void DrawBar(DrawingContext context, Bar bar)
    {
        if (!VisibleBar(bar)) return;
        var rect = BarBounds(bar);
        var fill = bar.Agent != null && selection.Agent != null && bar.Agent != selection.Agent ? GridLine : bar.Fill;
        context.DrawRectangle(fill, bar.Lane == 1 ? new Pen(AccentEdge, 1) : null, rect);
        if (bar.Lane < 3 && rect.Width >= 48) BarCaption(context, bar, rect);
        if (keyboardBar == bar && IsFocused) context.DrawRectangle(null, new Pen(UiPalette.Focus, 2), rect);
    }

    private Rect BarBounds(Bar bar)
    {
        var left = Tokens ? X(bar.TokenStart) : At(bar.Start);
        var right = Tokens ? X(bar.TokenEnd) : At(bar.End);
        return new Rect(Math.Min(left, Bounds.Width - 14), bar.Lane * Row + 5, Math.Max(2, right - left - 1), Row - 10);
    }

    private static void BarCaption(DrawingContext context, Bar bar, Rect rect)
    {
        ShortLabel(context, bar.Title, rect.X + 5, rect.Y + 1, rect.Width - 10, bar.Lane == 1 ? Accent : UiPalette.Text);
    }

    private void DrawRange(DrawingContext ctx)
    {
        var left = At(selection.From);
        var right = At(selection.To);
        var top = Row;
        var bottom = Height - 7;
        var pen = new Pen(Accent, 1.5);
        ctx.FillRectangle(Brush.Parse("#12BFA7FF"), new Rect(left, top, right - left, bottom - top));
        foreach (var edge in new[] { left, right })
        {
            ctx.DrawLine(pen, new Point(edge, top), new Point(edge, bottom));
            ctx.FillRectangle(Accent, new Rect(edge - 3, bottom - 12, 6, 13));
        }
        ctx.DrawLine(pen, new Point(left, bottom), new Point(right, bottom));
        if (IsKeyboardFocusWithin) ctx.DrawRectangle(null, new Pen(UiPalette.Focus, 1), new Rect(Bounds.Size).Deflate(1));
    }

    private static FormattedText Formatted(string text, IBrush color) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, color);
    private bool VisibleBar(Bar bar) => Tokens
        ? bar.TokenEnd > AxisFrom && bar.TokenStart < AxisTo && bar.TokenStart < bar.TokenEnd
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

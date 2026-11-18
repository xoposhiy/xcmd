// @implements OpenViewers, HighlightSource, SyntaxSpans, ReadLargeFiles
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Xcmd.Desktop;

internal sealed class SyntaxSelectionForeground : Control
{
    private readonly SyntaxTextPresenter presenter;
    private Rect? viewport;

    public SyntaxSelectionForeground(SyntaxTextPresenter presenter)
    {
        this.presenter = presenter;
        EffectiveViewportChanged += (_, e) => { viewport = e.EffectiveViewport; InvalidateVisual(); };
    }

    public override void Render(DrawingContext context)
    {
        if (!presenter.ShowSelectionHighlight ||
            presenter.SelectionForegroundBrush is not { } brush || presenter.SelectionStart == presenter.SelectionEnd) return;

        var layout = presenter.TextLayout;
        var positions = presenter.LinePositions;
        var lines = positions.Use(layout);
        var start = Math.Min(presenter.SelectionStart, presenter.SelectionEnd);
        var end = Math.Max(presenter.SelectionStart, presenter.SelectionEnd);
        var firstLine = Math.Max(positions.At(viewport?.Top ?? 0), layout.GetLineIndexFromCharacterIndex(start, false));
        for (var index = firstLine; index < lines.Count; index++)
        {
            var line = lines[index];
            var top = positions.Top(index);
            if (top >= viewport?.Bottom || line.FirstTextSourceIndex >= end) break;
            var from = Math.Max(start, line.FirstTextSourceIndex);
            var to = Math.Min(end, line.FirstTextSourceIndex + line.Length);
            foreach (var bounds in line.GetTextBounds(from, to - from))
                DrawSelectedRuns(context, line, top, bounds.Rectangle.Translate(new Vector(0, top)), brush);
        }
    }

    private static void DrawSelectedRuns(DrawingContext context, TextLine line, double top, Rect bounds, IBrush brush)
    {
        var x = line.Start;
        using var clip = context.PushClip(bounds);
        foreach (var run in line.TextRuns)
        {
            if (run is not DrawableTextRun drawable) continue;
            if (x >= bounds.Right) break;
            if (x + drawable.Size.Width > bounds.Left && run is ShapedTextRun shaped)
            {
                using var offset = context.PushTransform(Matrix.CreateTranslation(x, top + line.Baseline - shaped.Baseline));
                context.DrawGlyphRun(brush, shaped.GlyphRun);
            }
            x += drawable.Size.Width;
        }
    }
}

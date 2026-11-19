// @implements OpenViewers, HighlightSource, SyntaxSpans, ReadLargeFiles, ReadWorkingFileDiff
using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer;

namespace Xcmd.Desktop.Features.Viewer.Syntax;

internal sealed class SyntaxTextPresenter : TextPresenter
{
    private Size constraint;
    internal ViewerTextLines LinePositions { get; } = new();
    private SyntaxColorRuns? runs;
    private IReadOnlyDictionary<SyntaxKind, IBrush> colors = SyntaxPalette.Create(ReaderThemes.Default);
    private readonly TextRunCache shapedRuns = new();
    private readonly SyntaxSelectionForeground selectionLayer;
    private bool repaintSelection;

    public SyntaxTextPresenter()
    {
        selectionLayer = new SyntaxSelectionForeground(this) { IsHitTestVisible = false }.WithFeedbackId("SyntaxTextBox.selectionForeground");
        VisualChildren.Add(selectionLayer);
    }

    public void SetSyntax(SyntaxDocument? value, IReadOnlyDictionary<SyntaxKind, IBrush> palette)
    {
        runs = value is { Spans.Count: > 0 } ? new SyntaxColorRuns(value) : null;
        colors = palette;
        InvalidateTextLayout();
    }

    protected override void InvalidateTextLayout()
    {
        selectionLayer?.InvalidateVisual();
        if (repaintSelection)
        {
            InvalidateVisual();
            return;
        }
        shapedRuns.Invalidate();
        base.InvalidateTextLayout();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // Selection uses the existing glyph positions. The base presenter still draws its background and caret.
        var previous = repaintSelection;
        repaintSelection = change.Property == SelectionStartProperty || change.Property == SelectionEndProperty;
        try { base.OnPropertyChanged(change); }
        finally { repaintSelection = previous; }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        shapedRuns.Invalidate();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        constraint = availableSize;
        selectionLayer.Measure(availableSize);
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (finalSize.Width != constraint.Width)
            constraint = new Size(Math.Ceiling(finalSize.Width), double.PositiveInfinity);
        var size = base.ArrangeOverride(finalSize);
        selectionLayer.Arrange(new Rect(size));
        selectionLayer.InvalidateVisual();
        return size;
    }

    protected override TextLayout CreateTextLayout()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var width = constraint.Width > 0 ? constraint.Width : double.PositiveInfinity;
        var height = constraint.Height > 0 ? constraint.Height : double.PositiveInfinity;
        if (runs == null || runs.Text != Text)
            return new TextLayout(Text, typeface, FontSize, Foreground, TextAlignment, TextWrapping,
                flowDirection: FlowDirection, maxWidth: width, maxHeight: height, lineHeight: LineHeight,
                letterSpacing: LetterSpacing, fontFeatures: FontFeatures, textRunCache: shapedRuns);
        TextRunProperties Style(IBrush? brush) => new GenericTextRunProperties(typeface, FontSize,
            foregroundBrush: brush, fontFeatures: FontFeatures);
        var styles = colors.ToDictionary(pair => pair.Key, pair => Style(pair.Value));
        var source = new SyntaxTextSource(runs, styles);
        var paragraph = new GenericTextParagraphProperties(FlowDirection, TextAlignment, true, false,
            styles[SyntaxKind.Plain], TextWrapping, LineHeight, 0, LetterSpacing);
        return new TextLayout(source, paragraph, maxWidth: width, maxHeight: height, textRunCache: shapedRuns);
    }
}

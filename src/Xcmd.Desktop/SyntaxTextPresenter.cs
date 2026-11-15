using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class SyntaxTextPresenter : TextPresenter
{
    private Size constraint;
    private SyntaxDocument? document;
    private IReadOnlyDictionary<SyntaxKind, IBrush> colors = SyntaxPalette.Create(ReaderThemes.Default);

    public void SetSyntax(SyntaxDocument? value, IReadOnlyDictionary<SyntaxKind, IBrush> palette)
    {
        document = value; colors = palette;
        InvalidateTextLayout();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        constraint = availableSize;
        return base.MeasureOverride(availableSize);
    }

    protected override TextLayout CreateTextLayout()
    {
        if (document == null || document.Text != Text || document.Spans.Count == 0) return base.CreateTextLayout();
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        return new TextLayout(Text, typeface, FontSize, Foreground, TextAlignment, TextWrapping,
            flowDirection: FlowDirection, maxWidth: constraint.Width > 0 ? constraint.Width : double.PositiveInfinity,
            maxHeight: constraint.Height > 0 ? constraint.Height : double.PositiveInfinity,
            lineHeight: LineHeight, letterSpacing: LetterSpacing, fontFeatures: FontFeatures,
            textStyleOverrides: BuildStyles(document, typeface));
    }

    private List<ValueSpan<TextRunProperties>> BuildStyles(SyntaxDocument source, Typeface typeface)
    {
        var start = Math.Min(SelectionStart, SelectionEnd);
        var end = Math.Max(SelectionStart, SelectionEnd);
        var bounds = source.Spans.SelectMany(span => new[] { span.Start, span.Start + span.Length })
            .Concat([0, source.Text.Length, start, end]).Distinct().Order().ToArray();
        var result = new List<ValueSpan<TextRunProperties>>();
        var token = 0;
        for (var i = 0; i + 1 < bounds.Length; i++)
        {
            var position = bounds[i];
            while (token < source.Spans.Count && source.Spans[token].Start + source.Spans[token].Length <= position) token++;
            var kind = token < source.Spans.Count && source.Spans[token].Start <= position ? source.Spans[token].Kind : SyntaxKind.Plain;
            var brush = ShowSelectionHighlight && position >= start && position < end && SelectionForegroundBrush != null
                ? SelectionForegroundBrush : colors[kind];
            result.Add(new(position, bounds[i + 1] - position, new GenericTextRunProperties(typeface, FontSize,
                foregroundBrush: brush, fontFeatures: FontFeatures)));
        }
        return result;
    }
}

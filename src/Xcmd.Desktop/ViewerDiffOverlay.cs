// @implements ReadWorkingFileDiff
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class ViewerDiffOverlay : Control
{
    public const double GutterWidth = 28;
    private ViewerDiffDisplay? display;
    private SyntaxTextPresenter? presenter;
    private int pageStart;
    private int pageLength;
    private IBrush stagedAddition = Brushes.Transparent;
    private IBrush stagedDeletion = Brushes.Transparent;
    private IBrush unstagedAddition = Brushes.Transparent;
    private IBrush unstagedDeletion = Brushes.Transparent;
    private IBrush inlineAddition = Brushes.Transparent;
    private IBrush inlineDeletion = Brushes.Transparent;
    private FormattedText? plus;
    private FormattedText? minus;
    private (FontFamily Family, double Size) signFont;
    public IBrush Foreground { get; private set; } = Brushes.White;
    public IReadOnlyList<Color> HighlightBackgrounds { get; private set; } = [];
    private IDisposable? hoverTimer;
    private ViewerDiffKind hoverKind;

    public ViewerDiffOverlay() { IsHitTestVisible = false; ClipToBounds = true; }

    public void Update(ViewerDiffDisplay? document, TextPresenter? textPresenter, int start, int length)
    {
        display = document; presenter = textPresenter as SyntaxTextPresenter; pageStart = start; pageLength = length;
        InvalidateVisual();
    }

    public void ApplyTheme(ReaderTheme theme)
    {
        var background = Color.Parse(theme.Background);
        var text = Color.Parse(theme.Foreground);
        var green = Color.FromRgb(35, 146, 72);
        var red = Color.FromRgb(216, 55, 64);
        var strong = theme.IsDark ? 0.44 : 0.34;
        var soft = theme.IsDark ? 0.25 : 0.16;
        var strongGreen = Blend(background, green, strong);
        var strongRed = Blend(background, red, strong);
        var softGreen = Blend(background, green, soft);
        var softRed = Blend(background, red, soft);
        var inlineGreen = Blend(background, green, theme.IsDark ? 0.67 : 0.48);
        var inlineRed = Blend(background, red, theme.IsDark ? 0.67 : 0.48);
        stagedAddition = new SolidColorBrush(strongGreen);
        stagedDeletion = new SolidColorBrush(strongRed);
        unstagedAddition = new SolidColorBrush(softGreen);
        unstagedDeletion = new SolidColorBrush(softRed);
        inlineAddition = new SolidColorBrush(inlineGreen);
        inlineDeletion = new SolidColorBrush(inlineRed);
        HighlightBackgrounds = [strongGreen, strongRed, softGreen, softRed, inlineGreen, inlineRed];
        Foreground = HighlightBackgrounds.All(color => UiContrast.Ratio(text, color) >= 4.5)
            ? new SolidColorBrush(text) : theme.IsDark ? Brushes.White : Brushes.Black;
        plus = minus = null;
        InvalidateVisual();
    }

    private static Color Blend(Color background, Color tint, double strength) => Color.FromRgb(
        (byte)(background.R + (tint.R - background.R) * strength),
        (byte)(background.G + (tint.G - background.G) * strength),
        (byte)(background.B + (tint.B - background.B) * strength));

    public override void Render(DrawingContext context)
    {
        if (display == null || presenter == null || pageLength == 0 ||
            presenter.TranslatePoint(default, this) is not { } origin) return;
        var textLines = presenter.LinePositions;
        var lines = textLines.Use(presenter.TextLayout);
        PrepareSigns(presenter);
        for (var index = textLines.At(-origin.Y); index < lines.Count; index++)
        {
            var line = lines[index];
            var y = origin.Y + textLines.Top(index);
            if (y >= Bounds.Height || line.FirstTextSourceIndex >= pageLength) break;
            var position = pageStart + line.FirstTextSourceIndex;
            var rowIndex = display.RowAt(position);
            var row = display.Row(rowIndex);
            if (row.Kind == ViewerDiffKind.Context) continue;
            var added = IsAddition(row.Kind);
            context.FillRectangle(ColorFor(row.Kind), new Rect(0, y, Bounds.Width, line.Height));
            DrawEdits(context, line, display.Edits(rowIndex), row.Start - pageStart, new Point(origin.X, y), added ? inlineAddition : inlineDeletion);
            if (row.Start == position && y >= 0) context.DrawText(added ? plus! : minus!, new Point(9, y));
        }
    }

    private static void DrawEdits(DrawingContext context, TextLine line, ViewerDiffEdit[] edits, int rowStart, Point origin, IBrush brush)
    {
        var lineStart = line.FirstTextSourceIndex;
        var lineEnd = lineStart + line.Length;
        foreach (var edit in edits)
        {
            var start = Math.Max(lineStart, rowStart + edit.Start);
            var end = Math.Min(lineEnd, rowStart + edit.Start + edit.Length);
            if (start >= end) continue;
            foreach (var bound in line.GetTextBounds(start, end - start))
            {
                var rect = bound.Rectangle.Translate(new Vector(origin.X, origin.Y));
                if (rect.Right > GutterWidth)
                    context.FillRectangle(brush, rect.Intersect(new Rect(GutterWidth, rect.Y, rect.Right - GutterWidth, rect.Height)));
            }
        }
    }

    private void PrepareSigns(TextPresenter textPresenter)
    {
        var font = (textPresenter.FontFamily, textPresenter.FontSize);
        if (plus != null && signFont == font) return;
        signFont = font;
        var typeface = new Typeface(font.FontFamily);
        plus = new FormattedText("+", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, font.FontSize, Foreground);
        minus = new FormattedText("−", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, font.FontSize, Foreground);
    }

    private IBrush ColorFor(ViewerDiffKind kind) => kind switch
    {
        ViewerDiffKind.StagedAddition => stagedAddition,
        ViewerDiffKind.StagedDeletion => stagedDeletion,
        ViewerDiffKind.UnstagedAddition or ViewerDiffKind.Addition => unstagedAddition,
        _ => unstagedDeletion
    };

    private static bool IsAddition(ViewerDiffKind kind) =>
        kind is ViewerDiffKind.StagedAddition or ViewerDiffKind.UnstagedAddition or ViewerDiffKind.Addition;

    public void UpdateHint(Control target, PointerEventArgs e)
    {
        if (!IsVisible || display == null || presenter == null) return;
        var point = e.GetPosition(presenter);
        var textLines = presenter.LinePositions;
        var lines = textLines.Use(presenter.TextLayout);
        var row = lines.Count == 0 ? -1 : display.RowAt(pageStart + lines[textLines.At(point.Y)].FirstTextSourceIndex);
        var kind = row < 0 || point.Y < 0 || point.Y >= presenter.TextLayout.Height ? ViewerDiffKind.Context : display.Row(row).Kind;
        if (hoverKind == kind) return;
        ClearHint(target);
        hoverKind = kind;
        var hint = kind switch
        {
            ViewerDiffKind.StagedAddition => "Staged addition",
            ViewerDiffKind.StagedDeletion => "Staged deletion",
            ViewerDiffKind.UnstagedAddition => "Unstaged addition",
            ViewerDiffKind.UnstagedDeletion => "Unstaged deletion",
            ViewerDiffKind.Addition => "Working copy addition",
            ViewerDiffKind.Deletion => "Working copy deletion",
            _ => null
        };
        if (hint == null) return;
        hoverTimer = DispatcherTimer.RunOnce(() =>
        {
            ToolTip.SetTip(target, hint);
            ToolTip.SetIsOpen(target, true);
        }, TimeSpan.FromMilliseconds(400));
    }

    public void ClearHint(Control target)
    {
        hoverTimer?.Dispose();
        hoverTimer = null;
        if (hoverKind == ViewerDiffKind.Context) return;
        hoverKind = ViewerDiffKind.Context;
        ToolTip.SetIsOpen(target, false);
        ToolTip.SetTip(target, null);
    }
}

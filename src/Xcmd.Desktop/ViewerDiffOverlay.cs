// @implements ReadWorkingFileDiff
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class ViewerDiffOverlay : Control
{
    public const double GutterWidth = 28;
    private ViewerDiffDisplay? display;
    private TextPresenter? presenter;
    private int pageStart;
    private int pageLength;
    private IBrush stagedAddition = Brushes.Transparent;
    private IBrush stagedDeletion = Brushes.Transparent;
    private IBrush unstagedAddition = Brushes.Transparent;
    private IBrush unstagedDeletion = Brushes.Transparent;
    public IBrush Foreground { get; private set; } = Brushes.White;
    public IReadOnlyList<Color> HighlightBackgrounds { get; private set; } = [];
    private IDisposable? hoverTimer;
    private ViewerDiffKind hoverKind;

    public ViewerDiffOverlay() { IsHitTestVisible = false; ClipToBounds = true; }

    public void Update(ViewerDiffDisplay? document, TextPresenter? textPresenter, int start, int length)
    {
        display = document; presenter = textPresenter; pageStart = start; pageLength = length;
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
        stagedAddition = new SolidColorBrush(strongGreen);
        stagedDeletion = new SolidColorBrush(strongRed);
        unstagedAddition = new SolidColorBrush(softGreen);
        unstagedDeletion = new SolidColorBrush(softRed);
        HighlightBackgrounds = [strongGreen, strongRed, softGreen, softRed];
        Foreground = UiContrast.Ratio(text, strongGreen) >= 4.5 && UiContrast.Ratio(text, strongRed) >= 4.5
            ? new SolidColorBrush(text) : theme.IsDark ? Brushes.White : Brushes.Black;
        InvalidateVisual();
    }

    private static Color Blend(Color background, Color tint, double strength) => Color.FromRgb(
        (byte)(background.R + (tint.R - background.R) * strength),
        (byte)(background.G + (tint.G - background.G) * strength),
        (byte)(background.B + (tint.B - background.B) * strength));

    public override void Render(DrawingContext context)
    {
        if (display == null || presenter == null || pageLength == 0 || presenter.TranslatePoint(default, this) is not { } origin) return;
        var layout = presenter.TextLayout;
        var visible = layout.HitTestPoint(new Point(0, Math.Max(0, -origin.Y))).TextPosition;
        var first = Math.Max(0, display.RowAt(pageStart + visible));
        for (var index = first; index < display.RowCount; index++)
        {
            var row = display.Row(index);
            if (row.Start >= pageStart + pageLength) break;
            if (row.Kind == ViewerDiffKind.Context) continue;
            var top = layout.HitTestTextPosition(Math.Max(0, row.Start - pageStart));
            var bottom = layout.HitTestTextPosition(Math.Min(pageLength - 1, row.Last - pageStart));
            var y = origin.Y + top.Y;
            if (y >= Bounds.Height) break;
            if (origin.Y + bottom.Bottom <= 0) continue;
            context.FillRectangle(ColorFor(row.Kind), new Rect(0, y, Bounds.Width, bottom.Bottom - top.Y));
            if (row.Start >= pageStart && y >= 0)
            {
                var sign = new FormattedText(IsAddition(row.Kind) ? "+" : "−", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(presenter.FontFamily), presenter.FontSize, Foreground);
                context.DrawText(sign, new Point(9, y));
            }
        }
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
        var hit = presenter.TextLayout.HitTestPoint(point);
        var row = display.RowAt(pageStart + hit.TextPosition);
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

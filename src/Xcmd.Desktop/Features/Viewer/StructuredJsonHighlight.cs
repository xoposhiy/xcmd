// @implements ChooseViewMode
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed class StructuredJsonHighlight : Control
{
    private TextPresenter? presenter;
    private int start;
    private int length;
    private IBrush brush = Brushes.Transparent;

    public StructuredJsonHighlight() { IsHitTestVisible = false; ClipToBounds = true; IsVisible = false; }

    public void Update(TextPresenter? text, int first, int count, IBrush color)
    {
        presenter = text;
        start = first;
        length = count;
        brush = color;
        IsVisible = text != null && count > 0;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (presenter?.TranslatePoint(default, this) is not { } origin || start + length > presenter.Text?.Length) return;
        foreach (var bounds in presenter.TextLayout.HitTestTextRange(start, length))
            context.FillRectangle(brush, new Rect(origin.X + bounds.X, origin.Y + bounds.Bottom - 1, bounds.Width, 1));
    }
}

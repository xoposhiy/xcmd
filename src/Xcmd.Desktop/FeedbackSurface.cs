using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Xcmd.Desktop;

internal sealed class FeedbackSurface
{
    private readonly Canvas overlay = new() { IsHitTestVisible = false };
    private readonly Border highlight = new() { BorderBrush = Brushes.Gold, BorderThickness = new Thickness(2), IsVisible = false };
    private readonly FeedbackController controller;
    private readonly Window window;
    private Control? pointed;
    public bool IsPicking { get; private set; }

    public FeedbackSurface(FeedbackController controller, Window window, Grid root)
    {
        this.controller = controller; this.window = window;
        overlay.Children.Add(highlight); root.Children.Add(overlay);
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F12) { if (IsPicking) Cancel(); else Begin(); e.Handled = true; }
            else if (IsPicking) { if (e.Key == Key.Escape) Cancel(); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        { if (IsPicking) Highlight(Pick(e.GetPosition(window))); }, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!IsPicking) return;
            pointed = Pick(e.GetPosition(window)); e.Handled = true;
        }, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (!IsPicking) return;
            e.Handled = true;
            if (pointed is not { } selected) return;
            IsPicking = false; Highlight(selected); controller.OpenComment(window, selected, this);
        }, RoutingStrategies.Tunnel);
    }

    private Control? Pick(Point point) => FeedbackController.Target(window.GetVisualAt(point, visual => visual != overlay) as Control);

    public void Begin()
    {
        controller.StopNameSearch();
        IsPicking = true;
        controller.SetHint("Click an element to comment. Esc cancels.");
    }

    public void Cancel()
    {
        IsPicking = false; pointed = null; highlight.IsVisible = false; controller.SetHint("");
    }

    private void Highlight(Control? control)
    {
        if (control?.TranslatePoint(default, overlay) is not { } point) return;
        Canvas.SetLeft(highlight, point.X); Canvas.SetTop(highlight, point.Y);
        highlight.Width = control.Bounds.Width; highlight.Height = control.Bounds.Height; highlight.IsVisible = true;
    }
}

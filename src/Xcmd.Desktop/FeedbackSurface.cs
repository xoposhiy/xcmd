using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class FeedbackSurface
{
    private readonly Canvas overlay = new Canvas() { IsHitTestVisible = false }.WithFeedbackId("FeedbackSurface.overlay");
    private readonly Border highlight = new Border() { BorderBrush = Brushes.Gold, BorderThickness = new Thickness(2), IsVisible = false }.WithFeedbackId("FeedbackSurface.highlight");
    private readonly FeedbackController controller;
    private readonly Window window;
    private Control? pointed;
    private bool passingClick;
    private readonly List<FeedbackInteraction> interactions = [];
    public bool IsPicking { get; private set; }

    public FeedbackSurface(FeedbackController controller, Window window, Grid root)
    {
        this.controller = controller; this.window = window;
        overlay.Children.Add(highlight); root.Children.Add(overlay);
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F12 && e.KeyModifiers == KeyModifiers.None) { if (IsPicking) Cancel(); else controller.Begin(window); e.Handled = true; }
            else if (IsPicking) { if (e.Key == Key.Escape) Cancel(); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        { if (IsPicking) Highlight(Pick(e.GetPosition(window))); }, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        window.Closed += (_, _) => Cancel();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsPicking || passingClick) return;
        var selected = Pick(e.GetPosition(window));
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && selected?.IsEffectivelyEnabled == true && e.Source is InputElement source)
        {
            interactions.Add(FeedbackController.DescribeInteraction(selected, "Click"));
            passingClick = true;
            source.RaiseEvent(new PointerPressedEventArgs(e.Source, e.Pointer, window, e.GetPosition(window), e.Timestamp,
                e.GetCurrentPoint(window).Properties, e.KeyModifiers & ~KeyModifiers.Control, 1));
            e.Handled = true;
            return;
        }
        pointed = selected; e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (passingClick) { passingClick = false; return; }
        if (!IsPicking) return;
        e.Handled = true;
        if (pointed is not { } selected) return;
        if (!window.IsVisible || Pick(e.GetPosition(window)) != selected) return;
        IsPicking = false; Highlight(selected);
        controller.OpenComment(window, selected, this, interactions.ToArray());
    }

    private Control? Pick(Point point) => FeedbackController.Target(window.GetVisualAt(point, visual => visual != overlay) as Control);

    public void Begin()
    {
        controller.StopNameSearch();
        interactions.Clear(); passingClick = false;
        IsPicking = true;
        controller.SetHint(window, "Click to comment. Ctrl+click to use a control. Esc cancels.");
    }

    public void Cancel()
    {
        var wasPicking = IsPicking || interactions.Count > 0;
        IsPicking = false; passingClick = false; pointed = null; interactions.Clear(); highlight.IsVisible = false; controller.SetHint(window, "");
        if (wasPicking) foreach (var menu in window.GetVisualDescendants().OfType<Menu>().ToArray()) menu.Close();
    }

    private void Highlight(Control? control)
    {
        if (control?.TranslatePoint(default, overlay) is not { } point) return;
        Canvas.SetLeft(highlight, point.X); Canvas.SetTop(highlight, point.Y);
        highlight.Width = control.Bounds.Width; highlight.Height = control.Bounds.Height; highlight.IsVisible = true;
    }
}

// @implements ReportControl
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Feedback;

internal sealed class FeedbackSurface
{
    private const KeyModifiers PassModifiers = KeyModifiers.Alt | KeyModifiers.Control;
    private static readonly AttachedProperty<FeedbackSurface?> SurfaceProperty =
        AvaloniaProperty.RegisterAttached<Window, FeedbackSurface?>("FeedbackSurface", typeof(FeedbackSurface));
    private readonly Canvas overlay = new Canvas { IsHitTestVisible = false, ZIndex = int.MaxValue }.WithFeedbackId("FeedbackSurface.overlay");
    private readonly Border highlight = new Border() { BorderBrush = Brushes.Gold, BorderThickness = new Thickness(2), IsVisible = false }.WithFeedbackId("FeedbackSurface.highlight");
    private readonly FeedbackController controller;
    private readonly Window window;
    private readonly Grid root;
    private readonly Action<string> setHint;
    public Action<Control?> RestoreFocus { get; }
    private Control? hovered;
    private Control? pointed;
    private PointerEventArgs? lastPointerEvent;
    private bool forwarding;
    private bool passingClick;
    private readonly List<FeedbackInteraction> interactions = [];
    public bool IsPicking { get; private set; }

    static FeedbackSurface()
    {
        RegisterPointerHandlers(InputElement.PointerMovedEvent);
        RegisterPointerHandlers(InputElement.PointerPressedEvent);
        RegisterPointerHandlers(InputElement.PointerReleasedEvent);
        InputElement.KeyDownEvent.AddClassHandler<Window>((window, e) => window.GetValue(SurfaceProperty)?.OnKeyDown(e), RoutingStrategies.Tunnel);
        InputElement.KeyDownEvent.AddClassHandler<InputElement>((source, e) => ForSource(source, e)?.OnKeyDown(e), RoutingStrategies.Tunnel);
    }

    private static void RegisterPointerHandlers<T>(RoutedEvent<T> routedEvent) where T : PointerEventArgs
    {
        routedEvent.AddClassHandler<Window>((window, e) => window.GetValue(SurfaceProperty)?.OnPointerEvent(e), RoutingStrategies.Tunnel);
        // Popup routes can stop before reaching the window. Use the source's visual top level.
        routedEvent.AddClassHandler<InputElement>((source, e) => ForSource(source, e)?.OnPointerEvent(e), RoutingStrategies.Tunnel);
    }

    public FeedbackSurface(FeedbackController controller, Window window, Grid root, Action<string> setHint, Action<Control?> restoreFocus)
    {
        this.controller = controller; this.window = window; this.root = root;
        this.setHint = setHint; RestoreFocus = restoreFocus;
        overlay.Children.Add(highlight);
        window.SetValue(SurfaceProperty, this);
        window.PointerExited += (_, _) => Highlight(null);
        window.Closed += (_, _) => Cancel();
    }

    private static FeedbackSurface? ForSource(InputElement source, RoutedEventArgs e) =>
        source == e.Source && TopLevel.GetTopLevel(source) is Window window ? window.GetValue(SurfaceProperty) : null;

    private void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !window.IsEnabled) return;
        if (e.Key == Key.F12 && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            if (IsPicking) Cancel();
            else controller.Begin(window);
        }
        else if (IsPicking)
        {
            e.Handled = true;
            if (e.Key == Key.Escape) Cancel();
        }
    }

    private void OnPointerEvent(PointerEventArgs e)
    {
        if (!IsPicking || forwarding || lastPointerEvent == e) return;
        lastPointerEvent = e;
        switch (e)
        {
            case PointerPressedEventArgs pressed: OnPointerPressed(pressed); break;
            case PointerReleasedEventArgs released: OnPointerReleased(released); break;
            default: Highlight(Pick(e.GetPosition(window))); break;
        }
    }

    private void OnPointerPressed(PointerPressedEventArgs e)
    {
        var position = e.GetPosition(window);
        var target = Pick(position);
        var properties = e.GetCurrentPoint(window).Properties;
        if ((e.KeyModifiers & PassModifiers) != 0 && target?.IsEffectivelyEnabled == true && e.Source is InputElement source &&
            properties.PointerUpdateKind is PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.RightButtonPressed)
        {
            interactions.Add(FeedbackController.DescribeInteraction(target, "Click"));
            Highlight(null);
            passingClick = true;
            Forward(source, new PointerPressedEventArgs(source, e.Pointer, window, position, e.Timestamp, properties, e.KeyModifiers & ~PassModifiers, 1));
        }
        else pointed = target;
        e.Handled = true;
    }

    private void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (passingClick)
        {
            passingClick = false;
            if ((e.KeyModifiers & PassModifiers) != 0 && e.Source is InputElement source)
            {
                Forward(source, new PointerReleasedEventArgs(source, e.Pointer, window, e.GetPosition(window), e.Timestamp,
                    e.GetCurrentPoint(window).Properties, e.KeyModifiers & ~PassModifiers, e.InitialPressMouseButton));
                e.Handled = true;
            }
            return;
        }
        e.Handled = true;
        var selected = pointed;
        pointed = null;
        if (selected == null) return;
        if (!window.IsVisible || Pick(e.GetPosition(window)) != selected) return;
        StopPicking();
        controller.OpenComment(window, selected, this, interactions.ToArray());
    }

    private void Forward(InputElement source, PointerEventArgs e)
    {
        forwarding = true;
        try { source.RaiseEvent(e); }
        finally { forwarding = false; }
    }

    private Control? Pick(Point point)
    {
        var hit = window.GetVisualsAt(point, visual => visual.IsVisible && visual != overlay)
            .OfType<Control>().FirstOrDefault(control => control == root || root.IsVisualAncestorOf(control) ||
                control.FindAncestorOfType<OverlayPopupHost>() != null);
        return FeedbackController.Target(hit);
    }

    public void Begin()
    {
        controller.StopNameSearch();
        interactions.Clear();
        IsPicking = true;
        setHint("Click to comment · Ctrl+click or Alt+click to use · Esc cancels");
    }

    public void Cancel()
    {
        var wasPicking = IsPicking || interactions.Count > 0;
        StopPicking();
        passingClick = false; pointed = null; interactions.Clear(); setHint("");
        if (wasPicking) ClosePopups();
    }

    public void ClosePopups()
    {
        var popups = window.GetVisualDescendants().OfType<OverlayPopupHost>().Select(host => host.Parent).OfType<Popup>().ToArray();
        foreach (var popup in popups.Reverse()) popup.IsOpen = false;
        foreach (var menu in window.GetVisualDescendants().OfType<Menu>().ToArray()) menu.Close();
    }

    private void StopPicking()
    {
        IsPicking = false;
        lastPointerEvent = null;
        Highlight(null);
    }

    private void Highlight(Control? control)
    {
        if (hovered != control)
        {
            if (hovered != null) hovered.DetachedFromVisualTree -= ClearDetachedHighlight;
            hovered = control;
            if (hovered != null) hovered.DetachedFromVisualTree += ClearDetachedHighlight;
        }
        if (control == null)
        {
            highlight.IsVisible = false;
            if (overlay.Parent is Panel host) host.Children.Remove(overlay);
            return;
        }

        // Popup hosts share a layer above the window's regular overlays.
        var layer = control.FindAncestorOfType<OverlayPopupHost>()?.GetVisualParent() as Panel
            ?? (Panel?)OverlayLayer.GetOverlayLayer(root) ?? root;
        if (overlay.Parent != layer)
        {
            if (overlay.Parent is Panel previousLayer) previousLayer.Children.Remove(overlay);
            layer.Children.Add(overlay);
        }
        if (control.TranslatePoint(default, overlay) is not { } point)
        {
            highlight.IsVisible = false;
            return;
        }
        Canvas.SetLeft(highlight, point.X); Canvas.SetTop(highlight, point.Y);
        highlight.Width = control.Bounds.Width; highlight.Height = control.Bounds.Height; highlight.IsVisible = true;
    }

    private void ClearDetachedHighlight(object? sender, VisualTreeAttachmentEventArgs e) => Highlight(null);
}

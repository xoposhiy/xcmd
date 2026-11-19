// @implements RestoreWorkspace, OpenViewers
using Avalonia;
using Avalonia.Controls;
using Xcmd.Core;

namespace Xcmd.Desktop.Shared.Ui;

internal sealed class WorkspaceWindowPlacement(Window window)
{
    private WindowPlacement? normalPlacement;
    private WindowState lastVisibleState;

    public WindowPlacement Capture()
    {
        var placement = window.WindowState == WindowState.Normal || normalPlacement == null ? CurrentPlacement() : normalPlacement;
        return placement with
        {
            Maximized = window.WindowState == WindowState.Maximized || window.WindowState == WindowState.Minimized && lastVisibleState == WindowState.Maximized,
            Minimized = window.WindowState == WindowState.Minimized,
            Active = window.IsActive
        };
    }

    public void Track()
    {
        void Remember()
        {
            if (window.WindowState == WindowState.Normal) normalPlacement = CurrentPlacement();
        }
        Remember();
        window.PositionChanged += (_, _) => Remember();
        window.SizeChanged += (_, _) => Remember();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty && window.WindowState != WindowState.Minimized)
                lastVisibleState = window.WindowState;
        };
    }

    public void Restore(WindowPlacement placement)
    {
        window.Width = Math.Clamp(placement.Width, window.MinWidth, 20000);
        window.Height = Math.Clamp(placement.Height, window.MinHeight, 20000);
        var position = new PixelPoint(placement.X, placement.Y);
        if (window.Screens.ScreenFromPoint(position) == null && window.Screens.Primary is { } primary)
            position = primary.WorkingArea.Position;
        window.Position = position;
        normalPlacement = placement;
        window.WindowState = placement.Maximized ? WindowState.Maximized : WindowState.Normal;
        if (placement.Minimized) window.WindowState = WindowState.Minimized;
        window.ShowActivated = placement.Active && !placement.Minimized;
    }

    private WindowPlacement CurrentPlacement() => new(window.Position.X, window.Position.Y, window.Width, window.Height, false);
}

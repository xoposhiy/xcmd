using Avalonia;
using Avalonia.Controls;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    internal void RestoreViewerTheme(string? id) => viewing.RestoreTheme(id);
    private WindowPlacement? normalPlacement;
    private WorkspaceState? restoredState;
    private WindowState lastVisibleWindowState;

    public WorkspaceState CaptureWorkspace()
    {
        var placement = WindowState == WindowState.Normal || normalPlacement == null
            ? new WindowPlacement(Position.X, Position.Y, Width, Height, false) : normalPlacement;
        return new(1, Left.CaptureState(), Right.CaptureState(), ActivePanel.Id,
            placement with
            {
                Maximized = WindowState == WindowState.Maximized || WindowState == WindowState.Minimized && lastVisibleWindowState == WindowState.Maximized,
                Minimized = WindowState == WindowState.Minimized,
                Active = IsActive
            }, null, DirectoryUsage.Capture(), Favorites.Items.ToArray(), UsageVersion: 2, CommandLine.Capture(), ViewerThemeId, FileSearch.Capture());
    }

    public void TrackPlacement()
    {
        void Remember()
        {
            if (WindowState == WindowState.Normal) normalPlacement = new(Position.X, Position.Y, Width, Height, false);
        }
        Remember();
        PositionChanged += (_, _) => Remember();
        SizeChanged += (_, _) => Remember();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && WindowState != WindowState.Minimized) lastVisibleWindowState = WindowState;
        };
    }

    public async Task RestoreWorkspaceAsync(WorkspaceState state)
    {
        state.Validate();
        Width = Math.Clamp(state.Window.Width, MinWidth, 20000);
        Height = Math.Clamp(state.Window.Height, MinHeight, 20000);
        var position = new PixelPoint(state.Window.X, state.Window.Y);
        if (Screens.ScreenFromPoint(position) == null && Screens.Primary is { } primary) position = primary.WorkingArea.Position;
        Position = position;
        normalPlacement = state.Window;
        WindowState = state.Window.Maximized ? WindowState.Maximized : WindowState.Normal;
        if (state.Window.Minimized) WindowState = WindowState.Minimized;
        ShowActivated = state.Window.Active && !state.Window.Minimized;
        restoringVisits = true;
        try { await Task.WhenAll(Left.RestoreStateAsync(state.Left), Right.RestoreStateAsync(state.Right)); }
        finally { restoringVisits = false; }
        DirectoryUsage.Restore(state.UsageVersion >= 2 ? state.Usage ?? [] : []);
        DirectoryUsage.Seed(state.Left.History.Concat(state.Right.History).Append(state.Left.DirectoryPath).Append(state.Right.DirectoryPath));
        Favorites.Restore(state.Favorites ?? []);
        CommandLine.Restore(state.CommandLine);
        RestoreViewerTheme(state.ViewerThemeId);
        FileSearch.Restore(state.FileSearch);
        Activate(state.ActivePanel == "right" ? Right : Left);
        restoredState = state;
        RestoreView();
        if (Left.Session.DirectoryPath != state.Left.DirectoryPath || Right.Session.DirectoryPath != state.Right.DirectoryPath)
            SetUpdateMessage("A saved directory is unavailable. Opened its nearest accessible parent.");
    }

    public void RestoreView()
    {
        ActivePanel.FocusCurrent();
        UpdateLayout();
        if (restoredState is not { } state) return;
        Left.RestoreScroll(state.Left); Right.RestoreScroll(state.Right);
        if (state.CommandLine?.Focused == true) CommandLine.FocusCommand();
        CommandLine.RestoreSelection(state.CommandLine);
    }
}

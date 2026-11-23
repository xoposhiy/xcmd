// @implements RecentLocations, RestoreWorkspace, KeepFeedback, InspectAgentSession
using Xcmd.Core;

namespace Xcmd.Desktop.Workspace;

public sealed partial class CommanderWindow
{
    internal void RestoreViewerTheme(string? id) => viewing.RestoreTheme(id);
    private WorkspaceWindowPlacement? placement;
    private WorkspaceState? restoredState;

    private WorkspaceWindowPlacement Placement => placement ??= new(this);

    public WorkspaceState CaptureWorkspace() => new(1, Left.CaptureState(), Right.CaptureState(), ActivePanel.Id,
        Placement.Capture(), null, DirectoryUsage.Capture(), Favorites.Items.ToArray(), UsageVersion: 2,
        CommandLine.Capture(), ViewerThemeId, FileSearch.Capture(), Feedback.Agent);

    public void TrackPlacement() => Placement.Track();

    internal ViewerHandoff[] CaptureViewers() => viewing.CaptureViewers();
    internal Task PrepareViewersAsync(ViewerHandoff[] viewers) => viewing.PrepareViewersAsync(viewers);
    internal void ShowRestoredViewers() => viewing.ShowRestoredViewers();
    internal InspectorHandoff[] CaptureInspectors() => panelSources.CaptureInspectors();
    public void RestoreInspectors(InspectorHandoff[] inspectors)
    {
        foreach (var saved in inspectors) panelSources.OpenInspector(saved.Session, saved);
    }

    public async Task RestoreWorkspaceAsync(WorkspaceState state)
    {
        state.Validate();
        Placement.Restore(state.Window);
        restoringVisits = true;
        try { await Task.WhenAll(Left.RestoreStateAsync(state.Left), Right.RestoreStateAsync(state.Right)); }
        finally { restoringVisits = false; }
        DirectoryUsage.Restore(state.UsageVersion >= 2 ? state.Usage ?? [] : []);
        DirectoryUsage.Seed(state.Left.History.Concat(state.Right.History).Append(state.Left.DirectoryPath).Append(state.Right.DirectoryPath));
        Favorites.Restore(state.Favorites ?? []);
        CommandLine.Restore(state.CommandLine);
        RestoreViewerTheme(state.ViewerThemeId);
        FileSearch.Restore(state.FileSearch);
        Feedback.RestoreAgent(state.FeedbackAgent);
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

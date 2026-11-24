// @implements NavigatePanels, ViewRecentAgentSessions, ResumeAgentSession, InspectAgentSession
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Features.Viewer;
using Xcmd.Desktop.Shared.Platform;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Workspace;

internal sealed class PanelSourceController
{
    private readonly CommanderWindow window;
    private readonly IAgentSessions agentSessions;
    private ContextMenu? sourceMenu;
    private readonly List<SessionInspectorWindow> inspectors = [];

    internal PanelSourceController(CommanderWindow window, IAgentSessions? source)
    {
        this.window = window;
        if (source == null)
        {
            var readers = new AgentSessions();
            window.Closed += async (_, _) => await readers.DisposeAsync();
            source = readers;
        }
        agentSessions = source;
        window.Closed += (_, _) =>
        {
            foreach (var viewer in inspectors.ToArray()) viewer.Close();
        };
        window.Activated += (_, _) => SetWindowActive(true);
        window.Deactivated += (_, _) => SetWindowActive(false);
    }

    private void SetWindowActive(bool active)
    {
        window.Left.SessionWindowActivation(active);
        window.Right.SessionWindowActivation(active);
    }

    internal bool IsOpen => sourceMenu?.IsOpen == true;
    internal IReadOnlyList<Window> Inspectors => inspectors;
    internal InspectorHandoff[] CaptureInspectors() => inspectors.Select(v => v.Capture()).ToArray();

    internal void ChoosePanelSource(PanelView panel)
    {
        window.PreparePanelSource(panel);
        panel.FocusCurrent();
        var menu = new ContextMenu { Placement = PlacementMode.BottomEdgeAlignedLeft }.WithFeedbackId("PanelSource.menu");
        MenuItem? current = null;
        var root = Path.GetPathRoot(panel.Session.DirectoryPath);
        IEnumerable<string> drives = AgentSessions.Drives();
        if (!string.IsNullOrEmpty(root))
            drives = drives.Where(drive => !drive.Equals(root, StringComparison.OrdinalIgnoreCase)).Append(root);
        foreach (var drive in drives)
            AddItem(drive, !panel.ShowsAgentSessions && drive == root, () => window.Execute(async () =>
            {
                if (!await panel.LoadAsync(panel.Session.DirectoryForDrive(drive), nearestExistingParent: true))
                { window.SetError(panel.Session.Error); return; }
                panel.LeaveAgentSessions();
                window.Activate(panel);
                panel.FocusCurrent();
            }));
        menu.Items.Add(new Separator().WithFeedbackId("PanelSource.separator"));
        AddSessions("_All Agent Sessions", null);
        foreach (var agent in new[] { SessionAgent.ClaudeCode, SessionAgent.Codex })
        {
            var item = AddSessions(agent == SessionAgent.ClaudeCode ? "_Claude Code" : "Code_x", agent);
            item.Padding += new Thickness(16, 0, 0, 0);
        }
        sourceMenu = menu;
        menu.Closed += (_, _) => { if (sourceMenu == menu) sourceMenu = null; };
        menu.Open(panel.SourceMenuAnchor);
        current?.Focus(NavigationMethod.Directional);

        MenuItem AddSessions(string title, SessionAgent? filter) =>
            AddItem(title, panel.ShowsAgentSessions && panel.AgentFilter == filter, () =>
            {
                window.Execute(() => panel.ShowAgentSessionsAsync(agentSessions, filter));
                window.Activate(panel);
                panel.FocusCurrent();
            });

        MenuItem AddItem(string title, bool isCurrent, Action action)
        {
            var item = new MenuItem { Header = title }.WithFeedbackId("PanelSource.item");
            // Opening the menu under a stationary pointer also raises PointerEnteredItem.
            item.PointerEnteredItem += (_, e) => e.Handled = true;
            item.PointerMoved += (_, _) => { if (!item.IsSelected) item.Focus(NavigationMethod.Pointer); };
            item.Click += (_, _) => { menu.Close(); action(); };
            menu.Items.Add(item);
            if (isCurrent) current = item;
            return item;
        }
    }

    internal bool RouteKey(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.F1 or Key.F2)
        {
            ChoosePanelSource(e.Key == Key.F1 ? window.Left : window.Right);
            e.Handled = true;
            return true;
        }
        var panel = window.ActivePanel;
        if (!panel.ShowsAgentSessions || !panel.IsKeyboardFocusWithin || e.Key == Key.Tab) return false;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.Left or Key.Right)
        {
            var target = e.Key == Key.Left ? window.Left : window.Right;
            if (target != panel && panel.SelectedSession is { } session) window.Execute(() => OpenProjectAsync(target, session));
            e.Handled = true;
            return true;
        }
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.F3)
        {
            Inspect(panel);
            e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Enter)
        {
            if (window.BeginLaunchKey(e.Key)) window.Execute(() => ResumeAsync(panel));
            e.Handled = true;
        }
        else e.Handled = (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None) || panel.HandleCursorKey(e);
        return true;
    }

    private async Task OpenProjectAsync(PanelView target, AgentSession session)
    {
        if (!Path.IsPathFullyQualified(session.ProjectDirectory)) { window.SetError("The session has no local project folder."); return; }
        if (await target.LoadAsync(session.ProjectDirectory)) target.LeaveAgentSessions();
        else window.SetError(target.Session.Error);
    }

    internal void Inspect(PanelView panel)
    {
        if (panel.SelectedSession is not { } session) { window.SetError("Select a session to inspect."); return; }
        OpenInspector(session);
    }

    internal void OpenInspector(AgentSession session, InspectorHandoff? saved = null)
    {
        var viewer = new SessionInspectorWindow(session, agentSessions, window.Feedback).WithFeedbackId("PanelSource.inspector");
        inspectors.Add(viewer);
        viewer.Opened += (_, _) => TaskbarIdentity.Configure(viewer, window.Trace.Add);
        window.Feedback.Attach(viewer);
        viewer.Closed += (_, _) =>
        {
            inspectors.Remove(viewer);
            window.RecordActivity();
            if (window.IsActive) window.ActivePanel.FocusCurrent();
        };
        viewer.OpenFor(window, saved);
    }

    internal Task ResumeAsync(PanelView panel) => panel.SelectedSession is { } session
        ? window.LaunchExternalAsync(agentSessions.Resume(session)) : Task.CompletedTask;
}

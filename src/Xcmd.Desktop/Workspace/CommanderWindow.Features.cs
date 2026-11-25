// @implements OpenSearchMatch, SearchResultPanel, OpenViewers, QuickViewFile, ComparePanelFiles, ReportControl, BrowseHistory, InspectAgentSession
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Workspace;

public sealed partial class CommanderWindow
{
    private void ShowFileSearch()
    {
        if (HasOpenDialog) return;
        var panel = ActivePanel;
        FileSearch.Show(panel.Session.DirectoryPath, dialog =>
        {
            void ReturnToPanel()
            {
                dialog.Close();
                Activate();
                Activate(panel);
                panel.FocusCurrent();
            }

            dialog.GoRequested += result => Execute(async () =>
            {
                var entry = result.Entry;
                var directory = entry.IsDirectory ? entry.FullPath : ArchivePath.Parent(entry.FullPath)!;
                if (await panel.LoadAsync(directory, selectName: entry.IsDirectory ? null : entry.Name)) ReturnToPanel();
                else SetError(panel.Session.Error);
            });
            dialog.ViewRequested += viewing.ViewSearchResults;
            dialog.PanelRequested += (root, results, selected) =>
            {
                panel.ShowSearchResults(root, results, selected);
                ReturnToPanel();
            };
        }, () =>
        {
            RecordActivity();
            if (IsActive) panel.FocusCurrent();
        });
    }

    private void ViewCurrent()
    {
        if (ActivePanel.ShowsAgentSessions) panelSources.Inspect(ActivePanel);
        else if (ActivePanel.HasHistory) GitHistory.ViewCurrent();
        else if (!HasOpenDialog) viewing.ViewCurrent(ActivePanel);
    }

    private void ToggleQuickView() => viewing.ToggleQuickView(ActivePanel, OppositePanel);
    private Task EditCurrentAsync() => viewing.EditCurrentAsync(ActivePanel);
    private Task CompareCurrentFilesAsync() => viewing.CompareCurrentFilesAsync(Left.Current?.Entry, Right.Current?.Entry);

    private PanelView GetActivePanel() => ActivePanel;

    private void FocusAfterCommandEditor()
    {
        Activate(ActivePanel);
        ActivePanel.FocusCurrent();
    }

    private void CancelPanelLoads()
    {
        Left.Session.CancelLoad();
        Right.Session.CancelLoad();
    }

    private void RestoreFeedbackFocus(Control? previousFocus)
    {
        if (previousFocus != null && (previousFocus == CommandLine.Editor ||
            previousFocus.FindAncestorOfType<FileViewer>(includeSelf: true) != null) && previousFocus.Focus()) return;
        ActivePanel.FocusCurrent();
    }

    private void OnViewerClosed()
    {
        RecordActivity();
        if (IsActive) ActivePanel.FocusCurrent();
    }

    private void RestoreFocusAfterGitAction()
    {
        RecordActivity();
        ActivePanel.FocusCurrent(scrollIntoView: false);
    }
}

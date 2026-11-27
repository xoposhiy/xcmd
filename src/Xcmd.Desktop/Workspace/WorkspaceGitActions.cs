// @implements SeeRepositoryState
using Xcmd.Desktop.Features.Git;
using Xcmd.Core;
using Xcmd.Desktop.Features.FileOperations;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Workspace;

internal sealed class WorkspaceGitActions(CommanderWindow window, OperationDialogHost dialogs)
{
    private bool switching;

    public static void Attach(CommanderWindow window, OperationDialogHost dialogs, UiActions ui, Action restoreFocus,
        Func<ExternalLaunch, Task> launch, string? claude)
    {
        var actions = new WorkspaceGitActions(window, dialogs);
        PanelView[] panels = [window.Left, window.Right];
        GitCommitController.Attach(panels, window.Activate, restoreFocus, () => window.GitHistory.Open(selectedItem: false), ui, launch, claude);
        foreach (var panel in panels)
        {
            panel.Git.BranchesRequested += root => ui.Execute(() => actions.OpenBranchesAsync(panel, root));
            panel.Git.SwitchBranchRequested += (root, name) => ui.Execute(() => actions.SwitchAsync(panel, root, name));
        }
    }

    private async Task OpenBranchesAsync(PanelView panel, string root, string? selectedBranch = null, string? error = null)
    {
        var branches = await GitBranches.LocalAsync(root);
        if (!window.IsVisible || window.HasOpenDialog || window.ActivePanel != panel || panel.Git.Status?.Root != root) return;
        var dialog = new GitBranchDialog(panel.Git.Status, branches, selectedBranch) { Background = window.Background }.WithFeedbackId("CommanderWindow.branches");
        if (error != null) dialog.ShowError(error);
        dialogs.Track(dialog);
        dialog.Closed += (_, _) => { dialogs.Track(null); window.RecordActivity(); window.ActivePanel.FocusCurrent(scrollIntoView: false); };
        dialog.SwitchRequested += name => window.Execute(() => SwitchAsync(panel, root, name, dialog));
        _ = dialog.ShowDialog(window);
    }

    private async Task SwitchAsync(PanelView panel, string root, string name, GitBranchDialog? dialog = null)
    {
        if (switching) return;
        switching = true;
        dialog?.SetSwitching(true);
        window.SetStatusMessage("Switching to " + name + "…");
        try { await GitBranches.SwitchAsync(root, name); }
        catch (IOException failure)
        {
            window.SetError(failure.Message);
            if (dialog != null) dialog.ShowError(failure.Message);
            else await OpenBranchesAsync(panel, root, name, failure.Message);
            return;
        }
        finally { switching = false; dialog?.SetSwitching(false); }

        dialog?.Close();
        window.SetStatusMessage("Switched to " + name + ".");
        foreach (var item in new[] { window.Left, window.Right })
        {
            if (!string.Equals(item.Git.Status?.Root, root, StringComparison.OrdinalIgnoreCase)) continue;
            await item.RefreshAsync(() => item.EditingRow == null, () => window.ActivePanel == item);
            await item.Git.PendingWork;
            item.Git.Refresh();
            await item.Git.PendingWork;
        }
    }
}

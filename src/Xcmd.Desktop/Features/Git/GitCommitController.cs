// @implements BrowseHistory, DelegateCommit, SyncRepository
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Git;

internal static class GitCommitController
{
    public static void Attach(PanelView[] panels, Action<PanelView> activate, Action closeMenu, Action openLog,
        UiActions ui, Func<ExternalLaunch, Task> launch, string? executable)
    {
        var launcher = new ClaudeCommitLauncher(executable);
        var remoteCommandRunning = false;
        foreach (var panel in panels)
        {
            panel.Git.LogRequested += () => { activate(panel); openLog(); };
            panel.Git.Actions.Opened += (_, _) => activate(panel);
            panel.Git.Actions.Closed += (_, _) => closeMenu();
            panel.Git.CommitRequested += directory => ui.Execute(async () =>
            {
                var request = await launcher.PrepareAsync(directory);
                await launch(request);
                ui.SetStatus("Opened Claude Code for commit: " + request.WorkingDirectory);
            });
            panel.Git.RemoteCommandRequested += (root, command) => ui.Execute(async () =>
            {
                if (remoteCommandRunning) return;
                remoteCommandRunning = true;
                var operation = command == GitRemoteCommand.Push ? "Git push" : "Git pull";
                ui.SetStatus(operation + " in progress…");
                try
                {
                    await GitRemoteCommands.RunAsync(root, command);
                    ui.SetStatus(operation + " completed.");
                }
                finally
                {
                    remoteCommandRunning = false;
                    foreach (var item in panels) item.Git.Refresh();
                }
            });
        }
    }
}

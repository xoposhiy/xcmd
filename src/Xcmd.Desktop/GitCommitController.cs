// @implements BrowseHistory, DelegateCommit, SyncRepository
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal static class GitCommitController
{
    public static void Attach(CommanderWindow owner, string? executable)
    {
        var launcher = new ClaudeCommitLauncher(executable);
        var remoteCommandRunning = false;
        foreach (var panel in new[] { owner.Left, owner.Right })
        {
            panel.Git.LogRequested += () => { owner.Activate(panel); owner.GitHistory.Open(selectedItem: false); };
            panel.Git.Actions.Opened += (_, _) => owner.Activate(panel);
            panel.Git.Actions.Closed += (_, _) => { owner.RecordActivity(); owner.ActivePanel.FocusCurrent(scrollIntoView: false); };
            panel.Git.CommitRequested += directory => owner.Execute(async () =>
            {
                var launch = await launcher.PrepareAsync(directory);
                await owner.LaunchExternalAsync(launch);
                owner.SetStatusMessage("Opened Claude Code for commit: " + launch.WorkingDirectory);
            });
            panel.Git.RemoteCommandRequested += (root, command) => owner.Execute(async () =>
            {
                if (remoteCommandRunning) return;
                remoteCommandRunning = true;
                var operation = command == GitRemoteCommand.Push ? "Git push" : "Git pull";
                owner.SetStatusMessage(operation + " in progress…");
                try
                {
                    await GitRemoteCommands.RunAsync(root, command);
                    owner.SetStatusMessage(operation + " completed.");
                }
                finally
                {
                    remoteCommandRunning = false;
                    owner.Left.Git.Refresh();
                    owner.Right.Git.Refresh();
                }
            });
        }
    }
}

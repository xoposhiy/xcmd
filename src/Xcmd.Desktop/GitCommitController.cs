using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal static class GitCommitController
{
    public static void Attach(CommanderWindow owner, string? executable)
    {
        var launcher = new ClaudeCommitLauncher(executable);
        foreach (var panel in new[] { owner.Left, owner.Right })
        {
            panel.Git.Actions.Opened += (_, _) => owner.Activate(panel);
            panel.Git.Actions.Closed += (_, _) => { owner.RecordActivity(); panel.FocusCurrent(scrollIntoView: false); };
            panel.Git.CommitRequested += directory => owner.Execute(async () =>
            {
                var launch = await launcher.PrepareAsync(directory);
                await owner.LaunchExternalAsync(launch);
                owner.SetStatusMessage("Opened Claude Code for commit: " + launch.WorkingDirectory);
            });
        }
    }
}

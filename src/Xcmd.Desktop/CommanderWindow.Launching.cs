using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    private readonly IExternalLauncher externalLauncher;
    private bool launching;
    private readonly HashSet<Avalonia.Input.Key> launchKeysHeld = [];
    public CommandLineView CommandLine { get; } = new();
    private bool runningCommand;

    internal async Task LaunchExternalAsync(ExternalLaunch request)
    {
        if (launching) throw new IOException("A program is already opening. Try again when it finishes.");
        launching = true;
        try { await externalLauncher.LaunchAsync(request); SetError(null); }
        finally { launching = false; }
    }

    private async Task RunCommandLineAsync()
    {
        if (runningCommand || string.IsNullOrWhiteSpace(CommandLine.Editor.Text)) return;
        var text = CommandLine.Editor.Text;
        var panel = ActivePanel;
        var directory = panel.Session.DirectoryPath;
        runningCommand = true;
        try
        {
            var command = Core.CommandLine.Parse(text);
            status.Text = command.ChangesDirectory ? "Opening directory: " + command.Target : "Opening: " + text.Trim();
            if (command.ChangesDirectory)
            {
                var path = Core.CommandLine.DirectoryPath(command.Target, directory);
                if (!await panel.LoadAsync(path)) throw new IOException(panel.Session.Error ?? "The directory could not be opened.");
            }
            else await LaunchExternalAsync(LaunchPlans.RunCommand(command, directory));
            CommandLine.AddHistory(text.Trim());
            if (CommandLine.Editor.Text == text) CommandLine.Clear();
            SetError(null);
            status.Text = command.ChangesDirectory ? "Directory: " + panel.Session.DirectoryPath : "Opened: " + text.Trim();
        }
        finally { runningCommand = false; }
    }

    private void InsertCurrentArgument(bool fullPath)
    {
        if (ActivePanel.Current is not { } row) return;
        CommandLine.InsertArgument(fullPath ? row.Entry.FullPath : row.Name);
    }
}

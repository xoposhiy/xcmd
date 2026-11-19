// @implements RunCommand, CommandHistory, CompleteCommand, ReusePaths, SearchResultPanel, KeyboardLayouts
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.CommandLine;

internal sealed class CommandLineController(CommandLineView view, IArchiveContents archiveContents, UiActions ui,
    Func<PanelView> activePanel, Action focusActivePanel, Func<PanelView, bool> canReturnFocus, Action stopNameSearch,
    Func<ExternalLaunch, Task> launch, Func<bool> holdEnter, Action escapeEditor)
{
    private bool running;

    public bool HandleEditorKey(KeyEventArgs e)
    {
        if (view.Completions.HandleKey(e)) return true;
        switch (e.Key, e.KeyModifiers)
        {
            case (Key.Space, KeyModifiers.Control):
                ui.Execute(view.CompleteAsync);
                break;
            case (Key.Escape, KeyModifiers.None):
                escapeEditor();
                break;
            case (Key.C, KeyModifiers.Control) when view.Editor.SelectionStart == view.Editor.SelectionEnd:
                ui.Execute(view.CopyAllAsync);
                break;
            case (Key.Up or Key.Down, KeyModifiers.Alt):
                view.BrowseHistory(e.Key == Key.Up);
                break;
            case (Key.Up or Key.Down, KeyModifiers.None):
                focusActivePanel();
                break;
            case (Key.Enter, var modifiers) when modifiers.HasFlag(KeyModifiers.Control):
                InsertCurrentArgument(modifiers.HasFlag(KeyModifiers.Shift));
                break;
            case (Key.Enter, KeyModifiers.None):
                if (holdEnter()) ui.Execute(RunAsync);
                break;
            default:
                return false;
        }
        return true;
    }

    public bool HandlePanelShortcut(KeyEventArgs e)
    {
        Action? action = (e.Key, e.KeyModifiers) switch
        {
            (Key.Left or Key.Right, KeyModifiers.None) when activePanel().Table.IsKeyboardFocusWithin => view.FocusCommand,
            (Key.Enter, var modifiers) when modifiers.HasFlag(KeyModifiers.Control) => () => InsertCurrentArgument(modifiers.HasFlag(KeyModifiers.Shift)),
            (Key.L, KeyModifiers.Control) => view.FocusCommand,
            (Key.Up, KeyModifiers.Alt) => () => view.BrowseHistory(true),
            _ => null
        };
        if (action == null) return false;
        stopNameSearch();
        action();
        return true;
    }

    private async Task RunAsync()
    {
        var original = view.Editor.Text;
        if (running || string.IsNullOrWhiteSpace(original)) return;
        var text = original;
        var panel = activePanel();
        var directory = ArchivePath.WorkingDirectory(panel.Session.DirectoryPath);
        running = true;
        try
        {
            var command = Core.CommandLine.Parse(text);
            ui.SetStatus(command.ChangesDirectory ? "Opening directory: " + command.Target : "Opening: " + text.Trim());
            if (command.ChangesDirectory)
            {
                var path = Core.CommandLine.DirectoryPath(command.Target, directory, PanelView.HomeDirectory);
                if (!await panel.LoadAsync(path)) throw new IOException(panel.Session.Error ?? "The directory could not be opened.");
            }
            else
            {
                var launched = await CommandLayoutRecovery.LaunchAsync(LaunchPlans.RunCommand(command, directory), launch);
                if (launched.Target != command.Target)
                    text = Core.CommandLine.FormatArgument(launched.Target) + (launched.Arguments.Length == 0 ? "" : " " + launched.Arguments);
            }
            view.AddHistory(text.Trim());
            if (view.Editor.Text == original)
            {
                view.Clear();
                if (view.HasKeyboardFocus && canReturnFocus(panel)) panel.FocusCurrent(scrollIntoView: false);
            }
            ui.SetError(null);
            ui.SetStatus(command.ChangesDirectory ? "Directory: " + panel.DisplayPath : "Opened: " + text.Trim());
        }
        finally { running = false; }
    }

    private void InsertCurrentArgument(bool fullPath)
    {
        var panel = activePanel();
        if (panel.Current is not { } row) return;
        if (ArchivePath.IsEntry(row.Entry.FullPath))
        {
            if (row.Entry.IsDirectory) { ui.SetError("Extract this folder before opening it externally."); return; }
            ui.Execute(async () => view.InsertArgument(await archiveContents.MaterializeAsync(row.Entry.FullPath, CancellationToken.None)));
            return;
        }
        view.InsertArgument(fullPath || panel.IsSearchResults ? row.Entry.FullPath : row.Name);
    }
}

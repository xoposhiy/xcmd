using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileSearchController
{
    private readonly CommanderWindow owner;
    private readonly FileViewingController viewing;
    private readonly string? cacheRoot;
    private FileSearchState state = new();
    public FileSearchWindow? Window { get; private set; }
    public bool IsOpen => Window != null;

    internal FileSearchController(CommanderWindow owner, FileViewingController viewing, string? cacheRoot)
    { this.owner = owner; this.viewing = viewing; this.cacheRoot = cacheRoot; }

    public void Close() => Window?.Close();
    public FileSearchState Capture() => Window?.CaptureState() ?? state;
    public void Restore(FileSearchState? saved) => state = saved ?? new();

    public void Open()
    {
        if (Window is { } existing)
        {
            if (existing.WindowState == Avalonia.Controls.WindowState.Minimized) existing.WindowState = Avalonia.Controls.WindowState.Normal;
            existing.Activate(); existing.FocusQuery(); return;
        }
        if (owner.HasOpenDialog) return;
        var panel = owner.ActivePanel;
        var dialog = new FileSearchWindow(panel.Session.DirectoryPath, owner.Feedback, state, cacheRoot) { Background = owner.Background };
        Window = dialog;
        owner.Feedback.Attach(dialog, showButton: false);
        dialog.GoRequested += result => owner.Execute(async () =>
        {
            var directory = result.Entry.IsDirectory ? result.Entry.FullPath : ArchivePath.Parent(result.Entry.FullPath)!;
            if (await panel.LoadAsync(directory, selectName: result.Entry.IsDirectory ? null : result.Entry.Name))
            { dialog.Close(); owner.Activate(); owner.Activate(panel); panel.FocusCurrent(); }
            else owner.SetError(panel.Session.Error);
        });
        dialog.ViewRequested += viewing.ViewSearchResults;
        dialog.PanelRequested += (root, results, selected) =>
        {
            panel.ShowSearchResults(root, results, selected);
            dialog.Close(); owner.Activate(); owner.Activate(panel); panel.FocusCurrent();
        };
        dialog.Opened += (_, _) => TaskbarIdentity.Configure(dialog, owner.Trace.Add);
        dialog.Closed += (_, _) =>
        {
            state = dialog.CaptureState();
            Window = null; owner.RecordActivity();
            if (owner.IsActive) panel.FocusCurrent();
        };
        dialog.Show();
        dialog.FocusQuery();
    }
}

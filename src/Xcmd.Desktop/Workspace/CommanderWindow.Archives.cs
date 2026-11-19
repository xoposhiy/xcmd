// @implements RenameEntry, BrowseArchives, OpenExternalTool
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Workspace;

public sealed partial class CommanderWindow
{
    private bool CancelOpeningArchives() => archives.CancelOpening();

    private void OpenWithWindows()
    {
        var panel = ActivePanel;
        if (panel.Current is not { Entry.IsDirectory: false } row) { SetError("Select a file to open with Windows."); return; }
        var directory = panel.Session.DirectoryPath;
        Execute(() => OpenFileExternallyAsync(row.Entry.FullPath, directory));
    }

    private void RenameCurrent()
    {
        var path = ActivePanel.Current?.Entry.FullPath ?? ActivePanel.Session.DirectoryPath;
        if (ArchivePath.IsEntry(path)) { SetError(ArchivePath.ReadOnlyMessage(path, "Archives are read-only. Entries cannot be renamed.")); return; }
        ActivePanel.BeginRename();
    }

    private Task OpenArchiveAsync(PanelView panel) => archives.OpenAsync(panel);

    internal Task OpenFileExternallyAsync(string path, string directory) => archives.OpenExternallyAsync(path, directory);
}

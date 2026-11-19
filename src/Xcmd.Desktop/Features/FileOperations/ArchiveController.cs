// @implements BrowseArchives, InspectArchives
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.FileOperations;

internal sealed class ArchiveController(string? cacheDirectory, IArchiveContents? contents, UiActions ui,
    Func<ExternalLaunch, Task> launch, Func<PanelView, bool> canReturnFocus) : IDisposable
{
    private readonly HashSet<PanelView> opening = [];
    public IArchiveContents Files { get; } = contents ?? new ArchiveContents(cacheDirectory);
    public IFileSystem Wrap(IFileSystem fileSystem) => new ArchiveFileSystem(fileSystem, Files);

    public bool CancelOpening()
    {
        if (opening.Count == 0) return false;
        foreach (var panel in opening) panel.Session.CancelLoad();
        return true;
    }

    public async Task OpenAsync(PanelView panel, FileEntry? entry = null)
    {
        if (opening.Contains(panel)) return;
        entry ??= panel.Current?.Entry;
        if (entry is not { IsDirectory: false })
        { ui.SetError("Select a file to open as an archive."); return; }
        var hadFocus = panel.Table.IsKeyboardFocusWithin;
        opening.Add(panel);
        ui.SetStatus("Opening archive: " + entry.Name + " · Escape cancels");
        try
        {
            var loaded = await panel.LoadAsync(ArchivePath.Root(entry.FullPath), selectName: "..");
            ui.SetError(panel.Session.Error);
            if (!loaded && panel.Session.Error == null) ui.SetStatus("Archive opening cancelled.");
            if (hadFocus && canReturnFocus(panel)) panel.FocusCurrent();
        }
        finally { opening.Remove(panel); }
    }

    public async Task OpenExternallyAsync(string path, string directory)
    {
        var physical = await Files.MaterializeAsync(path, CancellationToken.None);
        await launch(LaunchPlans.OpenFile(physical, ArchivePath.WorkingDirectory(directory)));
        if (ArchivePath.IsEntry(path)) ui.SetError("Opened a temporary copy. Use F5 to extract a file before editing it.");
    }

    public void Dispose() => Files.Dispose();
}

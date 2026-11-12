using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal sealed class ArchiveController(CommanderWindow owner, string? cacheDirectory, IArchiveContents? contents) : IDisposable
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
        { owner.SetError("Select a file to open as an archive."); return; }
        var hadFocus = panel.Table.IsKeyboardFocusWithin;
        opening.Add(panel);
        owner.SetStatusMessage("Opening archive: " + entry.Name + " · Escape cancels");
        try
        {
            var loaded = await panel.LoadAsync(ArchivePath.Root(entry.FullPath), selectName: "..");
            owner.SetError(panel.Session.Error);
            if (!loaded && panel.Session.Error == null) owner.SetStatusMessage("Archive opening cancelled.");
            if (owner.ActivePanel == panel && hadFocus && !owner.CommandLine.HasKeyboardFocus && !owner.HasOpenDialog) panel.FocusCurrent();
        }
        finally { opening.Remove(panel); }
    }

    public async Task OpenExternallyAsync(string path, string directory)
    {
        var physical = await Files.MaterializeAsync(path, CancellationToken.None);
        await owner.LaunchExternalAsync(LaunchPlans.OpenFile(physical, ArchivePath.WorkingDirectory(directory)));
        if (ArchivePath.IsEntry(path)) owner.SetError("Opened a temporary copy. Use F5 to extract a file before editing it.");
    }

    public void Dispose() => Files.Dispose();
}

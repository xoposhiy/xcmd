using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal sealed class FileViewingController(CommanderWindow owner, string? editorPath)
{
    private readonly IViewerFiles files = new ViewerFiles();
    private readonly ExternalEditor editor = new(editorPath);
    private readonly List<FileViewerWindow> viewers = [];
    public IReadOnlyList<FileViewerWindow> Viewers => viewers;
    public FileViewerWindow? Viewer => viewers.LastOrDefault(viewer => viewer.IsActive) ?? viewers.LastOrDefault();
    public bool IsBusy => viewers.Any(viewer => viewer.IsLoading || viewer.IsSearching);
    public bool HasViewers => viewers.Count > 0;
    public string ThemeId { get; private set; } = "dark";

    public void RestoreTheme(string? id)
    {
        ThemeId = ReaderThemes.Find(id).Id;
        foreach (var viewer in viewers) viewer.ApplyTheme(ThemeId);
    }

    public void CloseAll() { foreach (var viewer in viewers.ToArray()) viewer.Close(); }

    public void ViewCurrent()
    {
        if (owner.HasOpenDialog) return;
        var panel = owner.ActivePanel;
        if (panel.Current is not { Entry.IsDirectory: false } row) { owner.SetError("Select a file to view."); return; }
        var paths = panel.Rows.Where(item => !item.Entry.IsDirectory).Select(item => item.Entry.FullPath).ToArray();
        var directory = panel.Session.DirectoryPath;
        var viewer = new FileViewerWindow(paths, Array.IndexOf(paths, row.Entry.FullPath), files, owner.Feedback,
            path => EditFileAsync(path, directory),
            path => owner.LaunchExternalAsync(LaunchPlans.OpenFile(path, directory)), ThemeId);
        viewers.Add(viewer);
        viewer.ThemeChanged += id => { RestoreTheme(id); owner.Execute(owner.NotifyViewerThemeChangedAsync); };
        owner.Feedback.Attach(viewer, showButton: false);
        viewer.Opened += (_, _) => TaskbarIdentity.Configure(viewer, owner.Trace.Add);
        viewer.Closed += (_, _) => { viewers.Remove(viewer); owner.RecordActivity(); if (owner.IsActive) owner.ActivePanel.FocusCurrent(); };
        viewer.Show();
        owner.Execute(viewer.OpenAsync);
    }

    public async Task EditCurrentAsync()
    {
        var panel = owner.ActivePanel;
        if (panel.Current is not { Entry.IsDirectory: false } row) { owner.SetError("Select a file to edit."); return; }
        await EditFileAsync(row.Entry.FullPath, panel.Session.DirectoryPath);
    }

    private async Task EditFileAsync(string path, string directory)
    {
        await owner.LaunchExternalAsync(await editor.PrepareAsync(path, directory));
    }
}

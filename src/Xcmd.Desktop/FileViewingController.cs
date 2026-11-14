using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal sealed class FileViewingController(CommanderWindow owner, string? editorPath, IArchiveContents archives)
{
    private readonly IViewerFiles files = new ArchiveViewerFiles(archives);
    private readonly ExternalEditor editor = new(editorPath);
    private readonly List<FileViewerWindow> viewers = [];
    private string lastSearch = "";
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
        if (owner.ActivePanel.HasHistory) { owner.GitHistory.ViewCurrent(); return; }
        if (owner.HasOpenDialog) return;
        var panel = owner.ActivePanel;
        if (panel.Current is not { Entry.IsDirectory: false } row) { owner.SetError("Select a file to view."); return; }
        var paths = panel.Rows.Where(item => !item.Entry.IsDirectory).Select(item => item.Entry.FullPath).ToArray();
        var directory = panel.Session.DirectoryPath;
        OpenViewer(paths, Array.IndexOf(paths, row.Entry.FullPath), directory);
    }

    public GitHistoryController CreateGitHistory() => new(owner, ViewDocument);
    public void ViewDocument(ViewerDocument document) => OpenViewer([document.Path], 0, Path.GetDirectoryName(document.Path)!, document: document);

    public void ViewSearchResults(IReadOnlyList<FileSearchResult> results, int index, string text)
    {
        var paths = results.Select(result => result.Entry.FullPath).ToArray();
        var match = results[index].Match;
        OpenViewer(paths, index, ArchivePath.WorkingDirectory(ArchivePath.Parent(paths[index])!),
            viewer => viewer.SearchFromResultsAsync(match?.Text ?? text, match?.CodePage));
    }

    private FileViewerWindow OpenViewer(string[] paths, int index, string directory, Func<FileViewerWindow, Task>? loaded = null, ViewerDocument? document = null)
    {
        var viewer = new FileViewerWindow(paths, index, files, owner.Feedback,
            path => EditFileAsync(path, directory),
            path => owner.OpenFileExternallyAsync(path, directory), ThemeId, document).WithFeedbackId("FileViewingController.viewer");
        viewers.Add(viewer);
        viewer.RememberedSearch = lastSearch;
        viewer.SearchTextChanged += text => { lastSearch = text; foreach (var openViewer in viewers) openViewer.RememberedSearch = text; };
        viewer.ThemeChanged += id => { RestoreTheme(id); owner.Execute(owner.NotifyViewerThemeChangedAsync); };
        owner.Feedback.Attach(viewer, showButton: false);
        viewer.Opened += (_, _) => TaskbarIdentity.Configure(viewer, owner.Trace.Add);
        viewer.Closed += (_, _) => { viewers.Remove(viewer); owner.RecordActivity(); if (owner.IsActive) owner.ActivePanel.FocusCurrent(); };
        viewer.Show();
        owner.Execute(async () => { await viewer.OpenAsync(); if (loaded != null) await loaded(viewer); });
        return viewer;
    }

    public async Task EditCurrentAsync()
    {
        var panel = owner.ActivePanel;
        if (panel.Current is not { Entry.IsDirectory: false } row) { owner.SetError("Select a file to edit."); return; }
        await EditFileAsync(row.Entry.FullPath, panel.OperationDirectory);
    }

    public Task<ExternalLaunch> CreateFileAsync(string path, string directory) => editor.CreateAsync(path, directory);

    private async Task EditFileAsync(string path, string directory)
    {
        ArchivePath.RequireWritable(path);
        await owner.LaunchExternalAsync(await editor.PrepareAsync(path, directory));
    }
}

// @implements ReusableFileViewer, CreateFile, OpenSearchMatch, OpenViewers, QuickViewFile, FindViewerText, ReaderThemes, OpenViewedFile, InspectArchives, OpenExternalTool, ComparePanelFiles, RestoreWorkspace, ReleaseHandoff
using Avalonia.Controls;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal sealed class FileViewingController
{
    private readonly CommanderWindow owner;
    private readonly IViewerFiles files;
    private readonly ExternalEditor editor;
    private readonly VisualStudioCodeComparison comparison;
    private readonly QuickViewPane quickView;
    private bool comparing;
    private readonly List<FileViewerWindow> viewers = [];
    private (FileViewerWindow Window, ViewerReadingState Reading)[] restoredViewers = [];
    private string lastSearch = "";
    public Control QuickViewControl => quickView;
    public IReadOnlyList<FileViewerWindow> Viewers => viewers;
    public FileViewerWindow? Viewer => viewers.LastOrDefault(viewer => viewer.IsActive) ?? viewers.LastOrDefault();
    public bool IsBusy => quickView.Reader is { IsLoading: true } or { IsSearching: true } || viewers.Any(viewer => viewer.Reader.IsLoading || viewer.Reader.IsSearching);
    public bool IsQuickViewOpen => quickView.IsVisible;
    public bool IsQuickViewFocused => quickView.IsKeyboardFocusWithin;
    public bool IsQuickViewMenuOpen => quickView.Reader?.Commands.IsOpen == true;
    public bool HasActiveViewer => viewers.Any(viewer => viewer.IsActive);
    public string ThemeId { get; private set; } = "dark";

    public FileViewingController(CommanderWindow owner, string? editorPath, IArchiveContents archives, string? comparisonExecutable)
    {
        this.owner = owner;
        files = new ArchiveViewerFiles(archives);
        editor = new ExternalEditor(editorPath);
        comparison = new VisualStudioCodeComparison(comparisonExecutable);
        quickView = new QuickViewPane().WithFeedbackId("FileViewingController.quickView");
    }

    public void RestoreTheme(string? id)
    {
        ThemeId = ReaderThemes.Find(id).Id;
        quickView.Reader?.ApplyTheme(ThemeId);
        foreach (var viewer in viewers) viewer.Reader.ApplyTheme(ThemeId);
    }

    public void CloseAll()
    {
        CloseQuickView();
        foreach (var viewer in viewers.ToArray()) viewer.Close();
    }

    public void ToggleQuickView()
    {
        if (IsQuickViewOpen) { quickView.CloseFromReader(); return; }
        if (owner.ActivePanel.HasHistory) { owner.SetError("Press F3 to view items in Git history."); return; }
        var panel = owner.ActivePanel;
        var reader = new FileViewer([], 0, files, owner.Feedback, path => EditFileAsync(path, panel.OperationDirectory),
            path => owner.OpenFileExternallyAsync(path, panel.OperationDirectory), quickView.CloseFromReader).WithFeedbackId("FileViewingController.embeddedViewer");
        reader.FileListFocusRequested += quickView.FocusSource;
        ConnectReader(reader);
        quickView.ShowPreview(panel, owner.OppositePanel, reader);
        reader.ApplyTheme(ThemeId);
        panel.FocusCurrent(scrollIntoView: false);
    }

    public bool CloseQuickView() => quickView.HidePreview();
    public void SwitchQuickViewFocus() => quickView.SwitchFocus();
    public void ResetQuickViewKeys() => quickView.Reader?.ResetKeys();
    public void RefreshQuickViewAfterActivation() => quickView.Reader?.RefreshAfterActivation();

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

    private FileViewerWindow OpenViewer(string[] paths, int index, string directory, Func<FileViewer, Task>? loaded = null, ViewerDocument? document = null)
    {
        var viewer = CreateViewer(paths, index, directory, document);
        viewer.FitInitialSize(owner);
        viewer.TrackPlacement();
        RegisterViewer(viewer);
        viewer.Show();
        owner.Execute(async () => { await viewer.Reader.OpenAsync(); if (loaded != null) await loaded(viewer.Reader); });
        return viewer;
    }

    private FileViewerWindow CreateViewer(string[] paths, int index, string directory, ViewerDocument? document) =>
        new FileViewerWindow(paths, index, files, owner.Feedback, path => EditFileAsync(path, directory),
            path => owner.OpenFileExternallyAsync(path, directory), ThemeId, document)
        { WorkingDirectory = directory }.WithFeedbackId("FileViewingController.viewer");

    private void RegisterViewer(FileViewerWindow viewer)
    {
        viewers.Add(viewer);
        ConnectReader(viewer.Reader);
        owner.Feedback.Attach(viewer, showButton: false);
        viewer.Opened += (_, _) => TaskbarIdentity.Configure(viewer, owner.Trace.Add);
        viewer.Closed += (_, _) => { viewers.Remove(viewer); owner.RecordActivity(); if (owner.IsActive) owner.ActivePanel.FocusCurrent(); };
    }

    public ViewerHandoff[] CaptureViewers() => viewers.Select(viewer => viewer.CaptureHandoff()).ToArray();

    public async Task PrepareViewersAsync(ViewerHandoff[] snapshots)
    {
        var prepared = await Task.WhenAll(snapshots.Select(PrepareViewerAsync));
        var missing = snapshots.Where((_, index) => prepared[index] == null)
            .Select(snapshot => snapshot.Document?.Caption ?? Path.GetFileName(ArchivePath.Display(snapshot.Paths[snapshot.Index]))).ToArray();
        if (missing.Length > 0) owner.SetUpdateMessage("Missing after update: " + string.Join(", ", missing));
        restoredViewers = prepared.Where(item => item.HasValue).Select(item => item.GetValueOrDefault()).ToArray();
    }

    private async Task<(FileViewerWindow, ViewerReadingState)?> PrepareViewerAsync(ViewerHandoff snapshot)
    {
        var viewer = CreateViewer(snapshot.Paths, snapshot.Index, snapshot.WorkingDirectory, snapshot.Document);
        try
        {
            await viewer.Reader.PrepareHandoffAsync(snapshot.Reading);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            viewer.Close();
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            viewer.Reader.ShowReadFailure(error.Message);
        }
        viewer.RestorePlacement(snapshot.Window);
        return (viewer, snapshot.Reading);
    }

    public void ShowRestoredViewers()
    {
        foreach (var (viewer, reading) in restoredViewers)
        {
            RegisterViewer(viewer);
            viewer.Show();
            viewer.UpdateLayout();
            viewer.Reader.RestoreReadingPosition(reading);
        }
        restoredViewers = [];
    }

    private void ConnectReader(FileViewer reader)
    {
        reader.RememberedSearch = lastSearch;
        reader.SearchTextChanged += text =>
        {
            lastSearch = text;
            if (quickView.Reader is { } embedded) embedded.RememberedSearch = text;
            foreach (var viewer in viewers) viewer.Reader.RememberedSearch = text;
        };
        reader.ThemeChanged += id => { RestoreTheme(id); owner.Execute(owner.NotifyViewerThemeChangedAsync); };
    }

    public async Task EditCurrentAsync()
    {
        var panel = owner.ActivePanel;
        if (panel.Current is not { Entry.IsDirectory: false } row) { owner.SetError("Select a file to edit."); return; }
        await EditFileAsync(row.Entry.FullPath, panel.OperationDirectory);
    }

    public async Task CompareCurrentFilesAsync()
    {
        if (comparing) return;
        var pair = FileComparison.FromEntries(owner.Left.Current?.Entry, owner.Right.Current?.Entry);
        comparing = true;
        try
        {
            await owner.LaunchExternalAsync(await comparison.PrepareAsync(pair));
            owner.SetStatusMessage("Opened file comparison in VS Code");
        }
        finally { comparing = false; }
    }

    public Task<ExternalLaunch> CreateFileAsync(string path, string directory) => editor.CreateAsync(path, directory);

    private async Task EditFileAsync(string path, string directory)
    {
        ArchivePath.RequireWritable(path);
        await owner.LaunchExternalAsync(await editor.PrepareAsync(path, directory));
    }
}

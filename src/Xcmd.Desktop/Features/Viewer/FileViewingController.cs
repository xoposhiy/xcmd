// @implements ReusableFileViewer, CreateFile, OpenSearchMatch, OpenViewers, QuickViewFile, FindViewerText, ReaderThemes, OpenViewedFile, InspectArchives, OpenExternalTool, ComparePanelFiles, RestoreWorkspace, ReleaseHandoff
using Avalonia.Controls;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Feedback;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Shared.Platform;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed class FileViewingController
{
    private readonly Window owner;
    private readonly FeedbackController feedback;
    private readonly UiActions actions;
    private readonly Func<ExternalLaunch, Task> launch;
    private readonly Func<string, string, Task> openFile;
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
    public event Action? ViewerClosed;
    public event Action? ThemeChanged;
    public event Action<string>? MissingAfterRestore;

    public FileViewingController(Window owner, FeedbackController feedback, IArchiveContents archives, string? editorPath,
        string? comparisonExecutable, UiActions actions, Func<ExternalLaunch, Task> launch, Func<string, string, Task> openFile)
    {
        this.owner = owner;
        this.feedback = feedback;
        this.actions = actions;
        this.launch = launch;
        this.openFile = openFile;
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

    public void ToggleQuickView(PanelView panel, PanelView opposite)
    {
        if (IsQuickViewOpen) { quickView.CloseFromReader(); return; }
        if (panel.HasHistory) { actions.SetError("Press F3 to view items in Git history."); return; }
        var reader = new FileViewer([], 0, files, feedback, path => EditFileAsync(path, panel.OperationDirectory),
            path => openFile(path, panel.OperationDirectory), quickView.CloseFromReader).WithFeedbackId("FileViewingController.embeddedViewer");
        reader.FileListFocusRequested += quickView.FocusSource;
        ConnectReader(reader);
        quickView.ShowPreview(panel, opposite, reader);
        reader.ApplyTheme(ThemeId);
        panel.FocusCurrent(scrollIntoView: false);
    }

    public bool CloseQuickView() => quickView.HidePreview();
    public void SwitchQuickViewFocus() => quickView.SwitchFocus();
    public void ResetQuickViewKeys() => quickView.Reader?.ResetKeys();
    public void RefreshQuickViewAfterActivation() => quickView.Reader?.RefreshAfterActivation();

    public void ViewCurrent(PanelView panel)
    {
        if (panel.Current is not { Entry.IsDirectory: false } row) { actions.SetError("Select a file to view."); return; }
        var paths = panel.Rows.Where(item => !item.Entry.IsDirectory).Select(item => item.Entry.FullPath).ToArray();
        var directory = panel.Session.DirectoryPath;
        OpenViewer(paths, Array.IndexOf(paths, row.Entry.FullPath), directory);
    }

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
        actions.Execute(async () => { await viewer.Reader.OpenAsync(); if (loaded != null) await loaded(viewer.Reader); });
        return viewer;
    }

    private FileViewerWindow CreateViewer(string[] paths, int index, string directory, ViewerDocument? document) =>
        new FileViewerWindow(paths, index, files, feedback, path => EditFileAsync(path, directory),
            path => openFile(path, directory), ThemeId, document)
        { WorkingDirectory = directory }.WithFeedbackId("FileViewingController.viewer");

    private void RegisterViewer(FileViewerWindow viewer)
    {
        viewers.Add(viewer);
        ConnectReader(viewer.Reader);
        feedback.Attach(viewer, showButton: false, viewer.Reader.SetFeedbackHint, viewer.Reader.FocusReader);
        viewer.Opened += (_, _) => TaskbarIdentity.Configure(viewer, actions.Trace);
        viewer.Closed += (_, _) => { viewers.Remove(viewer); ViewerClosed?.Invoke(); };
    }

    public ViewerHandoff[] CaptureViewers() => viewers.Select(viewer => viewer.CaptureHandoff()).ToArray();

    public async Task PrepareViewersAsync(ViewerHandoff[] snapshots)
    {
        var prepared = await Task.WhenAll(snapshots.Select(PrepareViewerAsync));
        var missing = snapshots.Where((_, index) => prepared[index] == null)
            .Select(snapshot => snapshot.Document?.Caption ?? Path.GetFileName(ArchivePath.Display(snapshot.Paths[snapshot.Index]))).ToArray();
        if (missing.Length > 0) MissingAfterRestore?.Invoke("Missing after update: " + string.Join(", ", missing));
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
        reader.ThemeChanged += id => { RestoreTheme(id); ThemeChanged?.Invoke(); };
    }

    public async Task EditCurrentAsync(PanelView panel)
    {
        if (panel.Current is not { Entry.IsDirectory: false } row) { actions.SetError("Select a file to edit."); return; }
        await EditFileAsync(row.Entry.FullPath, panel.OperationDirectory);
    }

    public async Task CompareCurrentFilesAsync(FileEntry? left, FileEntry? right)
    {
        if (comparing) return;
        var pair = FileComparison.FromEntries(left, right);
        comparing = true;
        try
        {
            await launch(await comparison.PrepareAsync(pair));
            actions.SetStatus("Opened file comparison in VS Code");
        }
        finally { comparing = false; }
    }

    public Task<ExternalLaunch> CreateFileAsync(string path, string directory) => editor.CreateAsync(path, directory);

    private async Task EditFileAsync(string path, string directory)
    {
        ArchivePath.RequireWritable(path);
        await launch(await editor.PrepareAsync(path, directory));
    }
}

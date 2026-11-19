// @tests NavigatePanels, RenameEntry
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Desktop.Features.FileOperations;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Workspace;

namespace Xcmd.Probe.Infrastructure;

public sealed partial class ProbeContext
{
    public string FixturePath { get; }
    public string LeftPath => Path.Combine(FixturePath, "left");
    public string RightPath => Path.Combine(FixturePath, "right");
    public ControlledFileSystem FileSystem { get; } = new();
    public RecordingExternalLauncher Launcher { get; } = new();
    public RecordingShellInteraction Shell { get; } = new();
    public RecordingFileDragSource FileDrag { get; } = new();
    public FileDragDropController DragDrop { get; }
    public ControlledArchiveContents Archives { get; }
    public CommanderWindow Window { get; }

    public ProbeContext(string outputPath)
    {
        Archives = new ControlledArchiveContents(Path.Combine(outputPath, "archive-cache"));
        DragDrop = new FileDragDropController(FileDrag);
        Window = new CommanderWindow(FileSystem, Launcher, editorPath: Path.Combine(AppContext.BaseDirectory, "launch-probe", "Xcmd.LaunchProbe.exe"), archiveCacheRoot: Path.Combine(outputPath, "archive-cache"), archiveContents: Archives, shellInteraction: new ShellInteractionController(Shell, DragDrop), claudeExecutable: Path.Combine(AppContext.BaseDirectory, "launch-probe", "Xcmd.LaunchProbe.exe"), comparisonExecutable: Path.Combine(AppContext.BaseDirectory, "launch-probe", "Xcmd.LaunchProbe.exe"));
        OutputPath = outputPath;
        Window.Feedback.Store = new Xcmd.Infrastructure.IssueStore(Path.Combine(outputPath, "issues"));
        Window.Feedback.ListPath = Window.Feedback.Store.DatabasePath;
        FixturePath = Path.Combine(outputPath, "fixture");
        Directory.CreateDirectory(LeftPath);
        Directory.CreateDirectory(RightPath);
        Directory.CreateDirectory(Path.Combine(LeftPath, "folder1"));
        Directory.CreateDirectory(Path.Combine(LeftPath, "folder2"));
        File.WriteAllText(Path.Combine(LeftPath, "folder1", "inside.txt"), "Nested file");
        AddFile("alpha.txt", "abc", new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddFile("file2.log", new string('b', 20), new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddFile("file10.bin", new string('c', 10), new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddFile("zeta.md", "notes", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddFile(".env", "a");
        AddFile("отчёт 日本語.txt", "Unicode file contents");
        AddFile(new string('a', 180) + ".txt", "Long file name");
        File.WriteAllText(Path.Combine(RightPath, "destination.txt"), "Destination file");
    }

    public void AddFile(string name, string contents, DateTime? modified = null)
    {
        var file = Path.Combine(LeftPath, name);
        File.WriteAllText(file, contents);
        if (modified != null) File.SetLastWriteTimeUtc(file, modified.Value);
    }

    public async Task StartAsync()
    {
        Window.Show();
        await Window.InitializeAsync(LeftPath, RightPath);
        await SettleAsync();
        Window.Left.FocusCurrent();
        PressKey(Key.Down);
        PressKey(Key.Tab);
        PressKey(Key.Down);
        PressKey(Key.Tab);
        await SettleAsync();
    }

    private bool IsBusy =>
        (Window.IsBusy && !Window.Operations.IsWaitingForDecision) ||
        Window.Left.IsCommitting || Window.Right.IsCommitting ||
        Window.OperationDialog is FileErrorDialog { Users.IsBusy: true } ||
        Window.FileSearch.Window?.IsSearching == true ||
        Window.Feedback.IsSaving;

    private IEnumerable<Window> OpenWindows()
    {
        yield return Window;
        if (Window.ActiveDialog is { } dialog) yield return dialog;
        if (Window.ActiveNavigationDialog is { } history) yield return history;
        if (Window.OperationDialog is { } operation) yield return operation;
        if (Window.FileSearch.Window is { } search) yield return search;
        foreach (var viewer in Window.Viewers) yield return viewer;
        if (Window.Feedback.ActiveComment is { } comment) yield return comment;
        if (Window.Feedback.ActiveNotes is { } notes) yield return notes;
    }

    public void CaptureFailure()
    {
        CaptureFailure("failure");
        if (Window.ActiveNavigationDialog is { } history) CaptureFailure("failure-history", history);
        if (Window.FileSearch.Window is { } search) CaptureFailure("failure-search", search);
    }

    public object Snapshot() => new
    {
        activePanel = Window.ActivePanel.Id,
        focused = (Window.FocusManager?.GetFocusedElement() as Control)?.GetType().Name,
        busy = Window.IsBusy,
        error = Window.LastError,
        dialog = Window.ActiveDialog?.Title ?? Window.ActiveNavigationDialog?.Title ?? Window.OperationDialog?.Title ?? Window.Viewer?.Title,
        viewer = Window.Viewer is { } viewer ? new { viewer.Reader.CurrentPath, viewer.Reader.Mode, viewer.Reader.ByteOffset, viewer.Reader.FileLength, viewer.Reader.Information, viewer.Reader.Error, viewer.Reader.IsLoading, viewer.Reader.HasImage, viewer.Reader.ReadingStatus, viewer.Reader.FirstVisibleByte, viewer.Reader.VisibleEnd, viewer.Reader.ViewedPercent } : null,
        operations = Window.Operations.Jobs.Select(job => new { job.Id, job.State, job.CurrentPath, job.BytesTransferred, job.TotalFiles, job.ProcessedFiles, job.SkippedFiles, job.FailedEntries, job.TotalBytes, job.ProcessedBytes, job.TotalsComplete, job.Outcomes }),
        historyDialog = Window.ActiveNavigationDialog is { } history ? new { selected = history.Directories.SelectedItem, history.IsOpening, history.Error } : null,
        search = Window.FileSearch.Window is { } search ? new { search.StatusMessage, search.IsSearching, failures = search.Session?.Failures, results = SearchProbe.Results(search) } : null,
        left = PanelSnapshot(Window.Left),
        right = PanelSnapshot(Window.Right),
        input = InputTrace,
        actions = Window.Trace,
        measurements = Measurements
    };

    private static object PanelSnapshot(PanelView panel) => new
    {
        path = panel.Session.DirectoryPath,
        count = panel.Rows.Count,
        names = panel.Rows.Take(100).Select(row => row.Name),
        selected = panel.SelectedNames(),
        current = panel.Current?.Name,
        editing = panel.EditingRow?.DraftName,
        sort = panel.Session.SortOrder,
        error = panel.Session.Error,
        realizedRows = panel.Table.GetRealizedContainers().Count(),
        history = panel.Session.PreviousDirectories,
        columns = panel.Table.Columns.Select(column => column.ActualWidth)
    };

    public void Dispose()
    {
        Window.ActiveDialog?.Close();
        Window.ActiveNavigationDialog?.Close();
        Window.OperationDialog?.Close();
        Window.FileSearch.Window?.Close();
        foreach (var viewer in Window.Viewers.ToArray()) viewer.Close();
        Window.Feedback.ActiveComment?.Close();
        Window.Feedback.ActiveNotes?.Close();
        Window.Close();
    }
}

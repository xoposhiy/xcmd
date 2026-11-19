// @implements ReadablePanels, NavigatePanels, SortEntries, RenameEntry, QuickViewFile
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels.Git;

namespace Xcmd.Desktop.Features.Panels;

public sealed class PanelView : Border
{
    private readonly PanelFileTable fileTable;
    private readonly PanelLocationHeader header;
    private readonly PanelContentLoader loader;
    private readonly PanelSearchView search;
    private readonly PanelStateRestore stateRestore;
    private readonly PanelHistoryNavigation history;
    private readonly PanelShellInput shellInput;
    private readonly PanelRows panelRows;
    private readonly PanelFooter footer;
    private readonly PanelRename rename;
    private readonly GitPanelRows gitRows = new();
    private readonly GitCommitMessageView commitMessage;

    public PanelSession Session { get; }
    public string DisplayPath => PanelLocationHeader.DisplayPath(Session);
    public TableView Table => fileTable.Table;
    public List<FileRow> Rows => fileTable.Rows;
    public FileRow? EditingRow => rename.EditingRow;
    public bool IsCommitting => rename.IsCommitting;
    public FileRow? Current => Selection.Current;
    public string Id { get; }
    public FileIconCache Icons { get; }
    public GitStatusView Git { get; }
    public static string HomeDirectory => UserDirectories.Home;

    public event Action<PanelView>? Activated;
    public event Action<PanelView>? OpenRequested;
    public event Action<PanelView>? HistoryRequested;
    public event Action<string>? LocationVisited;
    public event Action<PanelView>? LoadingRequested;
    public event Action<PanelView, FileRow, PixelPoint, bool>? ShellMenuRequested;
    internal event Action? CurrentEntryChanged;

    private PanelSelection Selection => fileTable.Selection;

    public PanelView(string id, IFileSystem fileSystem, FileTypes? fileTypes = null)
        : this(id, fileSystem, fileTypes, null) { }

    public PanelView(string id, IFileSystem fileSystem, FileTypes? fileTypes, IGitStatusReader? gitReader)
    {
        Id = id;
        FeedbackTarget.SetScope(this, id);
        Name = id;
        Session = new PanelSession(fileSystem);
        Icons = new FileIconCache(this);
        Git = new GitStatusView(id, gitReader).WithFeedbackId("PanelView.Git");
        panelRows = new PanelRows(fileTypes ?? FileTypeSettings.Bundled());
        fileTable = new PanelFileTable(id, Icons, panelRows, gitRows.Column);
        header = new PanelLocationHeader(id, Git);
        rename = new PanelRename(Table, fileSystem, () => Current, ApplyRenameAsync);

        var returnLocations = new PanelReturnLocations();
        loader = new PanelContentLoader(Session, fileTable, rename, Git, returnLocations, CaptureReturnPoint, () => IsSearchResults);
        search = new PanelSearchView(Session, loader, returnLocations, fileTable, header, CaptureReturnPoint);
        stateRestore = new PanelStateRestore(Session, fileTable, panelRows, loader, search, returnLocations);
        history = new PanelHistoryNavigation(Session, loader, fileTable, returnLocations, stateRestore);
        commitMessage = new GitCommitMessageView(id, Activate).WithFeedbackId("PanelView.commitMessageArea");
        shellInput = new PanelShellInput(Table, Selection, () => EditingRow != null, Activate);

        Background = UiPalette.Surface;
        BorderBrush = UiPalette.Divider;
        BorderThickness = new Thickness(0, 2, 0, 0);
        CornerRadius = new CornerRadius(0);
        ClipToBounds = true;
        DetachedFromVisualTree += (_, _) => Icons.Dispose();

        var content = new Grid { RowDefinitions = new RowDefinitions("34,*,0,29") }.WithFeedbackId("PanelView.content");
        content.Children.Add(header.Border);
        Grid.SetRow(Table, 1);
        content.Children.Add(Table);
        commitMessage.Attach(content);

        var summary = new TextBlock { FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary }.WithFeedbackId("PanelView.summary");
        footer = new PanelFooter(summary, fileTable.LogPresentation);
        summary.VerticalAlignment = VerticalAlignment.Center;
        summary.TextTrimming = TextTrimming.CharacterEllipsis;
        summary.HorizontalAlignment = HorizontalAlignment.Right;
        summary.Bind(ToolTip.TipProperty, new Binding(nameof(TextBlock.Text)) { Source = summary });
        var summaryContainer = new Border
        {
            Name = id + "Footer",
            Padding = new Thickness(8, 0),
            Child = summary,
            Background = Background,
            BorderBrush = UiPalette.Divider,
            BorderThickness = new Thickness(0, 1, 0, 0),
            ClipToBounds = true
        }.WithFeedbackId("PanelView.summaryContainer");
        AutomationProperties.SetAutomationId(summary, id + ".summary");
        Grid.SetRow(summaryContainer, 3);
        content.Children.Add(summaryContainer);
        Child = content;

        fileTable.Activated += Activate;
        fileTable.RowFocused += rename.CancelOnFocusChange;
        fileTable.OpenRequested += () => { if (EditingRow == null) OpenRequested?.Invoke(this); };
        fileTable.SortRequested += SortBy;
        fileTable.RowsPublished += ApplyGitStatus;
        fileTable.StateChanged += UpdateSummary;
        Git.StatusChanged += ApplyGitStatus;
        header.HistoryRequested += () => { Activate(); HistoryRequested?.Invoke(this); };
        shellInput.MenuRequested += (row, point, extended) => ShellMenuRequested?.Invoke(this, row, point, extended);
        loader.LoadingRequested += () => LoadingRequested?.Invoke(this);
        loader.DirectoryLoaded += ApplyDirectoryLoad;
        loader.StateChanged += UpdateSummary;
        stateRestore.StateChanged += UpdateSummary;
        fileTable.UpdateSort(Session.SortOrder);
    }

    private void Activate() => Activated?.Invoke(this);

    public PanelState CaptureState() => stateRestore.CaptureState();
    private PanelReturnPoint CaptureReturnPoint() => stateRestore.CaptureReturnPoint();
    public Task<bool> RestoreStateAsync(PanelState state) => stateRestore.RestoreStateAsync(state);
    public void RestoreScroll(PanelState state) => stateRestore.RestoreScroll(state);

    public Task<bool> LoadAsync(string directory, bool preserveSelection = false, string? selectName = null,
        CancellationToken cancellationToken = default, bool nearestExistingParent = false) =>
        loader.LoadAsync(directory, preserveSelection, selectName, cancellationToken, nearestExistingParent);

    public Task<bool> LoadAccessibleAsync(string directory) => loader.LoadAccessibleAsync(directory);

    public Task<bool> RefreshAsync(Func<bool> canApply, Func<bool> canFocus) => IsSearchResults
        ? search.RefreshAsync(canApply) : loader.RefreshDirectoryAsync(canApply, canFocus);

    private void ApplyDirectoryLoad(string previousPath, string[] selected, string? selectName)
    {
        search.Close();
        panelRows.ClearMeasurements();
        loader.ShowRows(selected, selectName);
        header.ShowDirectory(Session);
        fileTable.SetAccessibleName("Files in " + Session.DirectoryPath);
        if (!previousPath.Equals(Session.DirectoryPath, StringComparison.OrdinalIgnoreCase)) LocationVisited?.Invoke(Session.DirectoryPath);
        UpdateSummary();
    }

    public bool HasHistory => history.IsOpen;
    public Task<bool> OpenHistoryAsync(string location, CancellationToken cancellationToken = default) => history.OpenAsync(location, cancellationToken);
    public Task ExitHistoryAsync() => history.ExitAsync();
    public Task BackHistoryAsync(CancellationToken cancellationToken = default) => history.BackAsync(cancellationToken);

    public bool IsSearchResults => search.IsOpen;
    public string OperationDirectory => search.OperationDirectory;
    public void ShowSearchResults(string root, IReadOnlyList<FileSearchResult> results, string? selectedPath = null) =>
        search.Show(root, results, selectedPath);
    public Task ExitSearchResultsAsync() => search.ExitAsync(stateRestore.RestoreReturnPointAsync);

    public void SortBy(SortField field)
    {
        CancelRename();
        var selected = SelectedNames();
        var current = Current?.Name;
        Session.SortBy(field);
        if (IsSearchResults) search.ShowRows(selected, current);
        else loader.ShowRows(selected, current);
        fileTable.UpdateSort(Session.SortOrder);
    }

    public void ResetColumns() => fileTable.Columns.Reset();

    public string[] SelectedNames() => Selection.SelectedNames();
    public void FocusCurrent(bool scrollIntoView = true) => Selection.FocusCurrent(scrollIntoView);
    public void SelectBoundary(bool last, bool extend) => Selection.SelectBoundary(last, extend);
    public void SelectRow(FileRow row) => Selection.SelectRow(row);
    public void MovePage(bool down, bool extend) => Selection.MovePage(down, extend);
    public void MoveCursor(bool down, bool mark) => Selection.MoveCursor(down, mark);
    public void ClearMarks() => Selection.ClearMarks();

    public TextBox? RenameEditor => rename.Editor;
    public void BeginRename() => rename.Begin();
    public void FocusRenameEditor() => rename.FocusEditor();
    public void CancelRename() => rename.Cancel();
    public Task<FileOperationError?> CommitRenameAsync() => rename.CommitAsync();

    private async Task ApplyRenameAsync(FileEntry entry, string name)
    {
        search.Rename(entry, name);
        if (IsSearchResults) await search.RefreshAsync(() => true);
        else await LoadAsync(Session.DirectoryPath, selectName: name);
    }

    internal void CancelShellPress() => shellInput.CancelPress();
    internal void FocusDragRow(FileRow row) => Selection.FocusPointerRow(row);
    internal void SelectClickedRow(FileRow row, bool toggle) => Selection.SelectClickedRow(row, toggle);

    public TextBox CommitMessage => commitMessage.Editor;
    public bool IsCommitMessageLoading => commitMessage.IsLoading;
    public bool IsCommitMessageVisible => commitMessage.IsVisible;
    public bool HasCommitMessageFocus => commitMessage.IsKeyboardFocusWithin;
    public void HandleCommitMessageKey(KeyEventArgs e) => commitMessage.HandleKey(e, () => FocusCurrent());

    public void SetActive(bool active)
    {
        Selection.SetActive(active);
        BorderBrush = active ? UiPalette.Focus : Brushes.Transparent;
    }

    public void SetCutPaths(IEnumerable<string> paths) => panelRows.SetCutPaths(paths);
    public void SetFileTypes(FileTypes types) => panelRows.SetFileTypes(types);
    public void ShowNameMatches(NameJump? search) => panelRows.ShowNameMatches(search);

    public void SetDirectorySize(DirectoryMeasurement measurement)
    {
        if (panelRows.SetMeasurement(measurement)) UpdateSummary();
    }

    private void ApplyGitStatus() => fileTable.Columns.ShowGit(gitRows.Update(Rows, Git, Session.DirectoryPath, IsSearchResults));

    private void UpdateSummary()
    {
        var previous = Selection.UpdateHighlights();
        footer.Update(Session, Rows, SelectedNames(), IsSearchResults);
        commitMessage.Update(Session, Current?.Entry);
        if (Selection.CurrentEntryChanged(previous)) CurrentEntryChanged?.Invoke();
    }
}

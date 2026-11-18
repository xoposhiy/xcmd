// @implements NavigatePanels, SortEntries, JumpToName, ReadablePanels, RefreshEntries, MarkEntries, MeasureFolders, ViewSelectionSize, RenameEntry, SearchResultPanel, QuickViewFile, BrowseHistory, RestoreWorkspace, RecentLocations
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class PanelView : Border
{
    private readonly TextBlock path = new TextBlock() { FontSize = UiPalette.BodySize, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.LeadingCharacterEllipsis }.WithFeedbackId("PanelView.path");
    private readonly TextBlock summary = new TextBlock() { FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary }.WithFeedbackId("PanelView.summary");
    private readonly Dictionary<SortField, Button> headers = [];
    private bool headersConfigured;
    private readonly PanelContentLoader loader;
    private readonly PanelSelection selection;
    private readonly PanelRows panelRows;
    private readonly PanelFooter footer;
    private readonly PanelRename rename;
    private readonly PanelReturnLocations returnLocations = new();
    private readonly FilePanelColumns columns;
    public void SetCutPaths(IEnumerable<string> paths) => panelRows.SetCutPaths(paths);
    public PanelSession Session { get; }
    public string DisplayPath => PathText.Display(Session.FileSystem is GitFileSystem git ? git.Display(Session.DirectoryPath) : Session.DirectoryPath);
    public TableView Table { get; }
    public List<FileRow> Rows => panelRows.Items;
    public FileRow? EditingRow => rename.EditingRow;
    public bool IsCommitting => rename.IsCommitting;
    public FileRow? Current => selection.Current;
    public string Id { get; }
    public event Action<PanelView>? Activated;
    public event Action<PanelView>? OpenRequested;
    public event Action<PanelView>? HistoryRequested;
    public event Action<string>? LocationVisited;
    public event Action<PanelView>? LoadingRequested;
    internal event Action? CurrentEntryChanged;
    public FileIconCache Icons { get; }
    public GitStatusView Git { get; }
    public void ShowNameMatches(NameJump? search) => panelRows.ShowNameMatches(search);
    public static string HomeDirectory => UserDirectories.Home;

    public PanelState CaptureState()
    {
        if (HasHistory && returnLocations.History is { } saved) return saved.State;
        if (IsSearchResults) return CaptureSearchState();
        Session.RememberCursor(Current?.Name);
        var offset = Scroll?.Offset ?? default;
        return new(Session.DirectoryPath, Session.SortOrder, Current?.Name,
            CaptureRowSelection(),
            Session.PreviousDirectories.ToArray(), Session.CaptureCursors(),
            columns.Capture(), offset.X, offset.Y, panelRows.CaptureMeasurements(), selection.HasMarks);
    }

    public async Task<bool> RestoreStateAsync(PanelState state)
    {
        if (!await LoadAccessibleAsync(state.DirectoryPath)) return false;
        Session.RestoreNavigation(state);
        if (Session.DirectoryPath.Equals(state.DirectoryPath, StringComparison.OrdinalIgnoreCase)) panelRows.RestoreMeasurements(state.Sizes);
        selection.RestoreMarkMode(state);
        ApplyEntries(state.Selected, state.Cursor);
        selection.RestoreEmptySelection(state.Selected);
        columns.Restore(state.Columns);
        UpdateHeaders();
        UpdateSummary();
        if (state.SearchResults is { } results) RestoreSearchState(results);
        return true;
    }

    public void RestoreScroll(PanelState state)
    {
        Table.UpdateLayout();
        if (Scroll is { } scroll) scroll.Offset = state.SearchResults is { } results ? new Vector(results.ScrollX, results.ScrollY) : new Vector(state.ScrollX, state.ScrollY);
    }

    internal PanelReturnPoint CaptureReturnPoint() => new(CaptureState(), Table.Bounds.Width);

    private async Task RestoreTemporaryViewAsync(PanelReturnPoint saved)
    {
        if (!await RestoreStateAsync(saved.State)) return;
        columns.FitNameAfterResize(saved.Width);
        RestoreScroll(saved.State);
        FocusCurrent(scrollIntoView: false);
    }

    private ScrollViewer? Scroll => Table.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    public Task<bool> LoadAccessibleAsync(string directory) => loader.LoadAccessibleAsync(directory);

    public Task<bool> RefreshAsync(Func<bool> canApply, Func<bool> canFocus) => IsSearchResults
        ? RefreshSearchResultsAsync(canApply) : loader.RefreshDirectoryAsync(canApply, canFocus);

    internal void ApplyDirectoryRefresh()
    {
        if (Rows.Select(row => row.Entry).SequenceEqual(Session.Entries)) UpdateSummary();
        else ApplyRefreshedEntries();
    }

    private void ApplyRefreshedEntries()
    {
        panelRows.RetainMeasurements(Session.Entries);
        var selected = CaptureRowSelection();
        var current = Current?.Name;
        var index = Current == null ? 0 : Rows.IndexOf(Current);
        var focused = Table.IsKeyboardFocusWithin;
        var offset = Scroll?.Offset ?? default;
        ApplyEntries(selected, current);
        selection.RestoreRefreshCursor(current, index, selected.Length > 0);
        Table.UpdateLayout();
        if (Scroll is { } scroll) scroll.Offset = offset;
        Table.UpdateLayout();
        if (focused) FocusCurrent(scrollIntoView: false);
        if (Scroll is { } restored) restored.Offset = offset;
        UpdateSummary();
    }

    public PanelView(string id, IFileSystem fileSystem, FileTypes? fileTypes = null)
        : this(id, fileSystem, fileTypes, null) { }

    public PanelView(string id, IFileSystem fileSystem, FileTypes? fileTypes, IGitStatusReader? gitReader)
    {
        panelRows = new PanelRows(fileTypes ?? FileTypeSettings.Bundled());
        Id = id;
        Icons = new FileIconCache(this);
        Git = new GitStatusView(id, gitReader).WithFeedbackId("PanelView.Git");
        Name = id;
        Session = new PanelSession(fileSystem);
        loader = new PanelContentLoader(this);
        Background = UiPalette.Surface;
        BorderBrush = UiPalette.Divider;
        BorderThickness = new Thickness(0, 2, 0, 0);
        CornerRadius = new CornerRadius(0);
        ClipToBounds = true;
        DetachedFromVisualTree += (_, _) => Icons.Dispose();
        Table = new TableView
        {
            Name = id + "Files",
            SelectionMode = SelectionMode.Multiple,
            ClipToBounds = true,
            AutoScrollToSelectedItem = false,
            Focusable = true,
            FontSize = UiPalette.BodySize,
            Background = Brushes.Transparent
        }.WithFeedbackId("PanelView.Table");
        selection = new PanelSelection(Table, () => Rows, UpdateSummary);
        rename = new PanelRename(Table, Session.FileSystem, () => Current, ApplyRenameAsync);
        AutomationProperties.SetAutomationId(Table, id + ".files");
        Table.IsTextSearchEnabled = false;
        ScrollViewer.SetBringIntoViewOnFocusChange(Table, false);
        ScrollViewer.SetAllowAutoHide(Table, false);
        ScrollViewer.SetHorizontalScrollBarVisibility(Table, ScrollBarVisibility.Auto);
        Table.Resources["TableViewColumnResizerMargin"] = new Thickness(0);
        Table.Resources["TableViewRowPadding"] = new Thickness(0);
        Table.ItemContainerTheme = FileRowTheme.Create();
        UiLayout.Table(Table, fileRows: true);
        Table.Columns.Add(Column(SortField.Name, "Name", new GridLength(1, GridUnitType.Star), name: true));
        Table.Columns.Add(Column(SortField.Extension, "Type", new GridLength(64)));
        Table.Columns.Add(Column(SortField.Size, "Size", new GridLength(88)));
        Table.Columns.Add(MakeGitColumn());
        Table.Columns.Add(Column(SortField.Modified, "Modified", new GridLength(145)));
        logPresentation = new GitLogPresentation(Table);
        footer = new PanelFooter(summary, logPresentation);
        columns = new FilePanelColumns(Table, logPresentation);
        Git.StatusChanged += ApplyGitStatus;
        ResetColumns();
        Table.Loaded += (_, _) => ConfigureHeaders();
        Table.TemplateApplied += (_, _) => headersConfigured = false;
        Table.LayoutUpdated += (_, _) => { if (!headersConfigured) ConfigureHeaders(); };
        Table.SelectionChanged += (_, _) => UpdateSummary();
        Table.GotFocus += (_, e) =>
        {
            if (e.Source is Control control)
            {
                var row = control.GetSelfAndVisualAncestors().OfType<TableViewRow>().FirstOrDefault();
                if (row?.DataContext is FileRow file)
                {
                    rename.CancelOnFocusChange(file);
                    selection.SetCursor(file);
                    UpdateSummary();
                }
            }
            Activated?.Invoke(this);
        };
        Table.PointerPressed += (_, e) =>
        {
            if (e.Source is Control control && control.GetSelfAndVisualAncestors().OfType<TableViewRow>().Any())
            {
                selection.BeginPointerSelection(e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                UpdateSummary();
            }
            Activated?.Invoke(this);
        };
        Table.DoubleTapped += (_, e) =>
        {
            if (EditingRow == null && e.Source is Control control && control.GetSelfAndVisualAncestors().OfType<TableViewRow>().Any())
                OpenRequested?.Invoke(this);
        };
        AttachShellInput();
        var content = new Grid { RowDefinitions = new RowDefinitions("34,*,0,29") }.WithFeedbackId("PanelView.content");
        var historyButton = new Button
        {
            Content = "History",
            Name = id + "History",
            FontSize = UiPalette.MetaSize,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = UiPalette.Secondary,
            Padding = new Thickness(3, 4),
            Margin = new Thickness(8, 0, 0, 0)
        }.WithFeedbackId("PanelView.historyButton");
        historyButton.Classes.Add("quiet");
        AutomationProperties.SetAutomationId(historyButton, id + ".history");
        ToolTip.SetTip(historyButton, "Directory history (Alt+Down)");
        historyButton.Click += (_, _) => { Activated?.Invoke(this); HistoryRequested?.Invoke(this); };
        var pathHeader = new Grid { Name = id + "LocationHeader", ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 8 }.WithFeedbackId("PanelView.pathHeader");
        pathHeader.Children.Add(path);
        Grid.SetColumn(Git, 1);
        pathHeader.Children.Add(Git);
        Grid.SetColumn(historyButton, 3);
        pathHeader.Children.Add(historyButton);
        void FitLocationHeader()
        {
            Git.MaxWidth = Math.Clamp(pathHeader.Bounds.Width * 0.5, 120, 300);
            path.MaxWidth = Math.Max(60, pathHeader.Bounds.Width - Git.DesiredSize.Width - historyButton.DesiredSize.Width - 24);
        }
        pathHeader.SizeChanged += (_, _) => FitLocationHeader();
        Git.SizeChanged += (_, _) => FitLocationHeader();
        path.VerticalAlignment = VerticalAlignment.Center;
        var pathContainer = new Border { Padding = new Thickness(8, 0), Child = pathHeader, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("PanelView.pathContainer");
        content.Children.Add(pathContainer);
        Grid.SetRow(Table, 1);
        content.Children.Add(Table);
        BuildCommitMessageArea(content);
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
        UpdateHeaders();
    }

    public void ResetColumns() => columns.Reset();

    private void ConfigureHeaders()
    {
        foreach (var viewer in Table.GetVisualDescendants().OfType<ScrollViewer>()) viewer.AllowAutoHide = false;
        foreach (var presenter in Table.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ScrollContentPresenter>()) presenter.ClipToBounds = true;
        columns.ClipHeaders();
        var headers = Table.GetVisualDescendants().OfType<TableViewColumnHeader>().ToArray();
        if (headers.Length < Table.Columns.Count(column => column.IsVisible)) return;
        headersConfigured = true;
        foreach (var header in headers)
        {
            header.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            header.VerticalContentAlignment = VerticalAlignment.Stretch;
            header.VerticalAlignment = VerticalAlignment.Stretch;
            header.Padding = new Thickness(0); header.MinHeight = UiPalette.RowHeight;
        }
    }

    private TableViewColumn Column(SortField field, string title, GridLength width, bool name = false)
    {
        var button = new Button
        {
            Content = title,
            Name = Id + "Sort" + field,
            Focusable = false,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = field == SortField.Size ? HorizontalAlignment.Right : field == SortField.Extension ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            FontSize = UiPalette.BodySize,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiPalette.Text,
            MinHeight = UiPalette.RowHeight
        }.WithFeedbackId("PanelView.button");
        AutomationProperties.SetAutomationId(button, Id + ".sort." + field.ToString().ToLowerInvariant());
        button.Click += (_, _) => { Activated?.Invoke(this); SortBy(field); FocusCurrent(); };
        headers[field] = button;
        var column = new TableViewColumn { Header = button, Width = width, MinWidth = name ? 90 : 45 };
        if (name)
            column.CellTemplate = new FuncDataTemplate<FileRow>((row, _) => row == null ? new Border().WithFeedbackId("PanelView.empty") : FileNameCell.Create(row, Icons));
        else column.CellTemplate = new FuncDataTemplate<FileRow>((row, _) => MakeValueCell(row, field));
        if (field == SortField.Size) column.HorizontalContentAlignment = HorizontalAlignment.Right;
        if (field == SortField.Extension) column.HorizontalContentAlignment = HorizontalAlignment.Center;
        return column;
    }

    private static Control MakeValueCell(FileRow? row, SortField field)
    {
        var text = new TextBlock
        {
            DataContext = row,
            Text = row == null ? "" : field switch { SortField.Extension => row.Kind, SortField.Size => row.DisplaySize, _ => row.DisplayDate },
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = field == SortField.Size ? HorizontalAlignment.Right : field == SortField.Extension ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis
        }.WithFeedbackId("PanelView.text");
        if (field == SortField.Size)
        { text.Bind(TextBlock.TextProperty, new Binding(nameof(FileRow.DisplaySize))); text.Bind(ToolTip.TipProperty, new Binding(nameof(FileRow.SizeDescription))); }
        if (field == SortField.Extension)
        {
            text.Bind(TextBlock.TextProperty, new Binding(nameof(FileRow.Kind)));
            text.Bind(ToolTip.TipProperty, new Binding(nameof(FileRow.Kind)));
            text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(FileRow.TypeForeground)));
        }
        return text;
    }

    public Task<bool> LoadAsync(string directory, bool preserveSelection = false, string? selectName = null,
        CancellationToken cancellationToken = default, bool nearestExistingParent = false) =>
        loader.LoadAsync(directory, preserveSelection, selectName, cancellationToken, nearestExistingParent);

    internal void PrepareDirectoryLoad(bool preserveSelection)
    {
        LoadingRequested?.Invoke(this);
        CancelRename();
        if (!preserveSelection) selection.SetKeyboardMarks(false);
        if (!IsSearchResults) Session.RememberCursor(Current?.Name);
    }

    internal void ApplyDirectoryLoad(string previousPath, PanelReturnPoint? gitReturn, string[] selected, string? selectName)
    {
        returnLocations.ApplyDirectory(HasHistory, gitReturn);
        searchResults = null;
        panelRows.ClearMeasurements();
        ApplyEntries(selected, selectName);
        path.Text = DisplayPath;
        path.TextTrimming = TextTrimming.LeadingCharacterEllipsis;
        ToolTip.SetTip(path, path.Text);
        AutomationProperties.SetName(Table, "Files in " + Session.DirectoryPath);
        if (!previousPath.Equals(Session.DirectoryPath, StringComparison.OrdinalIgnoreCase)) LocationVisited?.Invoke(Session.DirectoryPath);
        UpdateSummary();
    }

    public void SortBy(SortField field)
    {
        CancelRename();
        var selected = SelectedNames();
        var current = Current?.Name;
        Session.SortBy(field);
        ApplyEntries(selected, current);
        UpdateHeaders();
    }

    private void ApplyEntries(string[] selected, string? current)
    {
        selection.ResetRange();
        ConfigureLogPresentation();
        panelRows.Replace(ResultRows(), showMeasurements: !IsSearchResults);
        Table.ItemsSource = Rows;
        ApplyGitStatus();
        columns.UpdateRows(Rows);
        panelRows.RefreshNameMatches();
        selection.ApplyRows(selected, current);
        UpdateSummary();
    }

    public string[] SelectedNames() => selection.SelectedNames();
    private string[] CaptureRowSelection() => selection.CaptureRowSelection();
    public void FocusCurrent(bool scrollIntoView = true) => selection.FocusCurrent(scrollIntoView);
    public void SelectBoundary(bool last, bool extend) => selection.SelectBoundary(last, extend);
    public void SelectRow(FileRow row) => selection.SelectRow(row);
    public void MovePage(bool down, bool extend) => selection.MovePage(down, extend);
    public void MoveCursor(bool down, bool mark) => selection.MoveCursor(down, mark);
    public void ClearMarks() => selection.ClearMarks();

    public TextBox? RenameEditor => rename.Editor;
    public void BeginRename() => rename.Begin();
    public void FocusRenameEditor() => rename.FocusEditor();
    public void CancelRename() => rename.Cancel();
    public Task<FileOperationError?> CommitRenameAsync() => rename.CommitAsync();

    private async Task ApplyRenameAsync(FileEntry entry, string name)
    {
        RenameSearchResult(entry, name);
        if (IsSearchResults) await RefreshSearchResultsAsync(() => true);
        else await LoadAsync(Session.DirectoryPath, selectName: name);
    }

    public void SetActive(bool active)
    {
        selection.SetActive(active);
        BorderBrush = active ? UiPalette.Focus : Brushes.Transparent;
    }

    private void UpdateHeaders()
    {
        foreach (var (field, button) in headers)
        {
            var title = field switch { SortField.Extension => "Type", SortField.Modified => "Modified", _ => field.ToString() };
            button.Content = title + (Session.SortOrder.Field == field ? Session.SortOrder.Descending ? "  ↓" : "  ↑" : "");
        }
    }

    public void SetFileTypes(FileTypes types) => panelRows.SetFileTypes(types);
    public void SetDirectorySize(DirectoryMeasurement measurement)
    {
        if (panelRows.SetMeasurement(measurement)) UpdateSummary();
    }

    internal void UpdateSummary()
    {
        var previous = selection.UpdateHighlights();
        footer.Update(Session, Rows, SelectedNames(), IsSearchResults);
        UpdateCommitMessage();
        if (selection.CurrentEntryChanged(previous)) CurrentEntryChanged?.Invoke();
    }
}

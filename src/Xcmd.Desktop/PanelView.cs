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
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed class PanelView : Border
{
    private readonly TextBlock path = new() { FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock summary = new() { FontSize = 12, Foreground = Brushes.LightGray };
    private readonly Dictionary<SortField, Button> headers = [];
    private string? cursorName;
    private bool headersConfigured;
    private FileTypes fileTypes;
    private HashSet<FileRow> selectedRows = [];
    private readonly Dictionary<string, DirectoryMeasurement> sizes = new(StringComparer.OrdinalIgnoreCase);
    public PanelSession Session { get; }
    public TableView Table { get; }
    public List<FileRow> Rows { get; private set; } = [];
    public FileRow? EditingRow { get; private set; }
    public bool IsCommitting { get; private set; }
    public FileRow? Current => Rows.FirstOrDefault(row => row.Name == cursorName) ?? Table.SelectedItem as FileRow;
    public string Id { get; }
    public event Action<PanelView>? Activated;
    public event Action<PanelView>? OpenRequested;
    public event Action<PanelView>? HistoryRequested;
    public event Action<string>? LocationVisited;
    public event Action<PanelView>? LoadingRequested;
    public FileIconCache Icons { get; } = new();

    public PanelState CaptureState()
    {
        Session.RememberCursor(Current?.Name);
        var offset = Scroll?.Offset ?? default;
        return new(Session.DirectoryPath, Session.SortOrder, Current?.Name,
            CaptureRowSelection(),
            Session.PreviousDirectories.ToArray(), Session.CaptureCursors(),
            Table.Columns.Select(column => new ColumnState(column.Width.Value, column.Width.IsStar)).ToArray(), offset.X, offset.Y, sizes.Values.ToArray());
    }

    public async Task RestoreStateAsync(PanelState state)
    {
        await LoadAccessibleAsync(state.DirectoryPath);
        Session.RestoreNavigation(state);
        if (Session.DirectoryPath.Equals(state.DirectoryPath, StringComparison.OrdinalIgnoreCase))
            foreach (var measurement in state.Sizes ?? []) sizes[measurement.Name] = measurement;
        ApplyEntries(state.Selected, state.Cursor);
        if (state.Selected.Length == 0) Table.SelectedItems?.Clear();
        for (var i = 0; i < Math.Min(Table.Columns.Count, state.Columns.Length); i++)
            Table.Columns[i].Width = new GridLength(state.Columns[i].Width, state.Columns[i].Star ? GridUnitType.Star : GridUnitType.Pixel);
        Table.Columns[^1].Width = new GridLength(1, GridUnitType.Star);
        UpdateHeaders();
        UpdateSummary();
    }

    public void RestoreScroll(PanelState state)
    {
        Table.UpdateLayout();
        if (Scroll is { } scroll) scroll.Offset = new Vector(state.ScrollX, state.ScrollY);
    }

    private ScrollViewer? Scroll => Table.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    public async Task<bool> LoadAccessibleAsync(string directory)
    {
        while (!await LoadAsync(directory))
        {
            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory));
            if (parent == null || Session.Error == null) return false;
            directory = parent;
        }
        return true;
    }

    public async Task<bool> RefreshAsync(Func<bool> canApply, Func<bool> canFocus)
    {
        var directory = Session.DirectoryPath;
        var hadFocus = Table.IsKeyboardFocusWithin;
        if (!await Session.LoadAsync(directory))
        {
            return await RecoverDirectoryAsync(directory, hadFocus, canApply, canFocus);
        }
        if (!canApply() || EditingRow != null || Session.DirectoryPath != directory) return false;
        ApplyRefreshedEntries();
        return true;
    }

    private void ApplyRefreshedEntries()
    {
        if (!Rows.Select(row => row.Entry).SequenceEqual(Session.Entries)) sizes.Clear();
        var selected = CaptureRowSelection();
        var current = Current?.Name;
        var index = Current == null ? 0 : Rows.IndexOf(Current);
        var focused = Table.IsKeyboardFocusWithin;
        var offset = Scroll?.Offset ?? default;
        ApplyEntries(selected, current);
        RestoreRefreshCursorAndSelection(current, index, selected.Length > 0);
        if (focused) FocusCurrent();
        Table.UpdateLayout();
        if (Scroll is { } scroll) scroll.Offset = offset;
        UpdateSummary();
    }

    private async Task<bool> RecoverDirectoryAsync(string directory, bool hadFocus, Func<bool> canApply, Func<bool> canFocus)
    {
        if (Session.Error == null || Session.DirectoryPath != directory || !canApply()) return false;
        var loaded = await LoadAccessibleAsync(directory);
        if (loaded && hadFocus && canApply() && canFocus()) FocusCurrent();
        return loaded;
    }

    private void RestoreRefreshCursorAndSelection(string? current, int index, bool hadSelection)
    {
        if (!Rows.Any(row => row.Name == current) && Rows.Count > 0)
        {
            var next = Rows[Math.Clamp(index, 0, Rows.Count - 1)]; cursorName = next.Name;
            if (Table.SelectedItems?.Count == 0 && hadSelection) Table.SelectedItem = next;
        }
        if (!hadSelection) Table.SelectedItems?.Clear();
    }

    public PanelView(string id, IFileSystem fileSystem, FileTypes? fileTypes = null)
    {
        this.fileTypes = fileTypes ?? FileTypeSettings.Bundled();
        Id = id;
        Name = id;
        Session = new PanelSession(fileSystem);
        Background = new SolidColorBrush(Color.Parse("#171E2B"));
        BorderBrush = new SolidColorBrush(Color.Parse("#354157"));
        BorderThickness = new Thickness(0, 2, 0, 0);
        CornerRadius = new CornerRadius(0);
        ClipToBounds = true;
        DetachedFromVisualTree += (_, _) => Icons.Dispose();
        Table = new TableView
        {
            Name = id + "Files",
            SelectionMode = SelectionMode.Multiple,
            Focusable = true,
            FontSize = 13,
            Background = Brushes.Transparent
        };
        AutomationProperties.SetAutomationId(Table, id + ".files");
        Table.IsTextSearchEnabled = false;
        Table.Resources["TableViewColumnResizerMargin"] = new Thickness(0);
        Table.Resources["TableViewRowPadding"] = new Thickness(0);
        Table.Styles.Add(new Style(selector => selector.OfType<TableViewRow>())
        {
            Setters = { new Setter(MinHeightProperty, 26d), new Setter(PaddingProperty, new Thickness(0)) }
        });
        Table.Styles.Add(new Style(selector => selector.OfType<TableViewCell>())
        { Setters = { new Setter(PaddingProperty, new Thickness(8, 0)) } });
        Table.Styles.Add(new Style(selector => selector.OfType<TableViewColumnHeader>())
        {
            Setters =
            {
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Stretch),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(0))
            }
        });
        Table.Columns.Add(Column(SortField.Name, "Name", new GridLength(1, GridUnitType.Star), name: true));
        Table.Columns.Add(Column(SortField.Extension, "Type", new GridLength(64)));
        Table.Columns.Add(Column(SortField.Size, "Size", new GridLength(88)));
        Table.Columns.Add(Column(SortField.Modified, "Modified", new GridLength(145)));
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
                    if (EditingRow != null && file != EditingRow && !IsCommitting) CancelRename();
                    cursorName = file.Name;
                }
            }
            Activated?.Invoke(this);
        };
        Table.PointerPressed += (_, _) => Activated?.Invoke(this);
        Table.DoubleTapped += (_, e) =>
        {
            if (EditingRow == null && e.Source is Control control && control.GetSelfAndVisualAncestors().OfType<TableViewRow>().Any())
                OpenRequested?.Invoke(this);
        };
        var content = new Grid { RowDefinitions = new RowDefinitions("34,*,24") };
        var historyButton = new Button
        {
            Content = "History",
            Name = id + "History",
            FontSize = 12,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.Parse("#8792A4")),
            Padding = new Thickness(3, 4),
            Margin = new Thickness(8, 0, 0, 0)
        };
        AutomationProperties.SetAutomationId(historyButton, id + ".history");
        ToolTip.SetTip(historyButton, "Directory history (Alt+Down)");
        historyButton.Click += (_, _) => { Activated?.Invoke(this); HistoryRequested?.Invoke(this); };
        var pathHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        pathHeader.Children.Add(path);
        Grid.SetColumn(historyButton, 1);
        pathHeader.Children.Add(historyButton);
        path.VerticalAlignment = VerticalAlignment.Center;
        var pathContainer = new Border { Padding = new Thickness(8, 0), Child = pathHeader, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(pathContainer);
        Grid.SetRow(Table, 1);
        content.Children.Add(Table);
        var summaryContainer = new Border { Padding = new Thickness(8, 2), Child = summary };
        Grid.SetRow(summaryContainer, 2);
        content.Children.Add(summaryContainer);
        Child = content;
        UpdateHeaders();
    }

    public void ResetColumns()
    {
        headersConfigured = false;
        Table.Columns[0].Width = new GridLength(3, GridUnitType.Star);
        Table.Columns[1].Width = new GridLength(64);
        Table.Columns[2].Width = new GridLength(128);
        Table.Columns[2].MinWidth = 112;
        Table.Columns[3].Width = new GridLength(1, GridUnitType.Star);
        Table.Columns[3].MinWidth = 150;
        Table.Columns[3].CanUserResize = false;
        ConfigureHeaders();
    }

    private void ConfigureHeaders()
    {
        var headers = Table.GetVisualDescendants().OfType<TableViewColumnHeader>().ToArray();
        if (headers.Length < Table.Columns.Count) return;
        headersConfigured = true;
        foreach (var header in headers)
        {
            header.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            header.VerticalContentAlignment = VerticalAlignment.Stretch;
            header.VerticalAlignment = VerticalAlignment.Stretch;
            header.Padding = new Thickness(0); header.MinHeight = 32;
            if (header.Column == Table.Columns[^1])
                foreach (var thumb in header.GetVisualDescendants().OfType<Thumb>()) thumb.IsVisible = false;
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
            HorizontalContentAlignment = field == SortField.Size ? HorizontalAlignment.Right : field == SortField.Extension ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            FontSize = 12
        };
        AutomationProperties.SetAutomationId(button, Id + ".sort." + field.ToString().ToLowerInvariant());
        button.Click += (_, _) => { Activated?.Invoke(this); SortBy(field); FocusCurrent(); };
        headers[field] = button;
        var column = new TableViewColumn { Header = button, Width = width, MinWidth = name ? 90 : 45 };
        if (name)
            column.CellTemplate = new FuncDataTemplate<FileRow>((row, _) => row == null ? new Border() : MakeNameCell(row));
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
        };
        if (field == SortField.Size)
        { text.Bind(TextBlock.TextProperty, new Binding(nameof(FileRow.DisplaySize))); text.Bind(ToolTip.TipProperty, new Binding(nameof(FileRow.SizeDescription))); }
        return text;
    }

    private Control MakeNameCell(FileRow row)
    {
        var grid = new Grid { DataContext = row, MinHeight = 26, ColumnDefinitions = new ColumnDefinitions("22,*") };
        var icon = new Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, DataContext = row };
        AutomationProperties.SetAutomationId(icon, "file.icon");
        grid.Children.Add(icon); Icons.Bind(icon, row);
        var text = new TextBlock
        {
            Text = row.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = row.Entry.IsDirectory ? FontWeight.SemiBold : FontWeight.Normal
        };
        text.Bind(IsVisibleProperty, new Binding(nameof(FileRow.IsReading)));
        text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(FileRow.NameForeground)));
        var editor = new TextBox
        {
            Name = "RenameEditor",
            MinHeight = 26,
            Padding = new Thickness(3, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 13,
            MaxLength = 255
        };
        AutomationProperties.SetAutomationId(editor, "rename.editor");
        editor.Bind(IsVisibleProperty, new Binding(nameof(FileRow.IsEditing)));
        editor.Bind(TextBox.TextProperty, new Binding(nameof(FileRow.DraftName)) { Mode = BindingMode.TwoWay });
        // Unhandled editor keys must not reach the table's row navigation.
        editor.KeyDown += (_, e) => e.Handled = true;
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        Grid.SetColumn(editor, 1); grid.Children.Add(editor);
        return grid;
    }

    public async Task<bool> LoadAsync(string directory, bool preserveSelection = false, string? selectName = null)
    {
        LoadingRequested?.Invoke(this);
        CancelRename();
        var selected = preserveSelection ? SelectedNames() : [];
        Session.RememberCursor(Current?.Name);
        var previousPath = Session.DirectoryPath;
        var loaded = await Session.LoadAsync(directory);
        if (loaded)
        {
            sizes.Clear();
            ApplyEntries(selected, selectName ?? (preserveSelection ? Current?.Name : Session.RememberedCursor));
            path.Text = Session.DirectoryPath;
            AutomationProperties.SetName(Table, "Files in " + Session.DirectoryPath);
            if (!previousPath.Equals(Session.DirectoryPath, StringComparison.OrdinalIgnoreCase)) LocationVisited?.Invoke(Session.DirectoryPath);
        }
        UpdateSummary();
        return loaded;
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
        Rows = Session.Entries.Select(entry => { var row = new FileRow(entry); ApplyFileColor(row); row.SetMeasurement(sizes.GetValueOrDefault(entry.Name)); return row; }).ToList();
        Table.ItemsSource = Rows;
        Table.SelectedItems?.Clear();
        foreach (var row in Rows.Where(row => selected.Contains(row.Name, StringComparer.OrdinalIgnoreCase)))
            Table.SelectedItems?.Add(row);
        var cursor = Rows.FirstOrDefault(row => row.Name.Equals(current, StringComparison.OrdinalIgnoreCase));
        if (Table.SelectedItems?.Count == 0) Table.SelectedItem = cursor ?? Rows.FirstOrDefault();
        cursorName = (cursor ?? Table.SelectedItem as FileRow)?.Name;
        UpdateSummary();
    }

    public string[] SelectedNames() => Table.SelectedItems?.OfType<FileRow>().Where(row => !row.Entry.IsParent).Select(row => row.Name).ToArray() ?? [];

    private string[] CaptureRowSelection() => Table.SelectedItems?.OfType<FileRow>().Select(row => row.Name).ToArray() ?? [];

    public void FocusCurrent()
    {
        Table.UpdateLayout();
        var index = Current is { } current ? Rows.IndexOf(current) : -1;
        if (index >= 0)
        {
            Table.ScrollIntoView(index);
            Table.UpdateLayout();
            if (Table.ContainerFromIndex(index) is Control container) { container.Focus(); return; }
        }
        Table.Focus();
    }

    public void SelectBoundary(bool last, bool extend)
    {
        if (Rows.Count == 0) return;
        var index = last ? Rows.Count - 1 : 0;
        if (extend) Table.Selection.SelectRange(Table.Selection.AnchorIndex < 0 ? 0 : Table.Selection.AnchorIndex, index);
        else Table.SelectedIndex = index;
        cursorName = Rows[index].Name;
        FocusCurrent();
    }

    public TextBox? RenameEditor => Table.GetVisualDescendants().OfType<TextBox>()
        .FirstOrDefault(editor => editor.Name == "RenameEditor" && editor.IsVisible && editor.DataContext == EditingRow);

    public void SelectRow(FileRow row)
    {
        cursorName = row.Name;
        Table.SelectedItem = row;
        FocusCurrent();
    }

    public void MovePage(bool down, bool extend)
    {
        if (Rows.Count == 0) return;
        var rowHeight = Table.GetRealizedContainers().FirstOrDefault()?.Bounds.Height ?? 36;
        var page = Math.Max(1, (int)(Table.Bounds.Height / Math.Max(1, rowHeight)) - 2);
        var current = Current is { } row ? Rows.IndexOf(row) : 0;
        var index = Math.Clamp(current + (down ? page : -page), 0, Rows.Count - 1);
        if (extend) Table.Selection.SelectRange(Table.Selection.AnchorIndex < 0 ? current : Table.Selection.AnchorIndex, index);
        else Table.SelectedIndex = index;
        cursorName = Rows[index].Name;
        FocusCurrent();
    }

    public void BeginRename()
    {
        if (Current is not { Entry.IsParent: false } row) return;
        CancelRename();
        EditingRow = row;
        row.DraftName = row.Name;
        row.IsEditing = true;
        Table.ScrollIntoView(row);
        Table.UpdateLayout();
        FocusRenameEditor();
        Dispatcher.UIThread.Post(FocusRenameEditor, DispatcherPriority.Input);
    }

    public void FocusRenameEditor()
    {
        if (RenameEditor is { } editor)
        {
            editor.Focus();
            editor.SelectAll();
        }
    }

    public void CancelRename()
    {
        if (EditingRow == null) return;
        EditingRow.IsEditing = false;
        EditingRow = null;
    }

    public async Task<string?> CommitRenameAsync()
    {
        if (IsCommitting || EditingRow is not { } row) return null;
        var name = row.DraftName;
        var validation = PanelRules.ValidateName(name);
        if (validation != null) return validation;
        IsCommitting = true;
        var editor = RenameEditor;
        if (editor != null) editor.IsReadOnly = true;
        try
        {
            await Session.FileSystem.RenameAsync(row.Entry, name);
            CancelRename();
            await LoadAsync(Session.DirectoryPath, selectName: name);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
        finally
        {
            IsCommitting = false;
            if (editor != null) editor.IsReadOnly = false;
        }
    }

    public void SetActive(bool active)
    {
        foreach (var key in new[] { "TableViewRowBackgroundSelected", "TableViewRowBackgroundSelectedPointerOver", "TableViewRowBackgroundSelectedPressed" })
        {
            if (active) Table.Resources.Remove(key);
            else Table.Resources[key] = new SolidColorBrush(Color.Parse("#1D2532"));
        }
        BorderBrush = active ? new SolidColorBrush(Color.Parse("#75B9F5")) : Brushes.Transparent;
    }

    private void UpdateHeaders()
    {
        foreach (var (field, button) in headers)
        {
            var title = field switch { SortField.Extension => "Type", SortField.Modified => "Modified", _ => field.ToString() };
            button.Content = title + (Session.SortOrder.Field == field ? Session.SortOrder.Descending ? "  ↓" : "  ↑" : "");
        }
    }

    public void SetFileTypes(FileTypes types) { fileTypes = types; foreach (var row in Rows) ApplyFileColor(row); }
    public void SetDirectorySize(DirectoryMeasurement measurement)
    {
        var row = Rows.FirstOrDefault(row => row.Name.Equals(measurement.Name, StringComparison.OrdinalIgnoreCase) && row.Entry.IsDirectory && !row.Entry.IsParent);
        if (row == null) return;
        sizes[row.Name] = measurement; row.SetMeasurement(measurement);
    }
    private void ApplyFileColor(FileRow row) => row.SetFileColor(fileTypes.Match(row.Name, row.Entry.IsDirectory) is { } rule ? new SolidColorBrush(Color.Parse(rule.Color)) : Brushes.White);
    private void UpdateSummary()
    {
        var selection = Table.SelectedItems?.OfType<FileRow>().ToHashSet() ?? [];
        foreach (var row in selectedRows.Except(selection)) row.SetSelected(false);
        foreach (var row in selection.Except(selectedRows)) row.SetSelected(true);
        selectedRows = selection;
        summary.Text = Session.Error ?? $"{Rows.Count(row => !row.Entry.IsParent):N0} items    {SelectedNames().Length:N0} selected";
    }
}

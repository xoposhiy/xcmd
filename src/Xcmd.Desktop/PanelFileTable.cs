// @implements ReadablePanels, NavigatePanels, MarkEntries, SortEntries, RefreshEntries, RestoreWorkspace
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

namespace Xcmd.Desktop;

internal sealed class PanelFileTable
{
    private readonly string id;
    private readonly FileIconCache icons;
    private readonly PanelRows panelRows;
    private readonly Dictionary<SortField, Button> headers = [];
    private bool headersConfigured;

    public TableView Table { get; }
    public FilePanelColumns Columns { get; }
    public GitLogPresentation LogPresentation { get; }
    public PanelSelection Selection { get; }
    public List<FileRow> Rows => panelRows.Items;
    public Vector ScrollOffset => Scroll?.Offset ?? default;

    public event Action? Activated;
    public event Action? OpenRequested;
    public event Action<SortField>? SortRequested;
    public event Action<FileRow>? RowFocused;
    public event Action? RowsPublished;
    public event Action? StateChanged;

    public PanelFileTable(string id, FileIconCache icons, PanelRows panelRows, TableViewColumn gitColumn)
    {
        this.id = id;
        this.icons = icons;
        this.panelRows = panelRows;
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

        Selection = new PanelSelection(Table, () => Rows, NotifyStateChanged);
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
        Table.Columns.Add(gitColumn);
        Table.Columns.Add(Column(SortField.Modified, "Modified", new GridLength(145)));

        LogPresentation = new GitLogPresentation(Table);
        Columns = new FilePanelColumns(Table, LogPresentation);
        Columns.Reset();
        Table.Loaded += (_, _) => ConfigureHeaders();
        Table.TemplateApplied += (_, _) => headersConfigured = false;
        Table.LayoutUpdated += (_, _) => { if (!headersConfigured) ConfigureHeaders(); };
        Table.SelectionChanged += (_, _) => NotifyStateChanged();
        Table.GotFocus += (_, e) =>
        {
            if (RowAt(e.Source) is { } row)
            {
                RowFocused?.Invoke(row);
                Selection.SetCursor(row);
                NotifyStateChanged();
            }
            Activated?.Invoke();
        };
        Table.PointerPressed += (_, e) =>
        {
            if (RowContainerAt(e.Source) != null)
            {
                Selection.BeginPointerSelection(e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                NotifyStateChanged();
            }
            Activated?.Invoke();
        };
        Table.DoubleTapped += (_, e) =>
        {
            if (RowContainerAt(e.Source) != null) OpenRequested?.Invoke();
        };
    }

    public void ReplaceRows(PanelSession session, List<FileRow> rows, bool showMeasurements, string[] selected, string? current)
    {
        Selection.ResetRange();
        Columns.ShowLocation(session);
        headersConfigured = false;
        panelRows.Replace(rows, showMeasurements);
        Table.ItemsSource = Rows;
        RowsPublished?.Invoke();
        Columns.UpdateRows(Rows);
        panelRows.RefreshNameMatches();
        Selection.ApplyRows(selected, current);
        NotifyStateChanged();
    }

    public void RefreshRows(PanelSession session, List<FileRow> rows, bool showMeasurements)
    {
        panelRows.RetainMeasurements(session.Entries);
        var selected = Selection.CaptureRowSelection();
        var current = Selection.Current;
        var index = current == null ? 0 : Rows.IndexOf(current);
        var focused = Table.IsKeyboardFocusWithin;
        var offset = ScrollOffset;
        ReplaceRows(session, rows, showMeasurements, selected, current?.Name);
        Selection.RestoreRefreshCursor(current?.Name, index, selected.Length > 0);
        SetScroll(offset);
        Table.UpdateLayout();
        if (focused) Selection.FocusCurrent(scrollIntoView: false);
        if (Scroll is { } restored) restored.Offset = offset;
        NotifyStateChanged();
    }

    public void SetAccessibleName(string name) => AutomationProperties.SetName(Table, name);

    public void SetScroll(Vector offset)
    {
        Table.UpdateLayout();
        if (Scroll is { } scroll) scroll.Offset = offset;
    }

    public void UpdateSort(SortOrder order)
    {
        foreach (var (field, button) in headers)
        {
            var title = field switch { SortField.Extension => "Type", SortField.Modified => "Modified", _ => field.ToString() };
            button.Content = title + (order.Field == field ? order.Descending ? "  ↓" : "  ↑" : "");
        }
    }

    private ScrollViewer? Scroll => Table.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    private static FileRow? RowAt(object? source) => RowContainerAt(source)?.DataContext as FileRow;

    private static TableViewRow? RowContainerAt(object? source) => (source as Control)?.GetSelfAndVisualAncestors()
        .OfType<TableViewRow>().FirstOrDefault();

    private void NotifyStateChanged() => StateChanged?.Invoke();

    private void ConfigureHeaders()
    {
        foreach (var viewer in Table.GetVisualDescendants().OfType<ScrollViewer>()) viewer.AllowAutoHide = false;
        foreach (var presenter in Table.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ScrollContentPresenter>()) presenter.ClipToBounds = true;
        Columns.ClipHeaders();
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
            Name = id + "Sort" + field,
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
        AutomationProperties.SetAutomationId(button, id + ".sort." + field.ToString().ToLowerInvariant());
        button.Click += (_, _) => { Activated?.Invoke(); SortRequested?.Invoke(field); Selection.FocusCurrent(); };
        headers[field] = button;
        var column = new TableViewColumn { Header = button, Width = width, MinWidth = name ? FilePanelColumns.NameMinimumWidth : 45 };
        if (name)
            column.CellTemplate = new FuncDataTemplate<FileRow>((row, _) => row == null ? new Border().WithFeedbackId("PanelView.empty") : FileNameCell.Create(row, icons));
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
}

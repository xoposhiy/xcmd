// @implements ReadablePanels, RestoreWorkspace
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class FilePanelColumns
{
    public const int NameMinimumWidth = 120;
    private const int TypeIndex = 1;
    private const int SizeIndex = 2;
    public const int GitIndex = 3;
    private const int ModifiedIndex = 4;
    private static readonly int[] MetadataPriority = [SizeIndex, GitIndex, TypeIndex, ModifiedIndex];
    private readonly TableView table;
    private readonly GitLogPresentation logPresentation;
    private IReadOnlyList<FileRow> rows = [];
    private double lastFilePanelWidth;
    private bool columnAdaptationPending;
    private bool useDefaultColumns = true;
    private bool settingColumnWidth;
    private bool showGitColumn;

    public FilePanelColumns(TableView table, GitLogPresentation logPresentation)
    {
        this.table = table;
        this.logPresentation = logPresentation;
        foreach (var column in table.Columns)
            column.PropertyChanged += OnColumnChanged;
        table.SizeChanged += (_, _) => QueueLayout();
        table.Loaded += (_, _) => QueueLayout();
    }

    public void ShowLocation(PanelSession session)
    {
        settingColumnWidth = true;
        logPresentation.Update(session);
        settingColumnWidth = false;
    }

    public void UpdateRows(IReadOnlyList<FileRow> value)
    {
        rows = value;
        UpdateDefaultTypeWidth();
        QueueLayout();
    }

    public void ShowGit(bool visible)
    {
        showGitColumn = visible;
        QueueLayout();
    }

    public void FitNameAfterResize(double previousWidth)
    {
        if (previousWidth > 0 && previousWidth != table.Bounds.Width) AdaptColumns(fitName: true);
    }

    public void ClipHeaders()
    {
        foreach (var presenter in table.GetVisualDescendants().OfType<TableViewColumnHeadersPresenter>())
            presenter.ClipToBounds = true;
    }

    public void Reset()
    {
        if (logPresentation.ResetWidths()) return;
        useDefaultColumns = true;
        SetColumnWidth(SizeIndex, 112);
        SetColumnWidth(GitIndex, 56);
        SetColumnWidth(ModifiedIndex, 128);
        UpdateDefaultTypeWidth();
        UpdateVisibility();
        FitNameColumn();
    }

    public ColumnState[] Capture() => useDefaultColumns ? [] :
        table.Columns.Select(column => new ColumnState(column.Width.Value, false)).ToArray();

    public void Restore(ColumnState[] saved)
    {
        Reset();
        if (saved.Length == 0 || saved is
            [{ Star: true }, { Width: 88, Star: false }, { Width: 128, Star: false }, { Width: 56, Star: false }, { Star: true }]) return;

        ColumnState[] columns = saved.Length == 4 ? [saved[0], saved[1], saved[2], new(56, false), saved[3]] : saved;
        useDefaultColumns = false;
        for (var i = 0; i < Math.Min(table.Columns.Count, columns.Length); i++)
            if (!columns[i].Star) SetColumnWidth(i, columns[i].Width);
        UpdateVisibility();
        if (columns[0].Star) FitNameColumn();
    }

    private void OnColumnChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TableViewColumn.WidthProperty && !settingColumnWidth && !logPresentation.IsLog)
            useDefaultColumns = false;
    }

    private void QueueLayout()
    {
        if (columnAdaptationPending) return;
        columnAdaptationPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            columnAdaptationPending = false;
            AdaptColumns();
        }, DispatcherPriority.Background);
    }

    private void AdaptColumns(bool fitName = false)
    {
        if (logPresentation.IsLog) { logPresentation.Adapt(); return; }
        var width = table.Bounds.Width;
        var resized = lastFilePanelWidth > 0 && width != lastFilePanelWidth;
        lastFilePanelWidth = width;
        UpdateVisibility();
        if (fitName || resized || useDefaultColumns) FitNameColumn();
    }

    private void UpdateVisibility()
    {
        var requiredWidth = (double)NameMinimumWidth;
        foreach (var index in MetadataPriority)
        {
            var column = table.Columns[index];
            var available = index != GitIndex || showGitColumn;
            if (available) requiredWidth += column.Width.Value;
            column.IsVisible = available && requiredWidth <= AvailableWidth;
        }
    }

    // Reserve the scrollbar gutter so changing row counts does not move the dividers.
    private double AvailableWidth => table.Bounds.Width > 0 ? table.Bounds.Width - 18 : 600;

    private void FitNameColumn()
    {
        var otherWidths = table.Columns.Skip(1).Where(column => column.IsVisible).Sum(column => column.Width.Value);
        SetColumnWidth(0, Math.Floor(AvailableWidth - otherWidths));
    }

    private void UpdateDefaultTypeWidth()
    {
        if (!useDefaultColumns || logPresentation.IsLog) return;
        const double maxWidth = 144;
        var width = 88d;
        var typeface = new Typeface(table.FontFamily);
        foreach (var kind in rows.Select(row => row.Kind).Distinct(StringComparer.Ordinal))
        {
            var text = new FormattedText(kind, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, UiPalette.BodySize, UiPalette.Text);
            width = Math.Max(width, Math.Ceiling(text.Width) + 16);
            if (width >= maxWidth) break;
        }
        SetColumnWidth(TypeIndex, Math.Min(width, maxWidth));
    }

    private void SetColumnWidth(int index, double width)
    {
        var column = table.Columns[index];
        settingColumnWidth = true;
        column.Width = new GridLength(Math.Max(column.MinWidth, width));
        settingColumnWidth = false;
    }
}

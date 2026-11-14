using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private bool columnAdaptationPending;

    private void RestoreColumnWidths(ColumnState[] saved)
    {
        ColumnState[] columns = saved.Length == 4 ? [saved[0], saved[1], saved[2], new(56, false), saved[3]] : saved;
        for (var i = 0; i < Math.Min(Table.Columns.Count, columns.Length); i++)
            Table.Columns[i].Width = new GridLength(i == 1 && !columns[i].Star && columns[i].Width == 64 ? 88 : columns[i].Width,
                columns[i].Star ? GridUnitType.Star : GridUnitType.Pixel);
        Table.Columns[^1].Width = new GridLength(1, GridUnitType.Star);
    }

    private void OnColumnChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TableViewColumn.WidthProperty || e.Property == TableViewColumn.MinWidthProperty) QueueColumnAdaptation();
    }

    private void QueueColumnAdaptation()
    {
        if (columnAdaptationPending) return;
        columnAdaptationPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            columnAdaptationPending = false;
            AdaptColumns();
        }, DispatcherPriority.Background);
    }

    private void AdaptColumns()
    {
        if (Table.Bounds.Width <= 0) return;
        if (logPresentation.IsLog) { logPresentation.Adapt(); return; }
        // Reserve scrollbar space independently of its current visibility. Otherwise a
        // scrollbar can change the column decision and start another layout cycle.
        var available = Math.Max(0, Table.Bounds.Width - 18);
        var name = Table.Columns[0];
        var required = name.MinWidth;
        // Retain the most useful columns first. Width preferences survive hiding and resizing.
        var availableColumns = Enumerable.Range(1, Table.Columns.Count - 1).Where(index => index != GitColumnIndex || showGitColumn).ToArray();
        foreach (var index in availableColumns)
        {
            var column = Table.Columns[index];
            required += RequiredWidth(column);
        }
        var count = availableColumns.Length;
        while (count > 0 && required > available)
        {
            var column = Table.Columns[availableColumns[--count]];
            required -= RequiredWidth(column);
        }
        for (var index = 1; index < Table.Columns.Count; index++)
        {
            var visible = availableColumns.Take(count).Contains(index);
            if (Table.Columns[index].IsVisible == visible) continue;
            Table.Columns[index].IsVisible = visible;
            headersConfigured = false;
        }
        var maximum = Math.Max(name.MinWidth, Math.Floor(available - required + name.MinWidth));
        if (Math.Abs(name.MaxWidth - maximum) > 0.5) name.MaxWidth = maximum;
    }

    private static double RequiredWidth(TableViewColumn column) => column.Width.IsStar ? column.MinWidth : Math.Max(column.MinWidth, column.Width.Value);
}

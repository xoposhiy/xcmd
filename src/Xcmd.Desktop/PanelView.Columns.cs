using Avalonia.Controls;
using Avalonia.Threading;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private bool columnAdaptationPending;

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
        // Reserve scrollbar space independently of its current visibility. Otherwise a
        // scrollbar can change the column decision and start another layout cycle.
        var available = Math.Max(0, Table.Bounds.Width - 18);
        var name = Table.Columns[0];
        var required = name.MinWidth;
        // Retain the most useful columns first. Width preferences survive hiding and resizing.
        for (var index = 1; index < Table.Columns.Count; index++)
        {
            var column = Table.Columns[index];
            required += column.Width.IsStar ? column.MinWidth : Math.Max(column.MinWidth, column.Width.Value);
        }
        var last = Table.Columns.Count - 1;
        while (last > 0 && required > available)
        {
            var column = Table.Columns[last--];
            required -= column.Width.IsStar ? column.MinWidth : Math.Max(column.MinWidth, column.Width.Value);
        }
        for (var index = 1; index < Table.Columns.Count; index++)
        {
            var visible = index <= last;
            if (Table.Columns[index].IsVisible == visible) continue;
            Table.Columns[index].IsVisible = visible;
            headersConfigured = false;
        }
        var maximum = Math.Max(name.MinWidth, Math.Floor(available - required + name.MinWidth));
        if (Math.Abs(name.MaxWidth - maximum) > 0.5) name.MaxWidth = maximum;
    }
}

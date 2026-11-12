using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Styling;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal static class SearchTables
{
    public static TableViewColumn Column<T>(string header, GridLength width, double minimumWidth, Func<T?, Control> cell,
        HorizontalAlignment alignment = HorizontalAlignment.Left) where T : class => new()
        {
            Header = header,
            Width = width,
            MinWidth = minimumWidth,
            HorizontalContentAlignment = alignment,
            CellTemplate = new FuncDataTemplate<T>((item, _) => cell(item))
        };

    public static void Configure(TableView table)
    {
        Avalonia.Automation.AutomationProperties.SetAutomationId(table, "search." + table.Name);
        table.SelectionMode = SelectionMode.Single;
        table.IsTextSearchEnabled = false;
        table.AutoScrollToSelectedItem = false;
        table.Focusable = true;
        UiLayout.Table(table);
    }

    public static TextBlock Text(string? value, string? name = null) => new()
    {
        Name = name,
        Text = value,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = Avalonia.Media.TextTrimming.LeadingCharacterEllipsis
    };

    public static ColumnState[] Capture(TableView table) => table.Columns.Select(column => new ColumnState(column.Width.Value, column.Width.IsStar)).ToArray();

    public static void Restore(TableView table, ColumnState[]? saved)
    {
        if (saved == null || saved.Length != table.Columns.Count) return;
        for (var index = 0; index < saved.Length; index++)
            table.Columns[index].Width = new GridLength(saved[index].Width, saved[index].Star ? GridUnitType.Star : GridUnitType.Pixel);
    }
}

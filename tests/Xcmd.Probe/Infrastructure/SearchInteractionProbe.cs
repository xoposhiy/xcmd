using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class SearchInteractionProbe
{
    public static async Task<TextBlock> QueryErrorAfterRefreshAsync(ProbeContext context, Window window)
    {
        await Task.Delay(180);
        await context.SettleAsync();
        return window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SearchQueryError");
    }

    public static TableView Table(Window window, string name) => window.GetVisualDescendants().OfType<TableView>().Single(table => table.Name == name);
    public static async Task DoubleClickRowAsync(ProbeContext context, Window window, TableView table, object item)
    {
        var index = table.ItemsSource!.Cast<object>().ToList().IndexOf(item);
        Check(index >= 0, "The selected detail row exists");
        table.ScrollIntoView(index); table.UpdateLayout();
        var row = table.ContainerFromIndex(index) ?? throw new InvalidOperationException("The detail row is not realized.");
        await FeedbackProbe.DoubleClickAsync(context, window, row);
    }

    public static void CheckHighlight(ProbeContext context, string name, string match)
    {
        var row = context.Window.Left.Rows.Single(row => row.Name == name);
        Equal(match, name.Substring(row.NameMatchStart, row.NameMatchLength), "The exact matching text is highlighted");
        var text = context.Row(name).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == name);
        Check(text.TextLayout.TextLines.SelectMany(line => line.TextRuns).Any(run =>
            run.Properties?.ForegroundBrush is ISolidColorBrush brush && brush.Color == Color.Parse("#65F0CE")), "The rendered text layout uses the match color");
    }
}

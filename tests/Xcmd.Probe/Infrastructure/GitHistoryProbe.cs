// @tests NavigatePanels, ReadablePanels, BrowseHistory, ReadCommitMessage, BrowseSnapshot
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Features.Viewer;

namespace Xcmd.Probe.Infrastructure;

public static class GitHistoryProbe
{
    public static FileRow[] Commits(PanelView panel) => panel.Rows.Where(row => !row.Entry.IsParent && GitPath.Parse(row.Entry.FullPath).Kind == "tree").ToArray();

    public static Task SelectCommitAsync(ProbeContext context, PanelView panel, string subject) =>
        context.SelectAsync(Commits(panel).Single(row => row.Name.EndsWith(" " + subject, StringComparison.Ordinal)).Name, panel);

    public static void CheckLogPresentation(ProbeContext context, PanelView panel)
    {
        var headers = panel.Table.Columns.Where(column => column.IsVisible).Select(column => (column.Header as TextBlock)?.Text ?? column.Header as string).ToArray();
        ProbeContext.Check(headers.SequenceEqual(new[] { "Commit", "Subject", "Author", "Date" }), "Git log has commit columns instead of file columns");
        var row = context.Row(panel.Current!.Name, panel);
        ProbeContext.Check(!row.GetVisualDescendants().OfType<Image>().Any(), "Git log rows have no filesystem icons");
        ProbeContext.Check(panel.Table.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "History Probe"), "The log shows commit authors");
    }

    public static void CheckMessageScroll(PanelView panel)
    {
        var scroll = panel.CommitMessage.GetVisualDescendants().OfType<ScrollViewer>().Single();
        ProbeContext.Check(scroll.Extent.Height > scroll.Viewport.Height, "Long commit messages remain available through vertical scrolling");
        ProbeContext.Check(panel.CommitMessage.Bounds.Height >= 60 && panel.Table.Bounds.Height >= 100, "The message and commit list both have useful space");
    }

    public static string Summary(PanelView panel) => panel.GetVisualDescendants().OfType<TextBlock>()
        .Single(text => Avalonia.Automation.AutomationProperties.GetAutomationId(text) == panel.Id + ".summary").Text ?? "";

    public static string KeyboardHelp(ProbeContext context) => string.Join(" · ", context.Window.GetVisualDescendants()
        .OfType<WrapPanel>().Single(control => control.Name == "KeyboardHelp").GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text));

    public static MenuItem ViewerCommand(FileViewerWindow viewer, string name) => viewer.Reader.Commands.ItemsSource!.OfType<MenuItem>()
        .SelectMany(menu => menu.ItemsSource?.OfType<MenuItem>() ?? []).Single(item => item.Name == name);

    public static async Task ResizeMessageAsync(ProbeContext context, PanelView panel)
    {
        var splitter = panel.GetVisualDescendants().OfType<GridSplitter>().Single(control => control.Name == "GitMessageDivider");
        var height = panel.CommitMessage.Bounds.Height;
        var point = splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, 2), context.Window)!.Value;
        context.Input.MovePointer(point); context.Input.PressPointer(point, MouseButton.Left);
        var destination = point + new Vector(0, -60);
        context.Input.MovePointer(destination); context.Input.ReleasePointer(destination, MouseButton.Left);
        await context.SettleAsync();
        ProbeContext.Check(panel.CommitMessage.Bounds.Height > height + 10, "The message divider changes the reading area height");
        ProbeContext.Check(panel.Table.Bounds.Height >= 84, "The divider preserves a useful commit list");
    }

    public static async Task CheckFileInteractionAsync(ProbeContext context, PanelView panel, string currentName, string hoverName)
    {
        await context.SelectAsync(currentName, panel);
        var current = panel.Current!;
        var hovered = panel.Rows.Single(row => row.Name == hoverName);
        var point = context.Row(hoverName, panel).TranslatePoint(new Point(40, 10), context.Window)!.Value;
        context.Input.MovePointer(point);
        await context.SettleAsync();
        ProbeContext.Check(current.IsCurrent && !hovered.IsCurrent && !hovered.IsMarked, "Hover does not move or mark the file cursor");
        var row = context.Row(hoverName, panel);
        var border = row.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "Border");
        ProbeContext.Equal(hovered.RowBackground, border.Background, "Git rows use the normal file-row background binding");
        ProbeContext.Check(!panel.GetVisualDescendants().OfType<Button>().Any(button => button.Content is string text && text is "Back" or "Diff" or "Close log" or "Repository tree" or "Changed files"),
            "The standard panel has no separate Git action bar");
    }
}

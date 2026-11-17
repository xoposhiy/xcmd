// @implements HeadlessVerification
// @tests NavigatePanels, ReadablePanels
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Desktop;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class PanelPresentationProbe
{
    public sealed record RowGeometry(Size Size, Point Name, Point Icon, Rect[] Cells);

    public static Control Container(PanelView panel, string name) => panel.Table.ContainerFromIndex(panel.Rows.FindIndex(row => row.Name == name))
        ?? throw new InvalidOperationException("The expected row is not visible.");

    public static Color Background(PanelView panel, string name) => ((ISolidColorBrush)Container(panel, name)
        .GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Border").Background!).Color;

    public static RowGeometry Geometry(PanelView panel, string name)
    {
        var row = Container(panel, name);
        var text = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == name);
        var icon = row.GetVisualDescendants().OfType<Image>().Single();
        var cells = row.GetVisualDescendants().OfType<TableViewCell>()
            .Select(cell => new Rect(cell.TranslatePoint(default, row)!.Value, cell.Bounds.Size)).ToArray();
        return new(row.Bounds.Size, text.TranslatePoint(default, row)!.Value, icon.TranslatePoint(default, row)!.Value, cells);
    }

    public static void SameGeometry(RowGeometry expected, RowGeometry actual)
    {
        Equal(expected.Size, actual.Size, "Cursor changes keep the row size");
        Equal(expected.Name, actual.Name, "Cursor changes keep the file name position");
        Equal(expected.Icon, actual.Icon, "Cursor changes keep the icon position");
        Check(expected.Cells.SequenceEqual(actual.Cells), "Cursor changes keep every cell position and size.");
    }

    public static Color NameColor(PanelView panel, string name) => ((ISolidColorBrush)Container(panel, name)
        .GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == name).Foreground!).Color;

    public static void HorizontalScrollAvailable(PanelView panel)
    {
        var viewer = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Check(viewer.Extent.Width > viewer.Viewport.Width, "Wide columns have a scrollable horizontal extent.");
        Check(panel.Table.GetVisualDescendants().OfType<ScrollBar>().Any(bar => bar.Orientation == Orientation.Horizontal && bar.IsVisible), "Wide columns show a horizontal scroll bar.");
    }

    public static void NoHorizontalScroll(PanelView panel)
    {
        var viewer = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Check(viewer.Extent.Width <= viewer.Viewport.Width + 0.01, $"Fitting columns do not overflow horizontally: {viewer.Extent.Width} / {viewer.Viewport.Width}.");
        Check(!panel.Table.GetVisualDescendants().OfType<ScrollBar>().Any(bar => bar.Orientation == Orientation.Horizontal && bar.IsVisible), "Fitting columns do not show a horizontal scroll bar.");
    }
}

// @tests FileIcons
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Probe.Infrastructure;

public static class IconPresentationProbe
{
    public static Image Icon(ProbeContext context, string name) =>
        context.Row(name).GetVisualDescendants().OfType<Image>().Single();

    public static Image BuildNameIcon(PanelView panel, string name)
    {
        var row = panel.Rows.Single(item => item.Name == name);
        var cell = panel.Table.Columns[0].CellTemplate!.Build(row)!;
        return cell.GetVisualDescendants().OfType<Image>().Single();
    }

    public static int PixelSize(Image image) => (image.Source as WriteableBitmap)?.PixelSize.Width ?? 0;
}

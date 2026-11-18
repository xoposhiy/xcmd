// @tests ReadablePanels, ReaderThemes, SharedControls
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Xcmd.Probe.Infrastructure;

public static class DesignSystemProbe
{
    public static void CheckTable(TableView table)
    {
        ProbeContext.Equal(14d, table.FontSize, "Primary table data stays readable");
        foreach (var row in table.GetVisualDescendants().OfType<TableViewRow>().Where(row => row.IsVisible))
            ProbeContext.Check(row.Bounds.Height >= 28 && row.Bounds.Height <= 30, "File and search rows share the compact height");
        foreach (var header in table.GetVisualDescendants().OfType<TableViewColumnHeader>())
        {
            ProbeContext.Equal(Color.Parse("#263247"), (header.Background as ISolidColorBrush)?.Color, "Table headers share the same surface");
            ProbeContext.Check(header.Bounds.Height >= 28, "Headers fit readable text");
        }
    }

    public static void CheckActions(Window window)
    {
        ProbeContext.Equal(14d, window.FontSize, "Every application window inherits readable body text");
        foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(button => button.IsVisible && button.Bounds.Width > 0))
        {
            ProbeContext.Equal(new CornerRadius(0), button.CornerRadius, "Buttons share square corners");
            var position = button.TranslatePoint(default, window)!.Value;
            ProbeContext.Check(position.X >= 0 && position.X + button.Bounds.Width <= window.Bounds.Width + 1, "Action fits the window horizontally: " + button.Name);
            ProbeContext.Check(position.Y >= 0 && position.Y + button.Bounds.Height <= window.Bounds.Height + 1, "Action fits the window vertically: " + button.Name);
        }
    }

    public static void CheckSelection(TextBox content)
    {
        var background = ((ISolidColorBrush)content.Background!).Color;
        var selection = ((ISolidColorBrush)content.SelectionBrush!).Color;
        var foreground = ((ISolidColorBrush)content.SelectionForegroundBrush!).Color;
        var a = selection.A / 255d;
        var composite = Color.FromRgb((byte)(selection.R * a + background.R * (1 - a)),
            (byte)(selection.G * a + background.G * (1 - a)), (byte)(selection.B * a + background.B * (1 - a)));
        double L(Color c)
        {
            double Linear(byte v) => v / 255d <= 0.04045 ? v / 255d / 12.92 : Math.Pow((v / 255d + 0.055) / 1.055, 2.4);
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        var ratio = (Math.Max(L(composite), L(foreground)) + .05) / (Math.Min(L(composite), L(foreground)) + .05);
        ProbeContext.Check(ratio >= 4.5, "The selected text remains readable in each reader palette");
    }
}

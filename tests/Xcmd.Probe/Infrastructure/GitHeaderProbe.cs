using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xcmd.Desktop;

namespace Xcmd.Probe.Infrastructure;

public static class GitHeaderProbe
{
    public static void CheckAlignment(PanelView panel)
    {
        var header = panel.Git.GetVisualAncestors().OfType<Grid>().Single(grid => grid.Name == panel.Id + "LocationHeader");
        var path = header.Children.OfType<TextBlock>().Single();
        var middle = Centre(path, header);
        foreach (var control in new Control[] { panel.Git, panel.Git.ActionsButton }
            .Concat(panel.Git.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsVisible && text.Bounds.Height > 0))
            .Concat(panel.Git.GetVisualDescendants().OfType<PathIcon>().Where(icon => icon.IsVisible)))
        {
            var actual = Centre(control, header);
            ProbeContext.Check(Math.Abs(actual - middle) <= .75,
                $"Git header content aligns with the folder path: {AutomationProperties.GetAutomationId(control)} centre {actual}, expected {middle}");
        }
        var tableTop = panel.Table.TranslatePoint(default, panel)!.Value.Y;
        ProbeContext.Check(panel.Git.TranslatePoint(new Point(0, panel.Git.Bounds.Height), panel)!.Value.Y <= tableTop,
            "The Git control fits entirely above the file table");
    }

    public static void CheckMenuPlacement(PanelView panel, Window window)
    {
        var bottom = panel.Git.ActionsButton.TranslatePoint(new Point(0, panel.Git.ActionsButton.Bounds.Height), window)!.Value.Y;
        var itemTop = panel.Git.CommitCommand.TranslatePoint(default, window)!.Value.Y;
        ProbeContext.Check(itemTop >= bottom, "Repository actions open below their button without covering the header");
    }

    private static double Centre(Control control, Control relativeTo) =>
        control.TranslatePoint(new Point(0, control.Bounds.Height / 2), relativeTo)!.Value.Y;
}

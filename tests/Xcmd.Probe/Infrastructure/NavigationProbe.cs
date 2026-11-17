using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class NavigationProbe
{
    public static async Task<DirectoryNavigationDialog> OpenHistoryAsync(ProbeContext context)
    {
        await context.SelectAsync("folder1"); await context.KeyAsync(Key.Enter); await context.KeyAsync(Key.Back);
        context.Window.Favorites.Add(context.RightPath);
        await context.KeyAsync(Key.Down, RawInputModifiers.Alt);
        return context.Window.ActiveNavigationDialog!;
    }

    public static void CheckOneSelection(DirectoryNavigationDialog dialog) => Equal(1,
        new[] { dialog.Directories, dialog.Favorites }.Count(list => list.SelectedItem != null), "Only the active list has a selection");

    public static Color FileColor(ProbeContext context, string name)
    {
        var text = context.Row(name).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == name);
        return ((ISolidColorBrush)text.Foreground!).Color;
    }

    public static Color TypeColor(ProbeContext context, string name)
    {
        var kind = context.Window.Left.Rows.Single(row => row.Name == name).Kind;
        var text = context.Row(name).GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Text == kind && text.GetValue(FeedbackTarget.IdProperty) == "PanelView.text");
        return ((ISolidColorBrush)text.Foreground!).Color;
    }

    public static IEnumerable<string> ObserveFor(DirectoryAttention attention, string path, DateTimeOffset start, int seconds, bool active)
    {
        for (var i = 0; i <= seconds; i++)
            if (attention.Observe(path, start.AddSeconds(i), active) is { } visit) yield return visit;
    }
}

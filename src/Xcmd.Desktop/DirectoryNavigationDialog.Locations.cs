// @implements FindSavedLocation
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class DirectoryNavigationDialog
{
    private readonly Dictionary<TextBlock, string> locationLabels = [];
    private HashSet<string> missingLocations = new(StringComparer.OrdinalIgnoreCase);

    public static string OpenedMessage(string requested, string opened) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(requested)).Equals(opened, StringComparison.OrdinalIgnoreCase)
            ? "Ready" : "Directory no longer exists. Opened " + PathText.Display(opened);

    public async Task RefreshAvailabilityAsync()
    {
        var paths = Directories.Items.Cast<string>().Concat(FavoriteLocations().Select(item => item is Xcmd.Core.FavoriteDirectory favorite ? favorite.Path : (string)item)).ToArray();
        missingLocations = await DirectoryAvailability.MissingAsync(paths);
        foreach (var (label, path) in locationLabels) ApplyAvailability(label, path);
    }

    private void TrackLocation(TextBlock label, string path)
    {
        locationLabels[label] = path;
        ApplyAvailability(label, path);
    }

    private void ApplyAvailability(TextBlock label, string path)
    {
        var missing = missingLocations.Contains(path);
        label.Text = PathText.Display(path) + (missing ? " [missing]" : "");
        ToolTip.SetTip(label, PathText.Display(path) + (missing ? "\nMissing directory. Open the nearest existing parent." : ""));
    }

    private bool HandleListMovement(KeyEventArgs e)
    {
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Control or KeyModifiers.Shift)) return false;
        if (e.Key is not (Key.PageUp or Key.PageDown or Key.Home or Key.End)) return false;
        ResetNameSearch();
        if (activeList.ItemCount == 0) return true;
        var viewport = activeList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.Viewport.Height ?? activeList.Bounds.Height;
        var page = Math.Max(1, (int)(viewport / UiPalette.RowHeight) - 2);
        var current = Math.Max(0, activeList.SelectedIndex);
        activeList.SelectedIndex = e.Key switch
        {
            Key.Home => 0,
            Key.End => activeList.ItemCount - 1,
            _ => Math.Clamp(current + (e.Key == Key.PageDown ? page : -page), 0, activeList.ItemCount - 1)
        };
        activeList.ScrollIntoView(activeList.SelectedItem!);
        FocusSelection();
        return true;
    }
}

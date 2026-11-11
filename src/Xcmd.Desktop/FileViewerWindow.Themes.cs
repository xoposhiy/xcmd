using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Xcmd.Desktop;

public sealed partial class FileViewerWindow
{
    private readonly Dictionary<string, MenuItem> themeItems = new(StringComparer.Ordinal);
    private ReaderTheme activeTheme = ReaderThemes.Default;
    public string ThemeId => activeTheme.Id;
    public event Action<string>? ThemeChanged;

    private MenuItem BuildThemeMenu()
    {
        foreach (var theme in ReaderThemes.All)
        {
            var item = MenuCommand(theme.Name, "viewer.theme." + theme.Id, () => SelectTheme(theme.Id));
            item.ToggleType = MenuItemToggleType.Radio; item.GroupName = "reader-theme";
            themeItems[theme.Id] = item;
        }
        return new MenuItem { Header = "Color theme", Name = "ViewerThemes", ItemsSource = themeItems.Values };
    }

    private void SelectTheme(string id)
    {
        ApplyTheme(id);
        ThemeChanged?.Invoke(activeTheme.Id);
        TextContent.Focus();
    }

    internal void ApplyTheme(string? id)
    {
        activeTheme = ReaderThemes.Find(id);
        var background = Brush.Parse(activeTheme.Background);
        var foreground = Brush.Parse(activeTheme.Foreground);
        var surface = Brush.Parse(activeTheme.Surface);
        var accent = Brush.Parse(activeTheme.Accent);
        RequestedThemeVariant = activeTheme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        Background = background; Foreground = foreground;
        viewerBody.Background = background; TitleBar.Background = background;
        statusBar.BorderBrush = new SolidColorBrush(Color.Parse(activeTheme.Border), 0.5);
        TextContent.Background = background; TextContent.Foreground = foreground; TextContent.CaretBrush = foreground;
        var selection = Color.Parse(activeTheme.Accent);
        TextContent.SelectionBrush = new SolidColorBrush(Color.FromArgb(110, selection.R, selection.G, selection.B));
        TextContent.SelectionForegroundBrush = foreground;
        foreach (var state in new[] { "", "Focused", "PointerOver", "Disabled" })
        {
            TextContent.Resources["TextControlBackground" + state] = background;
            Search.Resources["TextControlBackground" + state] = surface;
        }
        Search.Background = surface; Search.Foreground = foreground; Search.CaretBrush = foreground;
        Search.BorderBrush = Brush.Parse(activeTheme.Border);
        Search.Resources["TextControlBorderBrushFocused"] = accent;
        searchPanel.Background = surface;
        foreach (var text in new[] { titlePath, Progress, readingStatus, feedbackStatus, searchStatus, information }) text.Foreground = foreground;
        error.Foreground = Brush.Parse(activeTheme.IsDark ? "#ffb4ab" : "#b42318");
        Commands.Foreground = foreground;
        foreach (var root in Commands.Items.OfType<MenuItem>()) ApplyMenuColor(root, foreground);
        foreach (var button in TitleBar.GetVisualDescendants().OfType<Button>()) button.Foreground = foreground;
        foreach (var pair in themeItems) pair.Value.IsChecked = pair.Key == activeTheme.Id;
    }

    private static void ApplyMenuColor(MenuItem item, IBrush foreground)
    {
        item.Foreground = foreground;
        foreach (var child in item.Items.OfType<MenuItem>()) ApplyMenuColor(child, foreground);
    }
}

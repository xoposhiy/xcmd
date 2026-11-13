using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Xcmd.Desktop;

public sealed partial class FileViewerWindow
{
    private readonly Dictionary<string, MenuItem> themeItems = new(StringComparer.Ordinal);
    private ReaderTheme activeTheme = ReaderThemes.Default;
    public string ThemeId => activeTheme.Id;
    public event Action<string>? ThemeChanged;
    private ReaderSelection? menuSelection;

    private sealed record ReaderSelection(string? Text, int Caret, int Start, int End);

    private void PreserveSelectionInMenus()
    {
        Commands.AddHandler(InputElement.PointerPressedEvent, (_, _) =>
        {
            if (!Commands.IsOpen) menuSelection = new(TextContent.Text, TextContent.CaretIndex, TextContent.SelectionStart, TextContent.SelectionEnd);
        }, RoutingStrategies.Tunnel);
    }

    private MenuItem BuildThemeMenu()
    {
        foreach (var theme in ReaderThemes.All)
        {
            var item = MenuCommand(theme.Name, "viewer.theme." + theme.Id, () => SelectTheme(theme.Id));
            item.ToggleType = MenuItemToggleType.Radio; item.GroupName = "reader-theme";
            themeItems[theme.Id] = item;
        }
        return new MenuItem { Header = "Color theme", Name = "ViewerThemes", ItemsSource = themeItems.Values }.WithFeedbackId("FileViewerWindow.Themes.BuildThemeMenu");
    }

    private void SelectTheme(string id)
    {
        var saved = menuSelection;
        ApplyTheme(id);
        ThemeChanged?.Invoke(activeTheme.Id);
        TextContent.Focus();
        if (saved != null) RestoreReaderSelection(activeTheme.Id, saved.Text, saved.Caret, saved.Start, saved.End);
        menuSelection = null;
    }

    internal void ApplyTheme(string? id)
    {
        var originalText = TextContent.Text;
        var caret = TextContent.CaretIndex;
        var start = TextContent.SelectionStart;
        var end = TextContent.SelectionEnd;
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
        TextContent.SelectionForegroundBrush = UiContrast.SelectionText(selection, Color.Parse(activeTheme.Background));
        foreach (var state in new[] { "", "Focused", "PointerOver", "Disabled" })
        {
            TextContent.Resources["TextControlBackground" + state] = background;
            Search.Resources["TextControlBackground" + state] = surface;
        }
        Search.Background = surface; Search.Foreground = foreground; Search.CaretBrush = foreground;
        Search.BorderBrush = UiContrast.Ratio(Color.Parse(activeTheme.Border), Color.Parse(activeTheme.Surface)) >= 3
            ? Brush.Parse(activeTheme.Border) : foreground;
        Search.Resources["TextControlBorderBrushFocused"] = UiContrast.Ratio(selection, Color.Parse(activeTheme.Surface)) >= 3 ? accent : foreground;
        searchPanel.Background = surface;
        foreach (var text in new[] { titlePath, Progress, readingStatus, feedbackStatus, searchStatus, information }) text.Foreground = foreground;
        error.Foreground = Brush.Parse(activeTheme.IsDark ? "#ffb4ab" : "#b42318");
        Commands.Foreground = foreground;
        foreach (var root in Commands.Items.OfType<MenuItem>()) ApplyMenuColor(root, foreground);
        foreach (var button in TitleBar.GetVisualDescendants().OfType<Button>()) button.Foreground = foreground;
        foreach (var pair in themeItems) pair.Value.IsChecked = pair.Key == activeTheme.Id;
        RestoreReaderSelection(activeTheme.Id, originalText, caret, start, end);
    }

    private void RestoreReaderSelection(string themeId, string? text, int caret, int start, int end) => Dispatcher.UIThread.Post(() =>
    {
        // A palette change can rebuild the focused template while a menu click is
        // being released. Restore after that input, without overwriting a new file.
        if (closed || activeTheme.Id != themeId || TextContent.Text != text) return;
        TextContent.CaretIndex = caret;
        TextContent.SelectionStart = start; TextContent.SelectionEnd = end;
    }, DispatcherPriority.Input);

    private static void ApplyMenuColor(MenuItem item, IBrush foreground)
    {
        item.Foreground = foreground;
        foreach (var child in item.Items.OfType<MenuItem>()) ApplyMenuColor(child, foreground);
    }
}

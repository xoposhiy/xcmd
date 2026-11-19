// @implements ReaderThemes
using Avalonia.Media;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed record ReaderTheme(string Id, string Name, string Background, string Foreground, string Surface, string Border, string Accent)
{
    public bool IsDark
    {
        get { var color = Color.Parse(Background); return 299 * color.R + 587 * color.G + 114 * color.B < 128000; }
    }
}

internal static class ReaderThemes
{
    public static IReadOnlyList<ReaderTheme> All { get; } =
    [
        new("light", "Cream", "#fff2cf", "#000000", "#edead8", "#ccc09a", "#c8b860"),
        new("github", "GitHub", "#ffffff", "#1f2328", "#f6f8fa", "#d0d7de", "#0969da"),
        new("solarized-light", "Solarized Light", "#fdf6e3", "#586e75", "#eee8d5", "#93a1a1", "#b58900"),
        new("forest-light", "Forest Light", "#f6f1e3", "#2d3825", "#ebe5cf", "#d4c9a8", "#2d6a3e"),
        new("dark", "Dark", "#1c1810", "#e0d5b0", "#201d0c", "#4a4430", "#d4a820"),
        new("solarized-dark", "Solarized Dark", "#002b36", "#839496", "#073642", "#586e75", "#268bd2"),
        new("dracula", "Dracula", "#282a36", "#f8f8f2", "#21222c", "#44475a", "#bd93f9"),
        new("nord", "Nord", "#2e3440", "#d8dee9", "#272c36", "#434c5e", "#88c0d0"),
        new("forest-dark", "Forest Dark", "#0d1612", "#e0d8b8", "#11201a", "#1f3027", "#7eb877")
    ];
    public static ReaderTheme Default { get; } = All.Single(theme => theme.Id == "dark");
    public static ReaderTheme Find(string? id) => All.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.Ordinal)) ?? Default;
}

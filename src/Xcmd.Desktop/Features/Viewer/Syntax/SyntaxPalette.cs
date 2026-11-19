// @implements SyntaxSpans
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer;

namespace Xcmd.Desktop.Features.Viewer.Syntax;

internal static class SyntaxPalette
{
    private static readonly IReadOnlyDictionary<SyntaxKind, (string Dark, string Light)> TokenColors =
        new Dictionary<SyntaxKind, (string, string)>
        {
            [SyntaxKind.Comment] = ("#93b78a", "#47733e"),
            [SyntaxKind.Keyword] = ("#c8a2ff", "#7135a1"),
            [SyntaxKind.String] = ("#e8b286", "#a33c20"),
            [SyntaxKind.Number] = ("#a5d6a7", "#176f47"),
            [SyntaxKind.Type] = ("#70cbd1", "#006b77"),
            [SyntaxKind.Tag] = ("#70cbd1", "#006b77"),
            [SyntaxKind.Function] = ("#e4d38b", "#795600"),
            [SyntaxKind.Heading] = ("#e4d38b", "#795600"),
            [SyntaxKind.Property] = ("#8cc8f4", "#1e5d9d"),
            [SyntaxKind.Operator] = ("#ebafcc", "#99416c")
        };

    public static IReadOnlyDictionary<SyntaxKind, IBrush> Create(ReaderTheme theme, IReadOnlyList<Color>? highlights = null)
    {
        var background = Color.Parse(theme.Background);
        return Enum.GetValues<SyntaxKind>().ToDictionary(kind => kind, kind =>
            (IBrush)new SolidColorBrush(Readable(BaseColor(kind, theme), background, highlights ?? [])));
    }

    private static Color BaseColor(SyntaxKind kind, ReaderTheme theme)
    {
        if (!TokenColors.TryGetValue(kind, out var pair)) return Color.Parse(theme.Foreground);
        return Color.Parse(theme.IsDark ? pair.Dark : pair.Light);
    }

    private static Color Readable(Color color, Color background, IReadOnlyList<Color> highlights)
    {
        var target = UiContrast.Ratio(Colors.White, background) >= UiContrast.Ratio(Colors.Black, background) ? Colors.White : Colors.Black;
        while (color != target && (UiContrast.Ratio(color, background) < 4.5 || highlights.Any(highlight => UiContrast.Ratio(color, highlight) < 4.5)))
            color = Color.FromRgb(Step(color.R, target.R), Step(color.G, target.G), Step(color.B, target.B));
        return color;
    }

    private static byte Step(byte value, byte target) => (byte)(target > value ? Math.Min(255, value + 8) : Math.Max(0, value - 8));
}

// @implements SharedControls
using Avalonia.Media;

namespace Xcmd.Desktop.Shared.Ui;

internal static class UiContrast
{
    public static double Ratio(Color first, Color second)
    {
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public static IBrush SelectionText(Color accent, Color background)
    {
        const double alpha = 110d / 255;
        var composite = Color.FromRgb((byte)(accent.R * alpha + background.R * (1 - alpha)),
            (byte)(accent.G * alpha + background.G * (1 - alpha)), (byte)(accent.B * alpha + background.B * (1 - alpha)));
        return Ratio(Colors.White, composite) >= Ratio(Colors.Black, composite) ? Brushes.White : Brushes.Black;
    }

    private static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    private static double Linear(byte value) => value / 255d <= 0.04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
}

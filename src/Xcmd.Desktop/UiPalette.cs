// @implements SharedControls
using Avalonia;
using Avalonia.Media;

namespace Xcmd.Desktop;

internal static class UiPalette
{
    public const double BodySize = 14;
    public const double MetaSize = 12;
    public const double RowHeight = 28;
    public const double ControlHeight = 30;
    public const double TitleHeight = 32;
    public static readonly Thickness FormMargin = new(12);
    public static readonly Thickness CellPadding = new(8, 0);
    public static readonly Thickness ButtonPadding = new(10, 4);
    public static readonly IBrush Canvas = Brush.Parse("#0E1420");
    public static readonly IBrush Surface = Brush.Parse("#171E2B");
    public static readonly IBrush Raised = Brush.Parse("#263247");
    public static readonly IBrush Text = Brush.Parse("#F1F5FA");
    public static readonly IBrush Secondary = Brush.Parse("#B5C0D0");
    public static readonly IBrush Divider = Brush.Parse("#354157");
    public static readonly IBrush GitIgnored = Brush.Parse("#7B8DA5");
    public static readonly IBrush ControlBorder = Brush.Parse("#7B8DA5");
    public static readonly IBrush Cursor = Brush.Parse("#15558A");
    public static readonly IBrush InactiveCursor = Brush.Parse("#1D2532");
    public static readonly IBrush Primary = Brush.Parse("#185B92");
    public static readonly IBrush Focus = Brush.Parse("#65B8FF");
    public static readonly IBrush Mark = Brush.Parse("#FFD278");
    public static readonly IBrush Match = Brush.Parse("#355A45");
    public static readonly IBrush Error = Brush.Parse("#FFB4AB");
    public static readonly IBrush Success = Brush.Parse("#95D5A6");
}

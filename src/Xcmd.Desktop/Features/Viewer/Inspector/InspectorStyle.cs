// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

internal static class InspectorStyle
{
    internal static readonly IBrush Accent = Brush.Parse("#C7B6FF");
    internal static readonly IBrush AccentSurface = Brush.Parse("#39314E");
    internal static readonly IBrush AccentEdge = Brush.Parse("#8774B6");
    internal static readonly IBrush GridLine = Brush.Parse("#303546");
    internal static readonly IBrush Subtle = Brush.Parse("#202433");
    private static readonly IBrush[] Colors = [Brush.Parse("#87BCE5"), Brush.Parse("#E8B277"), Brush.Parse("#BCA4EF"),
        Brush.Parse("#84CFB5"), Brush.Parse("#C6D28F"), Brush.Parse("#A5B2C4"), Brush.Parse("#CFA7C2"), Brush.Parse("#BAC0CF"), Brush.Parse("#E797A6")];

    internal static IBrush Color(string target) => target switch
    {
        "source" => Colors[0],
        "build_release" => Colors[1],
        "agent_instructions" => Colors[2],
        "tests" => Colors[3],
        "docs" => Colors[4],
        "git" => Colors[5],
        "tracker" => Colors[6],
        "project" => Colors[8],
        _ => Colors[7]
    };
    internal static TextBlock Text(string value, double size = 14, IBrush? color = null, bool strong = false) =>
        new TextBlock
        {
            Text = value,
            FontSize = size,
            Foreground = color ?? UiPalette.Text,
            FontWeight = strong ? FontWeight.SemiBold : FontWeight.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        }.WithFeedbackId("SessionInspector.text");

    internal static Button Action(string title, Action click, string? tip = null)
    {
        var button = new Button
        {
            Content = title,
            FontSize = 13,
            Padding = new Thickness(10, 3),
            MinHeight = 28,
            VerticalContentAlignment = VerticalAlignment.Center
        }.WithFeedbackId("SessionInspector.action");
        button.Click += (_, _) => click();
        if (tip != null) ToolTip.SetTip(button, tip);
        return button;
    }

    internal static Grid Grid(string columns, string rows = "*") =>
        new Grid { ColumnDefinitions = new ColumnDefinitions(columns), RowDefinitions = new RowDefinitions(rows) }.WithFeedbackId("SessionInspector.grid");

    internal static void Put(Grid grid, Control child, int column = 0, int row = 0)
    {
        Avalonia.Controls.Grid.SetColumn(child, column);
        Avalonia.Controls.Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    internal static StackPanel Stack(double spacing = 0) =>
        new StackPanel { Spacing = spacing }.WithFeedbackId("SessionInspector.stack");

    internal static Border Box(Control child, IBrush? fill = null, Thickness? padding = null) =>
        new Border
        {
            Child = child,
            Background = fill ?? UiPalette.Surface,
            BorderBrush = GridLine,
            BorderThickness = new Thickness(1),
            Padding = padding ?? default
        }.WithFeedbackId("SessionInspector.box");

    internal static string Duration(long ms)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        if (ms is > 0 and < 1000) return $"{ms} ms";
        return time.TotalHours >= 1 ? $"{(long)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    internal static string Time(SessionTrace trace, long ms) => Duration(ms - trace.Start);
    internal static string Number(long n) => n.ToString("N0");
}

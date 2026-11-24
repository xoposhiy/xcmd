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
    internal static readonly IBrush MessageFill = Brush.Parse("#68528E");
    private static readonly Dictionary<string, (IBrush Base, IBrush Vivid)> TargetColors = new()
    {
        ["source"] = (Brush.Parse("#87BCE5"), Brush.Parse("#39C5FF")),
        ["tests"] = (Brush.Parse("#84CFB5"), Brush.Parse("#23E1A0")),
        ["docs"] = (Brush.Parse("#C6D28F"), Brush.Parse("#BDDF2D")),
        ["build_release"] = (Brush.Parse("#E8B277"), Brush.Parse("#FFAD43")),
        ["agent_instructions"] = (Brush.Parse("#BCA4EF"), Brush.Parse("#CE9BFF")),
        ["git"] = (Brush.Parse("#C5B587"), Brush.Parse("#E6C76C")),
        ["tracker"] = (Brush.Parse("#CFA7C2"), Brush.Parse("#FF90DB")),
        ["project"] = (Brush.Parse("#E797A6"), Brush.Parse("#FF8AA3")),
        ["unknown"] = (Brush.Parse("#BAC0CF"), Brush.Parse("#DDDDDD"))
    };

    internal static IBrush Color(string target, bool vivid = false)
    {
        var pair = TargetColors.TryGetValue(target, out var colors) ? colors : TargetColors["unknown"];
        return vivid ? pair.Vivid : pair.Base;
    }

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
    internal static string Usd(decimal amount) => "$" + amount.ToString(amount is > 0 and < 0.01m ? "0.00######" : "0.00",
        System.Globalization.CultureInfo.InvariantCulture);

    internal static string CostDetails(SessionResponse response)
    {
        var cost = response.Cost;
        var rows = new List<string> { response.Title, cost.Usd is { } total ? Usd(total) + " API cost" : cost.Unavailable! };
        if (!string.IsNullOrEmpty(cost.Model)) rows.Add(cost.Model);
        rows.AddRange(cost.Parts.Select(part => $"{part.Name} · {Number(part.Tokens)} tokens · {Usd(part.Usd)}"));
        if (cost.Usage?.Reasoning is { } reasoning) rows.Add($"Reasoning · {Number(reasoning)} tokens included in output");
        if (cost.Snapshot != null) rows.Add($"Standard global API rates · {cost.Snapshot}");
        return string.Join("\n", rows);
    }
}

// @tests InspectAgentSession
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using SkiaSharp;
using Xcmd.Core;
using Xcmd.Desktop.Shared.Ui;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

internal static class ResourceTimelineProbe
{
    internal static void UseTrace(AgentSessionFixture sessions)
    {
        var source = new ToolResource("file", "src/main.cs");
        var guide = new ToolResource("file", "docs/d/e/project-guide.md");
        var url = new ToolResource("url", "https://example.test/" + new string('a', 256));
        (TraceCall, decimal) Call(long start, long end, decimal cost, ToolOperation[] pairs, params ToolResource[] resources) =>
            (new(SessionTrace.MainAgent, "Inspect project files", start, end, [])
            { Classification = new("Work with project files.", pairs, resources) }, cost);
        var trace = SessionTimelineFixture.Priced(10000, [
            Call(0, 2000, 0.60m, [new(new("docs", "write"), ["project-guide.md"]), new(new("docs", "read"), ["docs/d/e/project-guide.md"]),
                new(new("source", "read"), [@"src\main.cs"])], source, guide, new("directory", "docs/d")),
            (new("Test runner", "Run checks", 1000, 7000, [])
                { Classification = new("Run checks.", [new(new("tests", "exec"), ["check"])], []) }, 0.10m),
            Call(4000, 7000, 0.20m, [new(new("source", "exec"), ["main.cs"])], source, url),
            Call(8000, 9000, 0.10m, [new(new("source", "read"), ["input"]), new(new("docs", "write"), ["output"])], source, guide),
            Call(9500, 9800, 0, [new(new("source", "exec"), ["main.cs"])], source)
        ]);
        sessions.Inspector = (_, _) => Task.FromResult(trace);
    }

    internal static void UseOrderingTrace(AgentSessionFixture sessions)
    {
        var first = new ToolResource("file", "zeta/nested/z-last.cs");
        var next = new ToolResource("file", "zeta/nested/a-next.cs");
        var other = new ToolResource("file", "alpha/middle.cs");
        TraceCall Call(long start, long end, string action, ToolResource resource) =>
            new(SessionTrace.MainAgent, "Access project resource", start, end, [])
            { Classification = new("Access a project resource.", [new(new("source", action), [resource.Value])], [resource]) };
        var trace = new SessionTrace(0, 10000, [
            Call(0, 250, "read", new("directory", "alpha")),
            Call(500, 5000, "read", first),
            Call(1500, 2000, "write", other),
            Call(2500, 3000, "write", next),
            Call(8000, 8500, "write", first)
        ], [], []);
        sessions.Inspector = (_, _) => Task.FromResult(trace);
    }

    internal static async Task CheckColumnWidthsAsync(ProbeContext c, InspectorProbe probe)
    {
        var guide = probe.Find<TextBlock>(t => t.Text == "project-guide.md");
        var table = probe.Find<TextBlock>(t => t.Text == "Activity breakdown").FindAncestorOfType<Border>()!;
        var activityWidth = table.Bounds.Width;
        var initialWidth = probe.Window.Bounds.Width;
        var normal = Plot(guide).Bounds.Width;
        Check(!probe.HasText("A call can appear under several target/action pairs"), "The overview omits the target/action explanation");
        var header = probe.Find<TextBlock>(t => t.Text == "Target").FindAncestorOfType<Grid>()!;
        var cells = table.GetVisualDescendants().OfType<Control>()
            .Where(t => t is TextBlock or Button && t.Parent is Grid row && row.ColumnDefinitions.Count == 4);
        foreach (var column in cells.GroupBy(Grid.GetColumn))
        {
            var contentWidth = column.Max(t => t is TextBlock text ? text.TextLayout.Width + t.Margin.Left + t.Margin.Right : t.DesiredSize.Width);
            var width = header.ColumnDefinitions[column.Key].ActualWidth - header.ColumnSpacing;
            Check(Math.Abs(width - contentWidth) < 2,
                $"Activity column {column.Key} fits its widest heading or value ({width:F1} wide, {contentWidth:F1} content)");
        }
        probe.Window.Width = 1920;
        await c.SettleAsync();
        var wide = Plot(guide).Bounds.Width;
        Check(table.Bounds.Width == activityWidth, "Widening the inspector keeps the activity table at its content width");
        Check(Math.Abs((wide - normal) - (probe.Window.Bounds.Width - initialWidth)) <= 1,
            "All additional window width goes to the resource timeline");
        Check(Math.Abs(guide.Bounds.Width - guide.TextLayout.Width) < 2, "The longest visible file name fits its text");
        double Start(Control plot) => plot.TranslatePoint(default, probe.Window)!.Value.X;
        var start = Start(Plot(guide));
        var axis = probe.Find<Grid>(p => p.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.resourceHeader")
            .Children.OfType<Grid>().Single();
        var source = probe.Find<TextBlock>(t => t.Text == "main.cs");
        Check(Math.Abs(start - Start(axis)) < 1 && Math.Abs(start - Start(Plot(source))) < 1,
            "The time axis and each resource timeline start at the same position");
        await probe.Screenshot("resource-columns-wide");

        await probe.ToggleKind("URLs");
        guide = probe.Find<TextBlock>(t => t.Text == "project-guide.md");
        Check(Plot(guide).Bounds.Width < wide - 200, "Showing a longer name expands the shared path column");
        probe.Window.Width = 900;
        await c.SettleAsync();
        Check(Plot(guide).Bounds.Width >= 80, "A very long URL leaves a readable timeline after resizing");
        await probe.ToggleGroup("Collapse example.test");
        Check(Plot(guide).Bounds.Width > 200, "Collapsing the URL group releases its column width");
        await probe.ToggleKind("URLs");
    }

    private static Control Plot(TextBlock name) => name.FindAncestorOfType<Button>()!.GetVisualDescendants().OfType<Control>()
        .Single(control => control.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.resourceTimeline");

    internal static void CheckRowSpacing(InspectorProbe probe)
    {
        var rows = probe.Window.GetVisualDescendants().OfType<Button>().Where(button =>
            button.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.resourceGroup" ||
            button.Content is Grid grid && grid.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.resourceRow").ToArray();
        Check(rows.Length >= 5 && rows.All(row => Math.Abs(row.Bounds.Height - 20) < 1), "Files and folder controls use the same compact height");
        var tops = rows.Select(row => row.TranslatePoint(default, probe.Window)!.Value.Y).Distinct().Order().ToArray();
        Check(tops.Zip(tops.Skip(1)).All(pair => Math.Abs(pair.Second - pair.First - 20) < 1),
            "Toggled folders and file rows line up without vertical gaps");
    }

    internal static async Task CheckColorsAsync(ProbeContext c, InspectorProbe probe)
    {
        probe.Window.Width = 650;
        probe.Window.Height = 650;
        probe.Window.SetScaleForTest(1.5);
        var scope = probe.Find<ComboBox>(_ => true);
        scope.Focus();
        await probe.Key(Avalonia.Input.Key.Down);
        Equal(SessionTrace.MainAgent, scope.SelectedItem as string, "Inspect only the main agent's resources");
        var read = TargetColor("source");
        var docsRead = TargetColor("docs");
        var docsEdit = new SKColor(0xBD, 0xDF, 0x2D);
        var sourceRun = new SKColor(0x39, 0xC5, 0xFF);
        var other = new SKColor(0xBA, 0xC0, 0xCF);
        const string endCost = "$1.00";
        CheckAxis("API cost $0.00", endCost);
        using (var image = await Capture("resource-timeline-cost"))
        {
            Equal(read, Pixel(image, "main.cs", 0.58, 0.17), "The $0.60 read fills the first 60 percent");
            CheckOverlap(image);
            Check(Pixel(image, "main.cs", 0.65, 0.50) != sourceRun, "The hidden agent retains its $0.10 interval");
            Equal(sourceRun, Pixel(image, "main.cs", 0.72, 0.50), "The $0.20 command uses the vivid source color");
            Equal(other, Pixel(image, "main.cs", 0.95, 0.83), "The last priced interval belongs to the compound call");
            Check(!HasMark(image, "main.cs", 0.30), "A read keeps the base target color without an icon");
            Check(HasMark(image, "project-guide.md", 0.30) && HasMark(image, "main.cs", 0.80),
                "The pencil and play icons are centered on their calls on the cost axis");
        }
        probe.RangeHandle("Range start").Focus();
        await probe.Key(Avalonia.Input.Key.Right);
        CheckAxis("API cost $0.60", endCost);
        using (var image = await Capture("resource-timeline-cost-range"))
        {
            Check(Pixel(image, "main.cs", 0.12, 0.25) != sourceRun, "The selected range starts with the hidden agent's slot");
            Equal(sourceRun, Pixel(image, "main.cs", 0.45, 0.25), "The command fills half of the selected $0.40");
        }
        await probe.Click("Time");
        CheckAxis("Time 0:00", "0:10");
        Equal(TargetColor("source"), BrushColor(FileName("main.cs").Foreground),
            "The source file uses the source target color");
        Equal(TargetColor("docs"), BrushColor(FileName("project-guide.md").Foreground),
            "A document in the same call uses the docs target color");
        using (var image = await Capture("resource-timeline-150"))
        {
            Equal(read, Pixel(image, "main.cs", 0.10, 0.17), "The first call reads the source file");
            CheckOverlap(image);
            Check(HasMark(image, "main.cs", 0.55) && HasMark(image, "project-guide.md", 0.10),
                "The pencil and play icons follow their calls on the time axis");
            Equal(sourceRun, Pixel(image, "main.cs", 0.55, 0.95), "The command fills the resource bar vertically");
            Equal(read, Pixel(image, "main.cs", 0.10, 0.95), "The read fills the resource bar vertically");
            Equal(other, Pixel(image, "main.cs", 0.85, 0.83), "A compound call with no matching object keeps the action unknown");
            Check(Pixel(image, "main.cs", 0.30, 0.17) != read && Pixel(image, "project-guide.md", 0.55, 0.50) != docsEdit, "The time axis leaves idle time empty");
        }
        await probe.Click("API cost");
        CheckAxis("API cost $0.00", endCost);

        void CheckOverlap(SKBitmap image)
        {
            var width = Plot(FileName("project-guide.md")).Bounds.Width;
            foreach (var (offset, expected) in new[] { (2, docsRead), (6, docsEdit) })
            {
                Equal(expected, Pixel(image, "project-guide.md", offset / width, 0.10), "Both simultaneous actions reach the top of the bar");
                Equal(expected, Pixel(image, "project-guide.md", offset / width, 0.90), "Both simultaneous actions reach the bottom of the bar");
            }
        }
        void CheckAxis(string from, string to)
        {
            var header = probe.Find<Grid>(g => g.GetValue(FeedbackTarget.IdProperty) == "SessionInspector.resourceHeader");
            var labels = header.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
            Check(labels.Contains(from) && labels.Contains(to), $"Resource axis runs from {from} to {to}");
        }
        TextBlock FileName(string name) => probe.Find<TextBlock>(t => t.Text == name);
        async Task<SKBitmap> Capture(string file)
        {
            FileName("main.cs").BringIntoView();
            await c.SettleAsync();
            await probe.Screenshot(file);
            return SKBitmap.Decode(Path.Combine(c.OutputPath, file + ".png"));
        }
        SKColor TargetColor(string target) => BrushColor(probe.Find<TextBlock>(t => Equals(ToolTip.GetTip(t), target)).Foreground);
        static SKColor BrushColor(IBrush? brush)
        {
            var color = ((ISolidColorBrush)brush!).Color;
            return new SKColor(color.R, color.G, color.B, color.A);
        }
        bool HasMark(SKBitmap image, string name, double fraction)
        {
            var plot = Plot(FileName(name));
            var center = plot.TranslatePoint(new Point(plot.Bounds.Width * fraction, plot.Bounds.Height / 2), probe.Window)!.Value;
            var scale = probe.Window.RenderScaling;
            var ink = 0;
            for (var y = (int)((center.Y - 6) * scale); y < (center.Y + 7) * scale; y++)
                for (var x = (int)((center.X - 7) * scale); x < (center.X + 7) * scale; x++)
                    if (image.GetPixel(x, y) is { Red: 241, Green: 245, Blue: 250 }) ink++;
            return ink >= 10 * scale * scale;
        }
        SKColor Pixel(SKBitmap image, string name, double x, double y)
        {
            var plot = Plot(FileName(name));
            var point = plot.TranslatePoint(new Point(Math.Min(plot.Bounds.Width - 1, plot.Bounds.Width * x), plot.Bounds.Height * y), probe.Window)!.Value;
            return image.GetPixel((int)(point.X * probe.Window.RenderScaling), (int)(point.Y * probe.Window.RenderScaling));
        }
    }
}

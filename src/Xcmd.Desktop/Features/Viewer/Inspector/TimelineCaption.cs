// @implements InspectAgentSession
using Avalonia;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

internal static class TimelineCaption
{
    private static readonly DrawingImage Edit = Symbol("M0,12 L1,8 L8,1 L11,4 L4,11 Z M9,0 L10,-1 L13,2 L12,3 Z");
    private static readonly DrawingImage Run = Symbol("M0,0 L10,6 L0,12 Z");

    private static DrawingImage Symbol(string path) => new()
    {
        Drawing = new GeometryDrawing { Geometry = Geometry.Parse(path), Brush = UiPalette.Text, Pen = new Pen(UiPalette.Canvas, 1.25) }
    };

    internal static string Name(string target) => target switch
    {
        "agent_instructions" => "Agent instructions",
        "build_release" => "Build",
        "git" => "Git",
        "source" => "Source",
        "docs" => "Docs",
        "tests" => "Tests",
        "project" => "Project",
        "tracker" => "Tracker",
        _ => "Unknown"
    };

    internal static string Describe(TimelineCategory category)
    {
        var resources = category.Files.Concat(category.Repositories.Select(repo => $"Repository: {repo}")).ToArray();
        return $"{Name(category.Target)}:\n{(resources.Length == 0 ? "No files identified" : string.Join('\n', resources))}";
    }

    internal static double MarkWidth(TimelineMark mark) => (mark.Edit ? Edit.Size.Width : 0) + (mark.Run ? Run.Size.Width : 0)
        + (mark.Edit && mark.Run ? 2 : 0);

    internal static void Draw(DrawingContext context, Rect bounds, TimelineMark mark)
    {
        if (mark.Edit) DrawSymbol(Edit, bounds.Left);
        if (mark.Run) DrawSymbol(Run, bounds.Right - Run.Size.Width);

        void DrawSymbol(DrawingImage symbol, double left) =>
            context.DrawImage(symbol, new Rect(new Point(left, bounds.Center.Y - symbol.Size.Height / 2), symbol.Size));
    }
}

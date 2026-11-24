// @implements InspectAgentSession
using Avalonia;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

internal static class TimelineCaption
{
    private static readonly Geometry ReadShape = Geometry.Parse("M0,6 Q6,-2 12,6 Q6,14 0,6 Z M2,6 Q6,0 10,6 Q6,12 2,6 Z M4,6 A2,2 0 1 1 8,6 A2,2 0 1 1 4,6 Z");
    private static readonly Geometry EditShape = Geometry.Parse("M0,12 L1,8 L8,1 L11,4 L4,11 Z M9,0 L10,-1 L13,2 L12,3 Z");
    private static readonly Geometry RunShape = Geometry.Parse("M0,0 L10,6 L0,12 Z");
    private static readonly Geometry OtherShape = Geometry.Parse("M0,0 H2 V2 H0 Z M5,0 H7 V2 H5 Z M10,0 H12 V2 H10 Z");
    private static readonly DrawingImage Edit = Symbol(EditShape);
    private static readonly DrawingImage Run = Symbol(RunShape);

    private static DrawingImage Symbol(Geometry shape) => new()
    {
        Drawing = new GeometryDrawing { Geometry = shape, Brush = UiPalette.Text, Pen = new Pen(UiPalette.Canvas, 1.25) }
    };

    internal static (string Name, Geometry Shape)? ActionIcon(string action) => action switch
    {
        "read" => ("Read", ReadShape),
        "write" => ("Write", EditShape),
        "exec" => ("Execute", RunShape),
        "other" => ("Other", OtherShape),
        _ => null
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

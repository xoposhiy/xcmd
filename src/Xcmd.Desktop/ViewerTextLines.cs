// @implements ReadLargeFiles, ReadWorkingFileDiff
using Avalonia.Media.TextFormatting;

namespace Xcmd.Desktop;

internal sealed class ViewerTextLines
{
    private TextLayout? layout;
    private double[] starts = [];

    public IReadOnlyList<TextLine> Use(TextLayout value)
    {
        if (ReferenceEquals(layout, value)) return value.TextLines;
        layout = value;
        var lines = value.TextLines;
        starts = new double[lines.Count + 1];
        for (var index = 0; index < lines.Count; index++)
            starts[index + 1] = starts[index] + lines[index].Height;
        return lines;
    }

    public int At(double y)
    {
        var index = Array.BinarySearch(starts, y);
        return Math.Clamp(index >= 0 ? index : ~index - 1, 0, Math.Max(0, starts.Length - 2));
    }

    public double Top(int line) => starts[line];
}

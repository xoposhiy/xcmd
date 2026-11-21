// @implements ChooseViewMode, ReadLargeFiles, FindViewerText
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

internal readonly record struct StructuredJsonSegment(int SourceStart, int SourceEnd, int Start, int End, int? Fold = null);
internal readonly record struct StructuredJsonLine(StructuredJsonRow Row, int SourceStart, int Start, int End);

internal sealed class StructuredJsonPage(SyntaxDocument syntax, IReadOnlyList<StructuredJsonSegment> segments,
    IReadOnlyList<StructuredJsonLine> lines, int sourceEnd, bool hasPrevious, bool hasNext)
{
    public SyntaxDocument Syntax { get; } = syntax;
    public int Start => lines[0].SourceStart;
    public int End => sourceEnd;
    public bool HasPrevious => hasPrevious;
    public bool HasNext => hasNext;

    public int ToDisplay(int position, bool trailing = false)
    {
        if (position < Start) return 0;
        if (position >= End) return Syntax.Text.Length;
        var index = Find(segments, segment => trailing ? segment.SourceEnd < position : segment.SourceEnd <= position);
        if (index == segments.Count) return Syntax.Text.Length;
        var segment = segments[index];
        if (position <= segment.SourceStart)
            return trailing && index > 0 ? segments[index - 1].End : segment.Start;
        if (segment.Fold != null) return trailing ? segment.End : segment.Start;
        return segment.Start + position - segment.SourceStart;
    }

    public int ToSource(int position, bool trailing = false)
    {
        if (position >= Syntax.Text.Length) return End;
        var index = Find(segments, segment => trailing ? segment.End < position : segment.End <= position);
        if (index == segments.Count) return End;
        var segment = segments[index];
        if (position <= segment.Start)
            return trailing && index > 0 ? segments[index - 1].SourceEnd : segment.SourceStart;
        if (segment.Fold != null) return trailing ? segment.SourceEnd : segment.SourceStart;
        return segment.SourceStart + position - segment.Start;
    }

    public int? FoldAt(int position)
    {
        var index = Find(segments, segment => segment.End <= position);
        return index < segments.Count && position >= segments[index].Start ? segments[index].Fold : null;
    }

    public StructuredJsonLine LineAt(int position)
    {
        var index = Find(lines, line => line.End <= position);
        return lines[Math.Min(index, lines.Count - 1)];
    }

    private static int Find<T>(IReadOnlyList<T> values, Func<T, bool> before)
    {
        var low = 0;
        var high = values.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (before(values[middle])) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}

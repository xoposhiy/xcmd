// @implements ChooseViewMode, ReadLargeFiles, HighlightSource
using System.Text;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed class StructuredJsonLayout(StructuredJsonSource source, StructuredJsonFolds folds)
{
    private const int CompactWidth = 104;
    private static readonly SyntaxLanguage Language = new("json", "JSON", "source.json");
    private readonly StructuredJsonRows rows = new(source, folds);
    private readonly StringBuilder text = new();
    private readonly List<SyntaxSpan> colors = [];
    private readonly List<StructuredJsonLine> lines = [];
    private readonly List<StructuredJsonSegment> segments = [];

    public StructuredJsonPage Render(int position, int screenRows)
    {
        var anchor = rows.At(position);
        var start = anchor;
        for (var count = 0; count < screenRows && rows.Previous(start) is { } before; count++) start = before;
        var hasPrevious = rows.Previous(start) != null;
        var end = 0;
        var remaining = 3 * screenRows;
        var passedAnchor = false;
        StructuredJsonRow? next = start;
        while (next is { } row)
        {
            passedAnchor |= row == anchor;
            end = rows.End(row);
            WriteRow(row, end);
            next = rows.Next(row);
            if (passedAnchor && --remaining == 0) break;
        }
        return new(new(text.ToString(), Language, colors), segments, lines, end, hasPrevious, next != null);
    }

    private void WriteRow(StructuredJsonRow row, int end)
    {
        var element = source.Tree[row.Element];
        var sourceStart = rows.Start(row);
        var start = text.Length;
        if (lines.Count != 0) text.Append('\n');
        // Deep files can be read with the path in the status bar even when indentation reaches the edge.
        var indent = Math.Min(60, element.Depth * 2);
        text.Append(' ', indent);
        var index = TokenAt(sourceStart);
        if (!row.Closing && element.CanFold && !rows.Expanded(row.Element) && !IsShort(element))
        {
            WriteTokens(ref index, element.ValueStart + 1, indent);
            WriteFold(row.Element, element);
            index = TokenAt(element.End - 1);
        }
        WriteTokens(ref index, end, indent);
        lines.Add(new(row, sourceStart, start, text.Length));
    }

    private bool IsShort(StructuredJsonElement element)
    {
        var width = 0;
        for (var index = TokenAt(element.ValueStart); index < source.Tokens.Count; index++)
        {
            var token = source.Tokens[index];
            if (token.Start >= element.End) break;
            width += token.End - token.Start + 1;
            if (width > CompactWidth || token.EndsLine ||
                source.Text.AsSpan(token.Start, token.End - token.Start).IndexOfAny('\r', '\n') >= 0) return false;
        }
        return true;
    }

    private void WriteTokens(ref int index, int end, int indent)
    {
        StructuredJsonToken? previous = null;
        while (index < source.Tokens.Count && source.Tokens[index].Start < end)
        {
            var token = source.Tokens[index++];
            if (previous is { EndsLine: true }) text.Append('\n').Append(' ', indent);
            else if (previous is { } prior && NeedsSpace(prior, token)) text.Append(' ');
            var start = text.Length;
            text.Append(source.Text.AsSpan(token.Start, token.End - token.Start));
            AddSegment(token.Start, token.End, start, token.Syntax);
            previous = token;
        }
    }

    private bool NeedsSpace(StructuredJsonToken prior, StructuredJsonToken next) =>
        source.Text[prior.Start] is ':' or ',' || prior.Syntax == SyntaxKind.Comment || next.Syntax == SyntaxKind.Comment;

    private void WriteFold(int id, StructuredJsonElement element)
    {
        var start = text.Length;
        var unit = element.Kind == JsonElementKind.Object ? "field" : "item";
        var count = element.Children.Count;
        text.Append($"⟪… {count:N0} {unit}{(count == 1 ? "" : "s")}⟫");
        AddSegment(element.ValueStart + 1, element.End - 1, start, SyntaxKind.Operator, id);
    }

    private void AddSegment(int sourceStart, int sourceEnd, int start, SyntaxKind kind, int? fold = null)
    {
        segments.Add(new(sourceStart, sourceEnd, start, text.Length, fold));
        if (kind != SyntaxKind.Plain) colors.Add(new(start, text.Length - start, kind, Language.Scope));
    }

    private int TokenAt(int position)
    {
        var low = 0;
        var high = source.Tokens.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (source.Tokens[middle].Start < position) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}

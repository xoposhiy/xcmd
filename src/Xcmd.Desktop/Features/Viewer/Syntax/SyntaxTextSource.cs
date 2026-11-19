// @implements HighlightSource, SyntaxSpans, ReadLargeFiles, ReadWorkingFileDiff
using System.Globalization;
using Avalonia.Media.TextFormatting;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer.Syntax;

internal sealed class SyntaxColorRuns
{
    private readonly List<int> ends = [];
    private readonly List<SyntaxKind> kinds = [];
    public string Text { get; }

    public SyntaxColorRuns(SyntaxDocument document)
    {
        Text = document.Text;
        var spans = document.Spans;
        var token = 0;
        for (var position = 0; position < Text.Length;)
        {
            while (token < spans.Count && spans[token].Start + spans[token].Length <= position) token++;
            var kind = token < spans.Count && spans[token].Start <= position ? spans[token].Kind : SyntaxKind.Plain;
            position += StringInfo.GetNextTextElementLength(Text.AsSpan(position));
            if (kinds.Count > 0 && kinds[^1] == kind) ends[^1] = position;
            else { kinds.Add(kind); ends.Add(position); }
        }
    }

    public (SyntaxKind Kind, int End) At(int position)
    {
        var index = ends.BinarySearch(position + 1);
        if (index < 0) index = ~index;
        return (kinds[index], ends[index]);
    }
}

internal sealed class SyntaxTextSource(SyntaxColorRuns runs, IReadOnlyDictionary<SyntaxKind, TextRunProperties> styles) : ITextSource
{
    public TextRun? GetTextRun(int textSourceIndex)
    {
        if (textSourceIndex >= runs.Text.Length) return null;
        var (kind, end) = runs.At(textSourceIndex);
        return new TextCharacters(runs.Text.AsMemory(textSourceIndex, end - textSourceIndex), styles[kind]);
    }
}

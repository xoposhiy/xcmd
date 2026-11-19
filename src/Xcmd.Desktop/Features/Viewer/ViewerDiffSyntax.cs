// @implements ReadWorkingFileDiff, HighlightSource
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

internal static class ViewerDiffSyntax
{
    public static SyntaxDocument? Highlight(ViewerGitComparison comparison, string text, int[] starts, SyntaxHighlighter highlighter,
        string path, string? languageId, CancellationToken token)
    {
        var language = languageId == null ? SyntaxHighlighter.Detect(path, Prefix(comparison.WorkingText))
            : SyntaxHighlighter.Languages.FirstOrDefault(item => item.Id == languageId);
        if (language == null) return null;

        Source HighlightSource(string source) => new(highlighter.Highlight(Prefix(source), language, token));
        var working = HighlightSource(comparison.WorkingText);
        var index = comparison.Rows.Any(row => !row.OnDisk && row.Kind != ViewerDiffKind.StagedDeletion)
            ? HighlightSource(comparison.IndexText) : null;
        var head = comparison.Rows.Any(row => row.Kind == ViewerDiffKind.StagedDeletion)
            ? HighlightSource(comparison.HeadText) : null;
        var spans = new List<SyntaxSpan>();
        for (var rowIndex = 0; rowIndex < comparison.Rows.Count; rowIndex++)
        {
            token.ThrowIfCancellationRequested();
            var row = comparison.Rows[rowIndex];
            var source = row.OnDisk ? working : row.Kind == ViewerDiffKind.StagedDeletion ? head : index;
            source?.CopyLine(row.OnDisk ? row.WorkingLine : row.SourceLine, starts[rowIndex], row.Text.Length, spans);
        }
        return new(text, language, spans, spans.Count == 0 ? working.Notice : null);
    }

    private static string Prefix(string text)
    {
        if (text.Length <= SyntaxHighlighter.MaximumContextBytes) return text;
        var end = text.LastIndexOf('\n', SyntaxHighlighter.MaximumContextBytes - 1);
        return end < 0 ? "" : text[..(end + 1)];
    }

    private sealed class Source(SyntaxDocument document)
    {
        private readonly int[] lineStarts = LineStarts(document.Text);
        public string? Notice => document.Notice;

        public void CopyLine(int line, int destination, int length, List<SyntaxSpan> result)
        {
            if (line >= lineStarts.Length || length == 0) return;
            var start = lineStarts[line];
            var end = start + length;
            var spans = document.Spans;
            for (var position = FirstSpanAt(start); position < spans.Count && spans[position].Start < end; position++)
            {
                var span = spans[position];
                result.Add(span with { Start = destination + span.Start - start });
            }
        }

        private int FirstSpanAt(int start)
        {
            var spans = document.Spans;
            var low = 0;
            var high = spans.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (spans[middle].Start < start) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        private static int[] LineStarts(string text)
        {
            var starts = new List<int> { 0 };
            for (var position = text.IndexOf('\n'); position >= 0; position = text.IndexOf('\n', position + 1))
                starts.Add(position + 1);
            return starts.ToArray();
        }
    }
}

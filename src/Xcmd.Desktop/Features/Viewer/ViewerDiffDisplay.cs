// @implements ReadWorkingFileDiff, RestoreWorkspace
using System.Text;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed class ViewerDiffDisplay
{
    private readonly ViewerGitComparison comparison;
    private readonly IReadOnlyList<ViewerDiffRow> rows;
    private readonly int[] starts;
    private readonly int[] workingPositions;
    private readonly long[] workingOffsets;
    private readonly IReadOnlyDictionary<int, ViewerDiffEdit[]> edits;
    private SyntaxDocument? syntax;
    public string Text { get; }
    public int[] Changes { get; }
    public long WorkingLength { get; }

    public ViewerDiffDisplay(ViewerGitComparison comparison, CancellationToken token = default)
    {
        this.comparison = comparison;
        rows = comparison.Rows;
        workingOffsets = comparison.WorkingLineOffsets;
        WorkingLength = comparison.WorkingLength;
        starts = new int[rows.Count];
        workingPositions = new int[rows.Count(row => row.OnDisk)];
        var changes = new List<int>();
        var text = new StringBuilder();
        var changed = false;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            starts[index] = text.Length;
            if (row.OnDisk) workingPositions[row.WorkingLine] = text.Length;
            if (!changed && row.Kind != ViewerDiffKind.Context) changes.Add(text.Length);
            changed = row.Kind != ViewerDiffKind.Context;
            text.Append(row.Text);
            if (index < rows.Count - 1 || comparison.EndsWithNewline || row.Text.Length == 0) text.Append('\n');
        }
        Text = text.ToString();
        Changes = changes.ToArray();
        edits = ViewerDiffEdits.Find(rows, token);
    }

    public async Task HighlightAsync(SyntaxHighlighter highlighter, string path, string? languageId, CancellationToken token)
    {
        var document = await Task.Run(() => ViewerDiffSyntax.Highlight(comparison, Text, starts, highlighter, path, languageId, token), token);
        token.ThrowIfCancellationRequested();
        syntax = document;
    }

    public SyntaxDocument? SyntaxPage(int start, int length) => syntax?.Select(start, length);

    public int RowAt(int position)
    {
        var index = Array.BinarySearch(starts, position);
        return index >= 0 ? index : ~index - 1;
    }

    public (int Start, ViewerDiffKind Kind) Row(int row) => (starts[row], rows[row].Kind);
    public ViewerDiffEdit[] Edits(int row) => edits.GetValueOrDefault(row) ?? [];

    public long WorkingOffset(int position)
    {
        var row = RowAt(position);
        if (row < 0) return 0;
        return workingOffsets[Math.Min(rows[row].WorkingLine, workingOffsets.Length - 1)];
    }

    public int PositionAtWorkingOffset(long offset)
    {
        if (workingPositions.Length == 0 || offset <= workingOffsets[0]) return 0;
        var line = Array.BinarySearch(workingOffsets, offset);
        return workingPositions[Math.Clamp(line >= 0 ? line : ~line - 1, 0, workingPositions.Length - 1)];
    }

    public int RestorePosition(long workingOffset, int? displayPosition)
    {
        // Deleted rows share a working offset with the next row on disk.
        if (displayPosition is >= 0 && displayPosition < Text.Length && WorkingOffset(displayPosition.Value) == workingOffset)
            return displayPosition.Value;
        return PositionAtWorkingOffset(workingOffset);
    }
}

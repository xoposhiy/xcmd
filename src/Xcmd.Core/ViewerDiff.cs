// @implements ReadWorkingFileDiff
namespace Xcmd.Core;

public enum ViewerGitStatus { OutsideRepository, NotTracked, Ignored, Unchanged, Changed, Staged, PartiallyStaged, Conflicted, Unavailable }

public enum ViewerDiffKind { Context, StagedAddition, StagedDeletion, UnstagedAddition, UnstagedDeletion, Addition, Deletion }

public sealed record ViewerDiffRow(string Text, ViewerDiffKind Kind, int WorkingLine, bool OnDisk = false, int SourceLine = 0);
public sealed record ViewerDiffRange(int BeforeStart, int BeforeCount, int AfterStart, int AfterCount);

public static class ViewerDiff
{
    public static string[] Lines(string text)
    {
        if (text.Length == 0) return [];
        var lines = text.Split('\n');
        return text.EndsWith('\n') ? lines[..^1] : lines;
    }

    public static IReadOnlyList<ViewerDiffRow> Merge(string[] head, string[] index, string[] working,
        IReadOnlyList<ViewerDiffRange> staged, IReadOnlyList<ViewerDiffRange> unstaged, bool conflicted)
    {
        var stagedRemovals = staged.Where(range => range.BeforeCount > 0).ToDictionary(range => range.AfterStart);
        var workingAdditions = unstaged.Where(range => range.AfterCount > 0).ToDictionary(range => range.BeforeStart + range.BeforeCount);
        var stagedAdditions = MarkLines(index.Length, staged.Select(range => (range.AfterStart, range.AfterCount)));
        var workingRemovals = MarkLines(index.Length, unstaged.Select(range => (range.BeforeStart, range.BeforeCount)));
        var result = new List<ViewerDiffRow>(working.Length);
        var workingLine = 0;
        var addition = conflicted ? ViewerDiffKind.Addition : ViewerDiffKind.UnstagedAddition;
        var deletion = conflicted ? ViewerDiffKind.Deletion : ViewerDiffKind.UnstagedDeletion;

        // The index is the shared coordinate system for both comparisons.
        for (var position = 0; position <= index.Length; position++)
        {
            if (stagedRemovals.TryGetValue(position, out var removed))
                for (var line = removed.BeforeStart; line < removed.BeforeStart + removed.BeforeCount; line++)
                    result.Add(new(head[line], ViewerDiffKind.StagedDeletion, workingLine, SourceLine: line));
            if (workingAdditions.TryGetValue(position, out var added))
                for (var line = added.AfterStart; line < added.AfterStart + added.AfterCount; line++)
                    result.Add(new(working[line], addition, workingLine++, true));
            if (position == index.Length) break;

            var kind = stagedAdditions[position] ? ViewerDiffKind.StagedAddition : ViewerDiffKind.Context;
            if (workingRemovals[position])
            {
                if (stagedAdditions[position]) result.Add(new(index[position], kind, workingLine, SourceLine: position));
                result.Add(new(index[position], deletion, workingLine, SourceLine: position));
            }
            else result.Add(new(working[workingLine], kind, workingLine++, true));
        }
        return result;
    }

    private static bool[] MarkLines(int length, IEnumerable<(int Start, int Count)> ranges)
    {
        var result = new bool[length];
        foreach (var (start, count) in ranges) Array.Fill(result, true, start, count);
        return result;
    }
}

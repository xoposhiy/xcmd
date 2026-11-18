// @implements ReadWorkingFileDiff
using System.Buffers;

namespace Xcmd.Core;

public readonly record struct ViewerDiffEdit(int Start, int Length);

public static class ViewerDiffEdits
{
    public static IReadOnlyDictionary<int, ViewerDiffEdit[]> Find(IReadOnlyList<ViewerDiffRow> rows, CancellationToken token = default)
    {
        var edits = new Dictionary<int, ViewerDiffEdit[]>();
        var budget = 8_000_000;
        for (var start = 0; start < rows.Count;)
        {
            token.ThrowIfCancellationRequested();
            if (rows[start].Kind == ViewerDiffKind.Context) { start++; continue; }
            var end = start + 1;
            while (end < rows.Count && rows[end].Kind != ViewerDiffKind.Context) end++;
            Pair(rows, start, end, ViewerDiffKind.StagedDeletion, ViewerDiffKind.StagedAddition, edits, ref budget, token);
            Pair(rows, start, end, ViewerDiffKind.UnstagedDeletion, ViewerDiffKind.UnstagedAddition, edits, ref budget, token);
            Pair(rows, start, end, ViewerDiffKind.Deletion, ViewerDiffKind.Addition, edits, ref budget, token);
            start = end;
        }
        return edits;
    }

    private static void Pair(IReadOnlyList<ViewerDiffRow> rows, int start, int end, ViewerDiffKind deletion, ViewerDiffKind addition,
        Dictionary<int, ViewerDiffEdit[]> edits, ref int budget, CancellationToken token)
    {
        var additions = new List<int>();
        for (var row = start; row < end; row++)
            if (rows[row].Kind == addition) additions.Add(row);
        var next = 0;
        for (var row = start; row < end && next < additions.Count; row++)
        {
            if (rows[row].Kind != deletion) continue;
            token.ThrowIfCancellationRequested();
            Match? best = null;
            var chosen = next;
            for (var candidate = next; candidate < Math.Min(next + 4, additions.Count); candidate++)
            {
                var match = Compare(rows[row].Text, rows[additions[candidate]].Text, ref budget);
                if (match == null || match.Score <= (best?.Score ?? 0)) continue;
                best = match;
                chosen = candidate;
                if (match.Score == 1) break;
            }
            if (best == null) continue;
            edits[row] = best.Before;
            edits[additions[chosen]] = best.After;
            next = chosen + 1;
        }
    }

    private sealed record Match(double Score, ViewerDiffEdit[] Before, ViewerDiffEdit[] After);

    private static Match? Compare(string before, string after, ref int budget)
    {
        var length = Math.Max(before.Length, after.Length);
        if (length == 0 || Math.Min(before.Length, after.Length) < length * 0.6) return null;
        var prefix = before.AsSpan().CommonPrefixLength(after);
        if (prefix == length) return new(1, [], []);
        if (prefix > 0 && char.IsHighSurrogate(before[prefix - 1])) prefix--;
        var suffix = Suffix(before.AsSpan(prefix), after.AsSpan(prefix));
        var left = before.AsSpan(prefix, before.Length - prefix - suffix);
        var right = after.AsSpan(prefix, after.Length - prefix - suffix);
        var pairs = CommonPairs(left, right, ref budget);
        var shared = prefix + suffix + pairs.Count;
        if (shared < 2 || shared < length * 0.6 || !HasContent(before, prefix, suffix, pairs)) return null;
        return new((double)shared / length, Edits(before, prefix, suffix, pairs.Select(pair => pair.Left)),
            Edits(after, prefix, suffix, pairs.Select(pair => pair.Right)));
    }

    private static int Suffix(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var length = 0;
        while (length < Math.Min(left.Length, right.Length) && left[^(length + 1)] == right[^(length + 1)]) length++;
        return length > 0 && char.IsLowSurrogate(left[^length]) ? length - 1 : length;
    }

    private static bool HasContent(string text, int prefix, int suffix, List<(int Left, int Right)> pairs) =>
        !text.AsSpan(0, prefix).IsWhiteSpace() || !text.AsSpan(text.Length - suffix).IsWhiteSpace() ||
        pairs.Any(pair => !char.IsWhiteSpace(text[prefix + pair.Left]));

    private static List<(int Left, int Right)> CommonPairs(ReadOnlySpan<char> left, ReadOnlySpan<char> right, ref int budget)
    {
        if (left.IsEmpty || right.IsEmpty || left.Length > 256 || right.Length > 256) return [];
        var width = right.Length + 1;
        var cells = (left.Length + 1) * width;
        if (cells > budget) return [];
        budget -= cells;
        var table = ArrayPool<int>.Shared.Rent(cells);
        try
        {
            FillTable(left, right, table, width, cells);
            return ReadPairs(left, right, table, width);
        }
        finally { ArrayPool<int>.Shared.Return(table); }
    }

    private static void FillTable(ReadOnlySpan<char> left, ReadOnlySpan<char> right, int[] table, int width, int cells)
    {
        Array.Clear(table, left.Length * width, width);
        for (var i = cells - width - 1; i >= 0; i--)
        {
            var column = i % width;
            table[i] = column == right.Length ? 0 : Same(left[i / width], right[column])
                ? table[i + width + 1] + 1
                : Math.Max(table[i + 1], table[i + width]);
        }
    }

    private static List<(int Left, int Right)> ReadPairs(ReadOnlySpan<char> left, ReadOnlySpan<char> right, int[] table, int width)
    {
        var pairs = new List<(int Left, int Right)>();
        for (int i = 0, j = 0; i < left.Length && j < right.Length;)
        {
            if (Same(left[i], right[j])) pairs.Add((i++, j++));
            else if (table[(i + 1) * width + j] >= table[i * width + j + 1]) i++;
            else j++;
        }
        return pairs;
    }

    private static bool Same(char left, char right) => left == right && !char.IsSurrogate(left);

    private static ViewerDiffEdit[] Edits(string text, int prefix, int suffix, IEnumerable<int> matches)
    {
        var edits = new List<ViewerDiffEdit>();
        var start = prefix;
        foreach (var end in matches.Select(position => prefix + position).Append(text.Length - suffix))
        {
            if (end > start) edits.Add(new(start, end - start));
            start = end + 1;
        }
        return edits.ToArray();
    }
}

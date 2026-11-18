// @implements MarkEntries
namespace Xcmd.Core;

public static class PanelRangeMarks
{
    public static HashSet<string> Toggle(IReadOnlyList<string> range, IReadOnlyList<string> selected)
    {
        var marked = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (range.All(marked.Contains)) marked.ExceptWith(range);
        else marked.UnionWith(range);
        return marked;
    }
}

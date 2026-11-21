// @implements ChooseViewMode, ReadLargeFiles
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

internal readonly record struct StructuredJsonRow(int Element, bool Closing = false);

internal sealed class StructuredJsonRows(StructuredJsonSource source, StructuredJsonFolds folds)
{
    private StructuredJsonTree Tree => source.Tree;

    public bool Expanded(int id) => Tree[id].CanFold && folds.IsExpanded(id);

    public int Start(StructuredJsonRow row) => row.Closing ? Tree[row.Element].End - 1 : row.Element == 0 ? 0 : Tree[row.Element].Start;

    public int End(StructuredJsonRow row) => Next(row) is { } next ? Start(next) : source.Text.Length;

    public StructuredJsonRow? Next(StructuredJsonRow row)
    {
        var id = row.Element;
        if (!row.Closing && Expanded(id)) return new(Tree[id].Children[0]);
        var next = Tree.Sibling(id, 1);
        if (next != id) return new(next);
        return Tree[id].Parent >= 0 ? new(Tree[id].Parent, Closing: true) : null;
    }

    public StructuredJsonRow? Previous(StructuredJsonRow row)
    {
        var id = row.Element;
        if (row.Closing) return LastRow(Tree[id].Children[^1]);
        var previous = Tree.Sibling(id, -1);
        if (previous != id) return LastRow(previous);
        return Tree[id].Parent >= 0 ? new(Tree[id].Parent) : null;
    }

    private StructuredJsonRow LastRow(int id) => new(id, Expanded(id));

    public StructuredJsonRow At(int position)
    {
        var id = Tree.ElementAt(position);
        foreach (var ancestor in Tree.Ancestors(id))
            if (!Expanded(ancestor)) id = ancestor;

        if (!Expanded(id)) return new(id);
        if (position >= Tree[id].End - 1) return new(id, Closing: true);
        var children = Tree[id].Children;
        var low = 0;
        var high = children.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Tree[children[middle]].Start <= position) low = middle + 1;
            else high = middle;
        }
        return low == 0 ? new(id) : LastRow(children[low - 1]);
    }
}

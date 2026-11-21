// @implements ChooseViewMode, FindViewerText
using System.Collections.Immutable;

namespace Xcmd.Core;

public enum JsonPeerAction { Collapse, Expand, ApplyLayout }

public sealed record StructuredJsonFolds(ImmutableHashSet<int> Expanded, ImmutableHashSet<int> SearchExpanded)
{
    public static StructuredJsonFolds Initial { get; } = new([0], []);

    public bool IsExpanded(int id) => Expanded.Contains(id) || SearchExpanded.Contains(id);

    public StructuredJsonFolds Set(int id, bool expanded) => new(
        expanded ? Expanded.Add(id) : Expanded.Remove(id), SearchExpanded.Remove(id));

    public StructuredJsonFolds Reveal(StructuredJsonTree tree, int id) =>
        this with { Expanded = Expanded.Union(tree.Ancestors(id).Where(ancestor => !SearchExpanded.Contains(ancestor))) };

    public StructuredJsonFolds RevealSearch(IEnumerable<int> containers) =>
        this with { SearchExpanded = containers.ToImmutableHashSet().Except(Expanded) };

    public StructuredJsonFolds ClearSearch() => this with { SearchExpanded = SearchExpanded.Clear() };

    public JsonPeerResult Navigate(StructuredJsonTree tree, int id, int direction, CancellationToken token)
    {
        var group = tree.PeerGroup(id, includeObjectRecords: true);
        if (group == null) return new(this, id, 0, false);
        var skipped = 0;
        var count = tree[group.Container].Children.Count;
        for (var index = tree[group.Item].Index + direction; index >= 0 && index < count; index += direction)
        {
            token.ThrowIfCancellationRequested();
            var target = tree.ResolvePeer(group, index);
            if (target >= 0) return new(Reveal(tree, target), target, skipped, true);
            skipped++;
        }
        return new(this, id, skipped, true);
    }

    public JsonPeerResult Apply(StructuredJsonTree tree, int id, JsonPeerAction action, CancellationToken token)
    {
        var group = tree.PeerGroup(id);
        if (group == null || action == JsonPeerAction.ApplyLayout && !tree[id].IsContainer)
            return new(this, id, 0, false);

        var edits = new FoldEdits(Expanded.ToBuilder(), SearchExpanded.ToBuilder());
        var skipped = 0;
        var count = tree[group.Container].Children.Count;
        for (var index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            if (action == JsonPeerAction.ApplyLayout && index == tree[group.Item].Index) continue;
            var target = tree.ResolvePeer(group, index);
            if (target < 0 || !tree[target].IsContainer) { skipped++; continue; }
            if (action == JsonPeerAction.ApplyLayout)
            {
                if (!CopyLayout(tree, id, target, edits, token)) skipped++;
            }
            else if (tree[target].CanFold) edits.Set(target, action == JsonPeerAction.Expand);
        }
        return new(new(edits.Expanded.ToImmutable(), edits.SearchExpanded.ToImmutable()), id, skipped, true);
    }

    private bool CopyLayout(StructuredJsonTree tree, int source, int target, FoldEdits edits, CancellationToken token)
    {
        var pairs = new Stack<(int Source, int Target)>();
        pairs.Push((source, target));
        var complete = true;
        while (pairs.TryPop(out var pair))
        {
            token.ThrowIfCancellationRequested();
            if (pair.Target < 0 || tree[pair.Source].Kind != tree[pair.Target].Kind) { complete = false; continue; }
            if (tree[pair.Target].CanFold) edits.Set(pair.Target, tree[pair.Source].CanFold && IsExpanded(pair.Source));
            foreach (var child in tree[pair.Source].Children)
            {
                token.ThrowIfCancellationRequested();
                if (tree[child].IsContainer) pairs.Push((child, tree.MatchingChild(pair.Target, child)));
            }
        }
        return complete;
    }

    private sealed record FoldEdits(ImmutableHashSet<int>.Builder Expanded, ImmutableHashSet<int>.Builder SearchExpanded)
    {
        public void Set(int id, bool expanded)
        {
            if (expanded) Expanded.Add(id);
            else Expanded.Remove(id);
            SearchExpanded.Remove(id);
        }
    }
}

public sealed record JsonPeerResult(StructuredJsonFolds Folds, int Element, int Skipped, bool Available);

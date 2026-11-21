// @implements ChooseViewMode, StreamingReads
using System.Text;

namespace Xcmd.Core;

public enum JsonElementKind { Object, Array, Scalar }

public readonly record struct JsonMember(string Name, int Occurrence)
{
    public string PathPart => Name.Length > 0 && IsIdentifierStart(Name[0]) && Name.All(IsIdentifierPart)
        ? "." + Name + OccurrenceSuffix
        : "[\"" + Escape(Name) + "\"]" + OccurrenceSuffix;

    private string OccurrenceSuffix => Occurrence > 1 ? $"#{Occurrence}" : "";
    private static bool IsIdentifierStart(char value) => char.IsAsciiLetter(value) || value == '_';
    private static bool IsIdentifierPart(char value) => IsIdentifierStart(value) || char.IsAsciiDigit(value);

    private static string Escape(string name)
    {
        var result = new StringBuilder();
        foreach (var value in name)
            result.Append(value switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                _ when char.IsControl(value) || char.IsSurrogate(value) => $"\\u{(int)value:x4}",
                _ => value.ToString()
            });
        return result.ToString();
    }
}

public sealed record StructuredJsonElement(int Parent, int Index, int Depth, JsonMember? Member,
    JsonElementKind Kind, int Start, int ValueStart, int End, IReadOnlyList<int> Children)
{
    public bool IsContainer => Kind != JsonElementKind.Scalar;
    public bool CanFold => Children.Count > 0;
    public string PathPart => Member?.PathPart ?? $"[{Index}]";
}

public sealed class StructuredJsonTree
{
    private readonly IReadOnlyList<StructuredJsonElement> elements;
    private readonly Dictionary<int, Dictionary<JsonMember, int>> largeObjects = [];
    private readonly HashSet<int> objectPeerContainers = [];

    public StructuredJsonTree(IReadOnlyList<StructuredJsonElement> elements, CancellationToken token)
    {
        this.elements = elements;
        for (var id = 0; id < elements.Count; id++)
        {
            token.ThrowIfCancellationRequested();
            var element = elements[id];
            if (element.Kind != JsonElementKind.Object) continue;
            if (HasObjectPeers(element, token)) objectPeerContainers.Add(id);
            if (element.Children.Count < 16) continue;
            var members = new Dictionary<JsonMember, int>();
            foreach (var child in element.Children)
            {
                token.ThrowIfCancellationRequested();
                members.Add(elements[child].Member!.Value, child);
            }
            largeObjects.Add(id, members);
        }
    }

    public StructuredJsonElement this[int id] => elements[id];

    private bool HasObjectPeers(StructuredJsonElement element, CancellationToken token)
    {
        var records = 0;
        foreach (var child in element.Children)
        {
            token.ThrowIfCancellationRequested();
            if (elements[child].Kind == JsonElementKind.Object && ++records == 2) return true;
        }
        return false;
    }

    public IEnumerable<int> Ancestors(int id)
    {
        for (var parent = elements[id].Parent; parent >= 0; parent = elements[parent].Parent) yield return parent;
    }

    public int ElementAt(int sourcePosition)
    {
        var id = 0;
        while (TryFindChild(id, sourcePosition, out var child)) id = child;
        return id;
    }

    private bool TryFindChild(int id, int position, out int child)
    {
        var children = elements[id].Children;
        var low = 0;
        var high = children.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (elements[children[middle]].Start <= position) low = middle + 1;
            else high = middle;
        }
        child = low == 0 ? -1 : children[low - 1];
        return child >= 0 && position < elements[child].End;
    }

    public int Sibling(int id, int direction)
    {
        var element = elements[id];
        var index = element.Index + direction;
        return element.Parent >= 0 && index >= 0 && index < elements[element.Parent].Children.Count
            ? elements[element.Parent].Children[index] : id;
    }

    public string Path(int id, int wildcard = -1)
    {
        var parts = new Stack<string>();
        for (var current = id; current != 0; current = elements[current].Parent)
        {
            var element = elements[current];
            var wildcardPart = element.Member == null ? "[*]" : ".*";
            parts.Push(current == wildcard ? wildcardPart : element.PathPart);
        }
        return "$" + string.Concat(parts);
    }

    public JsonPeerGroup? PeerGroup(int id, bool includeObjectRecords = false)
    {
        var path = new Stack<int>();
        for (var item = id; elements[item].Parent >= 0; item = elements[item].Parent)
        {
            var parent = elements[item].Parent;
            if (elements[parent].Kind == JsonElementKind.Array ||
                includeObjectRecords && elements[item].Kind == JsonElementKind.Object && objectPeerContainers.Contains(parent))
                return new(parent, item, path.ToArray());
            path.Push(item);
        }
        return null;
    }

    public int ResolvePeer(JsonPeerGroup group, int index)
    {
        var container = elements[group.Container];
        var id = container.Children[index];
        if (container.Kind == JsonElementKind.Object && elements[id].Kind != JsonElementKind.Object) return -1;
        foreach (var step in group.RelativePath)
        {
            id = MatchingChild(id, step);
            if (id < 0) break;
        }
        return id;
    }

    internal int MatchingChild(int parent, int source)
    {
        var target = elements[parent];
        var step = elements[source];
        if (step.Member is not { } member)
            return target.Kind == JsonElementKind.Array && step.Index < target.Children.Count ? target.Children[step.Index] : -1;
        if (target.Kind != JsonElementKind.Object) return -1;
        if (largeObjects.TryGetValue(parent, out var members)) return members.GetValueOrDefault(member, -1);
        foreach (var child in target.Children)
            if (elements[child].Member == member) return child;
        return -1;
    }
}

public sealed record JsonPeerGroup(int Container, int Item, IReadOnlyList<int> RelativePath);

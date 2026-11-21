// @implements ChooseViewMode, StreamingReads
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class StructuredJsonParser(string source, bool allowComments, bool allowTrailingComma, CancellationToken token)
{
    private readonly StructuredJsonLexer lexer = new(source, allowComments, token);
    private readonly List<StructuredJsonElement> elements = [];
    private readonly List<StructuredJsonToken> tokens = [];
    private readonly Stack<Container> containers = new();

    public (StructuredJsonTree Tree, IReadOnlyList<StructuredJsonToken> Tokens) Parse()
    {
        ReadValue(Read(), null, null, 0);
        while (containers.TryPeek(out var container)) ReadChild(container);
        Expect(Read(), JsonLexemeKind.End, "Expected the end of the document");
        return (new(elements, token), tokens);
    }

    private void ReadChild(Container container)
    {
        var parent = elements[container.Id];
        var isObject = parent.Kind == JsonElementKind.Object;
        var closing = isObject ? JsonLexemeKind.ObjectEnd : JsonLexemeKind.ArrayEnd;
        var next = Read(property: isObject);
        if (next == closing) { Close(parent, container.Id); return; }
        if (container.Children.Count > 0)
        {
            Expect(next, JsonLexemeKind.Comma, "Expected a comma or a closing delimiter");
            next = Read(property: isObject);
            if (next == closing && allowTrailingComma) { Close(parent, container.Id); return; }
        }
        var start = lexer.Start;
        JsonMember? member = null;
        if (isObject)
        {
            Expect(next, JsonLexemeKind.String, "Expected an object member name");
            member = container.Member(lexer.MemberName());
            Expect(Read(), JsonLexemeKind.Colon, "Expected a colon after the member name");
            next = Read();
        }
        ReadValue(next, container, member, start);
    }

    private void ReadValue(JsonLexemeKind tokenKind, Container? parent, JsonMember? member, int start)
    {
        var kind = tokenKind switch
        {
            JsonLexemeKind.ObjectStart => JsonElementKind.Object,
            JsonLexemeKind.ArrayStart => JsonElementKind.Array,
            JsonLexemeKind.String or JsonLexemeKind.Number or JsonLexemeKind.Literal => JsonElementKind.Scalar,
            _ => throw lexer.Error("Expected a JSON value")
        };
        var id = elements.Count;
        var index = parent?.Children.Count ?? 0;
        parent?.Children.Add(id);
        var container = kind == JsonElementKind.Scalar ? null : new Container(id, kind);
        elements.Add(new(parent?.Id ?? -1, index, containers.Count, member, kind, start, lexer.Start, lexer.End,
            container?.Children ?? (IReadOnlyList<int>)Array.Empty<int>()));
        if (container != null) containers.Push(container);
    }

    private void Close(StructuredJsonElement element, int id)
    {
        elements[id] = element with { End = lexer.End };
        containers.Pop();
    }

    private JsonLexemeKind Read(bool property = false)
    {
        JsonLexemeKind kind;
        do
        {
            kind = lexer.Read();
            if (kind != JsonLexemeKind.End) tokens.Add(lexer.Token(kind, property));
        } while (kind == JsonLexemeKind.Comment);
        return kind;
    }

    private void Expect(JsonLexemeKind actual, JsonLexemeKind expected, string message)
    {
        if (actual != expected) throw lexer.Error(message);
    }

    private sealed class Container(int id, JsonElementKind kind)
    {
        private readonly Dictionary<string, int>? occurrences = kind == JsonElementKind.Object ? new(StringComparer.Ordinal) : null;
        public int Id => id;
        public List<int> Children { get; } = [];

        public JsonMember Member(string key)
        {
            var members = occurrences!;
            var occurrence = members.GetValueOrDefault(key) + 1;
            members[key] = occurrence;
            return new(key, occurrence);
        }
    }
}

using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class SyntaxScopes
{
    private static readonly (string Prefix, SyntaxKind Kind)[] Rules =
    [
        ("entity.name.section", SyntaxKind.Heading),
        ("comment", SyntaxKind.Comment),
        ("entity.name.tag", SyntaxKind.Tag),
        ("entity.other.attribute", SyntaxKind.Property),
        ("string", SyntaxKind.String),
        ("constant.numeric", SyntaxKind.Number),
        ("keyword.operator", SyntaxKind.Operator),
        ("keyword", SyntaxKind.Keyword),
        ("storage", SyntaxKind.Keyword),
        ("constant.language", SyntaxKind.Keyword),
        ("entity.name.function", SyntaxKind.Function),
        ("support.function", SyntaxKind.Function),
        ("entity.name", SyntaxKind.Type),
        ("support.type", SyntaxKind.Type),
        ("support.class", SyntaxKind.Type),
        ("markup.heading", SyntaxKind.Heading),
        ("markup.bold", SyntaxKind.Heading)
    ];

    public static SyntaxKind Classify(IEnumerable<string> scopes)
    {
        foreach (var scope in scopes.Reverse())
        {
            var kind = Classify(scope);
            if (kind != SyntaxKind.Plain) return kind;
        }
        return SyntaxKind.Plain;
    }

    private static SyntaxKind Classify(string scope)
    {
        if (scope.Contains("property-name", StringComparison.Ordinal)) return SyntaxKind.Property;
        foreach (var (prefix, kind) in Rules)
            if (scope.StartsWith(prefix, StringComparison.Ordinal)) return kind;
        return SyntaxKind.Plain;
    }
}

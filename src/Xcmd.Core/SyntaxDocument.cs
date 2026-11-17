// @implements SyntaxSpans
namespace Xcmd.Core;

public enum SyntaxKind { Plain, Comment, Keyword, String, Number, Type, Function, Property, Tag, Heading, Operator }

public sealed record SyntaxLanguage(string Id, string Name, string Scope);
public sealed record SyntaxSpan(int Start, int Length, SyntaxKind Kind, string Scope);

public sealed record SyntaxDocument(string Text, SyntaxLanguage? Language, IReadOnlyList<SyntaxSpan> Spans, string? Notice = null)
{
    public SyntaxDocument Select(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (start > Text.Length - length) throw new ArgumentOutOfRangeException(nameof(length));
        var end = start + length;
        var selected = Spans.Where(span => span.Start < end && span.Start + span.Length > start)
            .Select(span => new SyntaxSpan(Math.Max(start, span.Start) - start,
                Math.Min(end, span.Start + span.Length) - Math.Max(start, span.Start), span.Kind, span.Scope)).ToArray();
        return new(Text.Substring(start, length), Language, selected, Notice);
    }
}

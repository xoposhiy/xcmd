// @implements ChooseViewMode, StreamingReads
using System.Globalization;
using System.Text;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal enum JsonLexemeKind { End, ObjectStart, ObjectEnd, ArrayStart, ArrayEnd, Colon, Comma, String, Number, Literal, Comment }

public readonly record struct StructuredJsonToken(int Start, int End, SyntaxKind Syntax, bool EndsLine = false);

internal sealed class StructuredJsonLexer(string source, bool comments, CancellationToken token)
{
    private int index;
    private char Current => index < source.Length ? source[index] : '\0';
    public int Start { get; private set; }
    public int End => index;

    public bool CanStartDocument(bool hasMore)
    {
        try
        {
            return ReadContent() switch
            {
                JsonLexemeKind.ObjectStart => ReadContent() is JsonLexemeKind.String or JsonLexemeKind.ObjectEnd || hasMore && index == source.Length,
                JsonLexemeKind.ArrayStart => ReadContent() is JsonLexemeKind.ObjectStart or JsonLexemeKind.ArrayStart or JsonLexemeKind.ArrayEnd
                    or JsonLexemeKind.String or JsonLexemeKind.Number or JsonLexemeKind.Literal || hasMore && index == source.Length,
                JsonLexemeKind.String or JsonLexemeKind.Number or JsonLexemeKind.Literal => ReadContent() == JsonLexemeKind.End,
                JsonLexemeKind.End => hasMore,
                _ => false
            };
        }
        catch (FormatException) { return hasMore && MayContinueToken(); }
    }

    private JsonLexemeKind ReadContent()
    {
        var kind = Read();
        while (kind == JsonLexemeKind.Comment) kind = Read();
        return kind;
    }

    private bool MayContinueToken()
    {
        var remainder = source.AsSpan(index);
        return remainder.IsEmpty || "true".AsSpan().StartsWith(remainder, StringComparison.Ordinal) ||
            "false".AsSpan().StartsWith(remainder, StringComparison.Ordinal) || "null".AsSpan().StartsWith(remainder, StringComparison.Ordinal);
    }

    public JsonLexemeKind Read()
    {
        token.ThrowIfCancellationRequested();
        while (Current is ' ' or '\t' or '\r' or '\n') Advance();
        Start = index;
        return Current switch
        {
            '{' => Punctuation(JsonLexemeKind.ObjectStart),
            '}' => Punctuation(JsonLexemeKind.ObjectEnd),
            '[' => Punctuation(JsonLexemeKind.ArrayStart),
            ']' => Punctuation(JsonLexemeKind.ArrayEnd),
            ':' => Punctuation(JsonLexemeKind.Colon),
            ',' => Punctuation(JsonLexemeKind.Comma),
            '"' => ReadString(),
            't' => Literal("true"),
            'f' => Literal("false"),
            'n' => Literal("null"),
            '-' or >= '0' and <= '9' => ReadNumber(),
            '/' when comments => ReadComment(),
            _ when index == source.Length => JsonLexemeKind.End,
            _ => throw Error("Expected a JSON token")
        };
    }

    private void Advance()
    {
        index++;
        if ((index & 4095) == 0) token.ThrowIfCancellationRequested();
    }

    private JsonLexemeKind Punctuation(JsonLexemeKind kind) { Advance(); return kind; }

    private JsonLexemeKind Literal(string value)
    {
        if (!source.AsSpan(index).StartsWith(value, StringComparison.Ordinal)) throw Error("Invalid literal");
        index += value.Length;
        return JsonLexemeKind.Literal;
    }

    private JsonLexemeKind ReadString()
    {
        Advance();
        while (Current != '"')
        {
            if (Current < ' ') throw Error("Unterminated string or unescaped control character");
            if (Current == '\\') ReadEscape();
            Advance();
        }
        return Punctuation(JsonLexemeKind.String);
    }

    private void ReadEscape()
    {
        Advance();
        if (Current == 'u')
        {
            for (var digit = 0; digit < 4; digit++)
            {
                Advance();
                if (!char.IsAsciiHexDigit(Current)) throw Error("Invalid Unicode escape");
            }
        }
        else if (Current is not ('"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't'))
            throw Error("Invalid string escape");
    }

    public string MemberName()
    {
        var raw = source.AsSpan(Start + 1, End - Start - 2);
        if (!raw.Contains('\\')) return raw.ToString();
        var name = new StringBuilder();
        for (var position = 0; position < raw.Length; position++)
        {
            if ((position & 4095) == 0) token.ThrowIfCancellationRequested();
            var value = raw[position];
            if (value == '\\') value = DecodeEscape(raw, ref position);
            name.Append(value);
        }
        return name.ToString();
    }

    private static char DecodeEscape(ReadOnlySpan<char> raw, ref int position)
    {
        var escape = raw[++position];
        if (escape == 'u')
        {
            var value = (char)int.Parse(raw.Slice(position + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            position += 4;
            return value;
        }
        return escape switch { 'b' => '\b', 'f' => '\f', 'n' => '\n', 'r' => '\r', 't' => '\t', _ => escape };
    }

    private JsonLexemeKind ReadNumber()
    {
        if (Current == '-') Advance();
        if (Current == '0') Advance();
        else ReadDigits();
        if (Current == '.') { Advance(); ReadDigits(); }
        if (Current is 'e' or 'E')
        {
            Advance();
            if (Current is '+' or '-') Advance();
            ReadDigits();
        }
        return JsonLexemeKind.Number;
    }

    private void ReadDigits()
    {
        if (!char.IsAsciiDigit(Current)) throw Error("Expected a digit");
        do { Advance(); } while (char.IsAsciiDigit(Current));
    }

    private JsonLexemeKind ReadComment()
    {
        Advance();
        if (Current == '/')
        {
            do { Advance(); } while (index < source.Length && Current is not ('\r' or '\n'));
        }
        else if (Current == '*')
        {
            Advance();
            while (!source.AsSpan(index).StartsWith("*/", StringComparison.Ordinal))
            {
                if (index == source.Length) throw Error("Unterminated block comment");
                Advance();
            }
            index += 2;
        }
        else throw Error("Expected a JSONC comment");
        return JsonLexemeKind.Comment;
    }

    public FormatException Error(string message) => new($"{message} at character {index + 1:N0}.");

    public StructuredJsonToken Token(JsonLexemeKind kind, bool property) => new(Start, End, kind switch
    {
        JsonLexemeKind.String => property ? SyntaxKind.Property : SyntaxKind.String,
        JsonLexemeKind.Number => SyntaxKind.Number,
        JsonLexemeKind.Literal => SyntaxKind.Keyword,
        JsonLexemeKind.Comment => SyntaxKind.Comment,
        _ => SyntaxKind.Plain
    }, kind == JsonLexemeKind.Comment && source[Start + 1] == '/');
}

namespace Xcmd.Core;

public sealed record CommandToken(int Start, int End, string Prefix, bool Program)
{
    public static CommandToken At(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var start = 0; var quoted = false;
        for (var i = 0; i < caret; i++)
        {
            if (text[i] == '"') quoted = !quoted;
            else if (char.IsWhiteSpace(text[i]) && !quoted) start = i + 1;
        }
        var end = caret;
        while (end < text.Length && (quoted || !char.IsWhiteSpace(text[end])))
        { if (text[end] == '"') quoted = !quoted; end++; }
        return new(start, end, text[start..caret].Trim('"'), string.IsNullOrWhiteSpace(text[..start]));
    }

    public string Replace(string text, string value) => text[..Start] + CommandLine.FormatArgument(value) + text[End..];
}

public sealed record CommandSuggestion(string Value, bool Directory = false);

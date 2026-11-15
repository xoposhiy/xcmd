using System.Diagnostics;
using System.Text;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class SyntaxHighlighter
{
    public const int MaximumContextBytes = 1024 * 1024;
    public const int MaximumLineLength = 8192;
    public const string Version = "TextMateSharp 2.0.5 / Xcmd grammars 1";
    private static readonly Lazy<SyntaxGrammarCatalog> Catalog = new(() => new());
    private readonly object grammarLock = new();
    private Registry? registry;
    public static IReadOnlyList<SyntaxLanguage> Languages => Catalog.Value.Languages;

    public static SyntaxLanguage? Detect(string path, string text) => Catalog.Value.Detect(path, text);

    public async Task<SyntaxDocument> ReadAsync(IViewerFiles files, string path, FileSlice page, Encoding encoding,
        string? languageId, CancellationToken cancellationToken)
    {
        var text = ViewerText.Decode(page, encoding);
        if (languageId == "plain") return new(text, null, []);
        var first = page.Offset == 0 ? page : await files.ReadAsync(path, 0, cancellationToken);
        var language = languageId == null ? Detect(path, ViewerText.Decode(first, encoding)) : Catalog.Value.Find(languageId);
        if (language == null) return new(text, null, []);
        if (page.Offset + page.Count > MaximumContextBytes) return new(text, language, [], "Plain text: syntax context exceeds 1 MiB");
        var prefix = await ReadPrefixAsync(files, path, page, encoding, first, cancellationToken);
        return await Task.Run(() => Highlight(prefix + text, language, cancellationToken).Select(prefix.Length, text.Length), cancellationToken);
    }

    private static async Task<string> ReadPrefixAsync(IViewerFiles files, string path, FileSlice page, Encoding encoding,
        FileSlice first, CancellationToken cancellationToken)
    {
        if (page.Offset == 0) return "";
        var start = ViewerText.ByteOffset(page, encoding, 0);
        var bytes = new byte[(int)start];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var part = offset == 0 ? first : await files.ReadAsync(path, offset, cancellationToken);
            if (part.Length != page.Length || part.Offset != offset || part.Count == 0)
                throw new IOException("The file changed while reading syntax context. Reload the file.");
            var count = Math.Min(part.Count, bytes.Length - offset);
            part.Bytes.AsSpan(0, count).CopyTo(bytes.AsSpan(offset));
            offset += count;
        }
        return ViewerText.Decode(new(0, bytes.Length, bytes), encoding);
    }

    public SyntaxDocument Highlight(string text, SyntaxLanguage language, CancellationToken cancellationToken)
    {
        try
        {
            lock (grammarLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                registry ??= new Registry(Catalog.Value);
                return Tokenize(text, language, registry, cancellationToken);
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException and not OutOfMemoryException)
        { return new(text, language, [], "Plain text: syntax highlighting failed (" + failure.GetType().Name + ")"); }
    }

    private static SyntaxDocument Tokenize(string text, SyntaxLanguage language, Registry registry, CancellationToken cancellationToken)
    {
        var grammar = registry.LoadGrammar(language.Scope);
        if (grammar == null) return new(text, language, [], "Plain text: grammar is unavailable");
        // Compile the bundled grammar before applying the source-text time budget.
        grammar.TokenizeLine("", null, TimeSpan.MaxValue);
        cancellationToken.ThrowIfCancellationRequested();
        var timer = Stopwatch.StartNew();
        IStateStack? state = null;
        var spans = new List<SyntaxSpan>();
        var offset = 0;
        foreach (var line in text.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.Length > MaximumLineLength) return new(text, language, [], "Plain text: syntax line exceeds 8,192 characters");
            if (timer.ElapsedMilliseconds > 1500) return new(text, language, [], "Plain text: syntax time limit reached");
            var lineStart = timer.Elapsed;
            var result = grammar.TokenizeLine(line, state, TimeSpan.FromMilliseconds(250));
            if (timer.Elapsed - lineStart >= TimeSpan.FromMilliseconds(250)) return new(text, language, [], "Plain text: syntax line time limit reached");
            state = result.RuleStack;
            foreach (var token in result.Tokens)
            {
                var start = Math.Clamp(token.StartIndex, 0, line.Length);
                var end = Math.Clamp(token.EndIndex, start, line.Length);
                if (end > start) spans.Add(new(offset + start, end - start, SyntaxScopes.Classify(token.Scopes), string.Join(' ', token.Scopes)));
            }
            offset += line.Length + 1;
        }
        return new(text, language, spans.ToArray());
    }
}

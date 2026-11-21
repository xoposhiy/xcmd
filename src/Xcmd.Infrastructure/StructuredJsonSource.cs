// @implements ChooseViewMode, ReadLargeFiles, StreamingReads
using System.Text;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed record StructuredJsonSource(string Text, StructuredJsonTree Tree, IReadOnlyList<StructuredJsonToken> Tokens,
    StructuredJsonBytes Bytes, ViewerFileVersion? Version)
{
    public static async Task<StructuredJsonSource> ReadAsync(IViewerFiles files, string path, Encoding encoding, CancellationToken token)
    {
        var extension = Path.GetExtension(path);
        if (ViewerText.IsUnsupportedJsonExtension(extension)) throw new FormatException("Structured view supports JSON and JSONC.");
        var jsonc = extension.Equals(".jsonc", StringComparison.OrdinalIgnoreCase);
        var version = await files.VersionAsync(path, token).ConfigureAwait(false);
        await using var stream = await files.OpenTextAsync(path, token).ConfigureAwait(false);
        var strictEncoding = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var (source, bomLength) = await ReadTextAsync(stream, strictEncoding, token).ConfigureAwait(false);
        var (tree, tokens) = new StructuredJsonParser(source, allowComments: true, allowTrailingComma: jsonc, token).Parse();
        var map = new StructuredJsonBytes(source, strictEncoding, bomLength, token);
        if (version != await files.VersionAsync(path, token).ConfigureAwait(false) || stream.Length != map.Length)
            throw new IOException("The file changed while JSON was being indexed. Press 4 to try again.");
        return new(source, tree, tokens, map, version);
    }

    private static async Task<(string Text, int BomLength)> ReadTextAsync(Stream stream, Encoding encoding, CancellationToken token)
    {
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, FileSlice.PageSize, leaveOpen: true);
        var text = await reader.ReadToEndAsync(token).ConfigureAwait(false);
        var bomLength = encoding.Preamble.Length;
        if (text.StartsWith('\uFEFF') && bomLength == 0)
        {
            bomLength = encoding.GetByteCount(text.AsSpan(0, 1));
            text = text[1..];
        }
        else if (bomLength > 0)
        {
            stream.Position = 0;
            var prefix = new byte[bomLength];
            var count = await stream.ReadAtLeastAsync(prefix, prefix.Length, throwOnEndOfStream: false, token).ConfigureAwait(false);
            if (count != bomLength || !encoding.Preamble.SequenceEqual(prefix)) bomLength = 0;
        }
        return (text, bomLength);
    }
}

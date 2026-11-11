using System.Text;
using System.Text.RegularExpressions;
using UtfUnknown;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class SearchText
{
    public static async Task<SearchTextMatch?> FindAsync(string path, FileSearchQuery query, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var sample = new byte[Math.Min(stream.Length, 64 * 1024)];
        await stream.ReadExactlyAsync(sample, token).ConfigureAwait(false);
        var detected = ViewerText.Detect(sample).Encoding;
        if (ViewerText.IsBinary(sample, detected)) return null;
        var candidates = new List<int> { detected.CodePage };
        var confidence = CharsetDetector.DetectFromBytes(sample).Detected?.Confidence ?? 0;
        if (detected.CodePage is not (65001 or 1200 or 1201 or 12000 or 12001) && confidence < 0.8)
            AddFallbacks(candidates);
        var regex = query.Regex ? query.CreateTextRegex() : null;
        var decoded = false;
        for (var index = 0; index < candidates.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            stream.Position = 0;
            var encoding = Encoding.GetEncoding(candidates[index], EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            try
            {
                var match = await ScanAsync(stream, encoding, query, regex, token).ConfigureAwait(false);
                decoded = true;
                if (match != null) return match;
            }
            catch (DecoderFallbackException) { AddFallbacks(candidates); }
        }
        if (!decoded) throw new IOException("The text encoding could not be decoded.");
        return null;
    }

    private static void AddFallbacks(List<int> candidates)
    { foreach (var codePage in new[] { 65001, 1251, 1252, 866 }) if (!candidates.Contains(codePage)) candidates.Add(codePage); }

    private static async Task<SearchTextMatch?> ScanAsync(Stream stream, Encoding encoding, FileSearchQuery query, Regex? regex, CancellationToken token)
    {
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: true);
        if (regex == null) return await ScanLiteralAsync(reader, encoding.CodePage, query, token).ConfigureAwait(false);
        var chars = new char[32 * 1024];
        var line = new StringBuilder();
        long number = 1;
        while (true)
        {
            var count = await reader.ReadAsync(chars, token).ConfigureAwait(false);
            for (var offset = 0; offset < count;)
            {
                token.ThrowIfCancellationRequested();
                var end = Array.IndexOf(chars, '\n', offset, count - offset);
                var length = (end < 0 ? count : end) - offset;
                line.Append(chars, offset, length);
                if (line.Length > 256 * 1024) throw new IOException("This file contains a very long line. Use plain text search to include it.");
                offset += length + 1;
                if (end < 0) break;
                var match = MatchLine(line.ToString(), number++, encoding.CodePage, query, regex);
                if (match != null) return match;
                line.Clear();
            }
            if (count == 0) return MatchLine(line.ToString(), number, encoding.CodePage, query, regex);
        }
    }

    private static async Task<SearchTextMatch?> ScanLiteralAsync(StreamReader reader, int codePage, FileSearchQuery query, CancellationToken token)
    {
        var chars = new char[32 * 1024];
        var carry = "";
        long line = 1;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var count = await reader.ReadAsync(chars, token).ConfigureAwait(false);
            var text = carry + new string(chars, 0, count);
            var position = text.IndexOf(query.Text, query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            if (position >= 0)
            {
                var start = Math.Max(position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1, position - 70);
                var end = text.IndexOf('\n', position);
                var excerpt = text.Substring(start, Math.Min(240, (end < 0 ? text.Length : end) - start)).Trim('\r', '\uFEFF').Replace('\t', ' ');
                return new(line + CountNewlines(text.AsSpan(0, position)), excerpt, codePage, query.Text);
            }
            if (count == 0) return null;
            var keep = Math.Min(query.Text.Length - 1, text.Length);
            line += CountNewlines(text.AsSpan(0, text.Length - keep));
            carry = text[^keep..];
        }
    }

    private static int CountNewlines(ReadOnlySpan<char> text)
    { var count = 0; foreach (var character in text) if (character == '\n') count++; return count; }

    private static SearchTextMatch? MatchLine(string line, long number, int codePage, FileSearchQuery query, Regex? regex)
    {
        line = line.TrimEnd('\r');
        if (number == 1) line = line.TrimStart('\uFEFF');
        var position = regex == null ? line.IndexOf(query.Text, query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)
            : regex.Match(line) is { Success: true } match ? match.Index : -1;
        if (position < 0) return null;
        var start = Math.Max(0, position - 70);
        var excerpt = line.Substring(start, Math.Min(240, line.Length - start)).TrimStart('\uFEFF').Replace('\t', ' ');
        return new(number, excerpt, codePage, regex == null ? query.Text : regex.Match(line).Value);
    }
}

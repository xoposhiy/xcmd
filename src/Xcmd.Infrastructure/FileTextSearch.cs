// @implements StreamingReads
using System.Text;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class FileTextSearch
{
    public static async Task<FileTextMatch?> FindAsync(FileTextQuery query, CancellationToken token)
    {
        if (query.Text.Length is 0 or > 1024) throw new ArgumentException("Enter between 1 and 1024 characters to search.");
        var encoding = ViewerText.GetEncoding(query.CodePage);
        await using var stream = new FileStream(query.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var start = Math.Clamp(query.Start, 0, stream.Length);
        var match = await ScanAsync(stream, encoding, query, start, token).ConfigureAwait(false);
        if (match != null) return match;
        if ((!query.Backward && start == 0) || (query.Backward && start == stream.Length)) return null;
        stream.Position = 0;
        var wrapped = query with { Start = query.Backward ? stream.Length : 0 };
        match = await ScanAsync(stream, encoding, wrapped, wrapped.Start, token).ConfigureAwait(false);
        return match == null ? null : match with { Wrapped = true };
    }

    private static async Task<FileTextMatch?> ScanAsync(FileStream stream, Encoding encoding, FileTextQuery query, long start, CancellationToken token)
    {
        if (!query.Backward) stream.Position = start;
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: true);
        var chars = new char[16 * 1024];
        var carry = "";
        long offset = stream.Position;
        FileTextMatch? previous = null;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var count = await reader.ReadAsync(chars, token).ConfigureAwait(false);
            var text = carry + new string(chars, 0, count);
            var position = 0;
            while (position <= text.Length - query.Text.Length)
            {
                var found = text.IndexOf(query.Text, position, StringComparison.OrdinalIgnoreCase);
                if (found < 0) break;
                var byteOffset = offset + encoding.GetByteCount(text.AsSpan(0, found));
                var match = new FileTextMatch(byteOffset, encoding.GetByteCount(text.AsSpan(found, query.Text.Length)));
                if (!query.Backward) return match;
                if (byteOffset >= start) return previous;
                previous = match;
                position = found + 1;
            }
            if (count == 0) return previous;
            var keep = CarryLength(text, query.Text.Length);
            var consumed = text.Length - keep;
            offset += encoding.GetByteCount(text.AsSpan(0, consumed));
            carry = text[^keep..];
            if (query.Backward && offset >= start) return previous;
        }
    }

    private static int CarryLength(string text, int queryLength)
    {
        var keep = Math.Min(queryLength - 1, text.Length);
        if (keep > 0 && char.IsLowSurrogate(text[text.Length - keep]) && text.Length > keep) keep++;
        var consumed = text.Length - keep;
        if (consumed > 0 && char.IsHighSurrogate(text[consumed - 1])) keep++;
        return keep;
    }
}

// @implements ReadWorkingFileDiff
using System.Text;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed record DiffText(string Text, long[] LineOffsets)
{
    public static DiffText Decode(byte[] bytes, int codePage, bool mapOffsets = false)
    {
        if (bytes.Length == 0) return new("", [0]);
        var sample = bytes[..Math.Min(bytes.Length, FileSlice.PageSize)];
        var encoding = codePage == 0 ? ViewerText.Detect(sample).Encoding : ViewerText.GetEncoding(codePage);
        if (ViewerText.IsBinary(sample, encoding)) throw new IOException("A Git version of this file is binary.");
        var text = encoding.GetString(bytes);
        var bom = text.StartsWith('\uFEFF') ? 1 : 0;
        var offsets = mapOffsets ? ReadLineOffsets(text, encoding, bom, bytes.LongLength) : [];
        var display = ViewerText.Decode(new(0, bytes.LongLength, bytes), encoding);
        return new(display.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'), offsets);
    }

    private static long[] ReadLineOffsets(string text, Encoding encoding, int start, long length)
    {
        var offset = (long)encoding.GetByteCount(text.AsSpan(0, start));
        var offsets = new List<long> { offset };
        while (start < text.Length)
        {
            var end = text.AsSpan(start).IndexOfAny('\r', '\n');
            if (end < 0) break;
            end += start + 1;
            if (text[end - 1] == '\r' && end < text.Length && text[end] == '\n') end++;
            offset += encoding.GetByteCount(text.AsSpan(start, end - start));
            offsets.Add(Math.Min(offset, length));
            start = end;
        }
        if (offsets[^1] != length) offsets.Add(length);
        return offsets.ToArray();
    }
}

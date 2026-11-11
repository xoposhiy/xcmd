using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using UtfUnknown;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed record ViewerEncoding(Encoding Encoding, string Description);

public static class ViewerText
{
    static ViewerText() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static ViewerEncoding Detect(byte[] bytes)
    {
        var detected = CharsetDetector.DetectFromBytes(bytes).Detected;
        var encoding = detected?.Encoding ?? Encoding.UTF8;
        if (encoding.CodePage is not (1200 or 1201 or 12000 or 12001) && IsUtf8(bytes))
            return new(Encoding.UTF8, "UTF-8 (auto, validated)");
        if (encoding.CodePage == 20127) encoding = Encoding.UTF8;
        var description = detected == null ? "UTF-8 (fallback; choose encoding if needed)"
            : $"{encoding.WebName.ToUpperInvariant()} (auto, {detected.Confidence:P0})";
        return new(encoding, description);
    }

    private static bool IsUtf8(ReadOnlySpan<byte> bytes)
    {
        while (!bytes.IsEmpty)
        {
            var status = Rune.DecodeFromUtf8(bytes, out _, out var consumed);
            if (status == OperationStatus.InvalidData) return false;
            if (status == OperationStatus.NeedMoreData) return true;
            bytes = bytes[consumed..];
        }
        return true;
    }

    public static Encoding GetEncoding(int codePage) => Encoding.GetEncoding(codePage);

    public static long ByteOffset(FileSlice slice, Encoding encoding, int character)
    {
        var start = 0;
        if (slice.Offset > 0 && encoding.CodePage == 65001)
            while (start < slice.Count && (slice.Bytes[start] & 0xc0) == 0x80) start++;
        if (slice.Offset > 0 && encoding.CodePage is 1200 or 1201 && slice.Count >= 2 && char.IsLowSurrogate(ReadChar(slice.Bytes, 0, encoding.CodePage))) start = 2;
        var raw = encoding.GetString(slice.Bytes, start, slice.Bytes.Length - start);
        var bom = slice.Offset == 0 && raw.StartsWith('\uFEFF') ? 1 : 0;
        return Math.Min(slice.Length, slice.Offset + start + encoding.GetByteCount(raw.AsSpan(0, Math.Clamp(character + bom, 0, raw.Length))));
    }

    public static bool IsBinary(byte[] bytes, Encoding encoding)
    {
        var text = encoding.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 4096)));
        var controls = text.Count(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t' and not '\f');
        return controls > 0 && controls >= text.Length / 100;
    }

    public static string Decode(FileSlice slice, Encoding encoding)
    {
        var start = 0;
        var end = slice.Count;
        var bytes = slice.Bytes;
        if (encoding.CodePage == 65001)
        {
            if (slice.Offset > 0) while (start < end && (bytes[start] & 0xc0) == 0x80) start++;
            while (end < bytes.Length && (bytes[end] & 0xc0) == 0x80) end++;
        }
        else if (encoding.CodePage is 1200 or 1201)
        {
            if (slice.Offset > 0 && end >= 2 && char.IsLowSurrogate(ReadChar(bytes, 0, encoding.CodePage))) start = 2;
            if (end >= 2 && end + 2 <= bytes.Length && char.IsHighSurrogate(ReadChar(bytes, end - 2, encoding.CodePage))) end += 2;
        }
        var text = encoding.GetString(bytes, start, end - start);
        if (slice.Offset == 0 && text.StartsWith('\uFEFF')) text = text[1..];
        return string.Concat(text.Select(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t' ? '·' : c));
    }

    private static char ReadChar(byte[] bytes, int offset, int codePage) => (char)(codePage == 1200
        ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2)) : BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2)));

    public static string FormatBytes(FileSlice slice, bool hex)
    {
        var result = new StringBuilder();
        var width = hex ? 16 : 64;
        for (var offset = 0; offset < slice.Count; offset += width)
        {
            result.Append((slice.Offset + offset).ToString("X8")).Append("  ");
            var count = Math.Min(width, slice.Count - offset);
            if (hex)
            {
                for (var i = 0; i < width; i++) result.Append(i < count ? slice.Bytes[offset + i].ToString("X2") : "  ").Append(i == 7 ? "  " : " ");
                result.Append(" │ ");
            }
            for (var i = 0; i < count; i++)
            {
                var value = slice.Bytes[offset + i];
                result.Append(value is >= 32 and <= 126 ? (char)value : '·');
            }
            result.AppendLine();
        }
        return result.ToString();
    }

    public static bool IsImage(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71 })
        || bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 })
        || bytes.AsSpan().StartsWith("GIF8"u8) || bytes.AsSpan().StartsWith("BM"u8)
        || bytes.AsSpan().StartsWith(new byte[] { 0, 0, 1, 0 })
        || (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8));
}

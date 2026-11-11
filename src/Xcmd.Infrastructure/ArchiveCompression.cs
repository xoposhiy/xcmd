using SharpCompress.Common;
using SharpCompress.Providers;

namespace Xcmd.Infrastructure;

internal static class ArchiveCompression
{
    public static CompressionType? Detect(Stream input)
    {
        input.Position = 0;
        Span<byte> header = stackalloc byte[6];
        var length = input.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        input.Position = 0;
        var bytes = header[..length];
        if (bytes.StartsWith("BZh"u8)) return CompressionType.BZip2;
        if (bytes.StartsWith(new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0 })) return CompressionType.Xz;
        if (bytes.StartsWith("LZIP"u8)) return CompressionType.LZip;
        if (bytes.StartsWith(new byte[] { 0x28, 0xB5, 0x2F, 0xFD })) return CompressionType.ZStandard;
        if (bytes.StartsWith(new byte[] { 0x1F, 0x9D })) return CompressionType.Lzw;
        return null;
    }

    public static Stream Open(Stream input, CompressionType type) => CompressionProviderRegistry.Default.CreateDecompressStream(type, input);

    public static long Measure(Stream input, CompressionType type, CancellationToken token)
    {
        input.Position = 0;
        using var data = Open(input, type);
        var buffer = new byte[131072];
        long size = 0;
        int count;
        while ((count = data.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); size = checked(size + count); }
        return size;
    }
}

// @implements ImageDecoding
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Xcmd.Desktop;

internal sealed record ViewerImage(Bitmap Bitmap, string Description)
{
    public static ViewerImage Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(stream) ?? throw new IOException("This image format is not supported. Try Hex or Open externally.");
        var size = codec.Info;
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * size.Height > 40_000_000)
            throw new IOException("Image preview is limited to 40 million pixels. Use Open externally for this image.");
        using var source = new MemoryStream(bytes, writable: false);
        var bitmap = Bitmap.DecodeToWidth(source, Math.Min(size.Width, 4096));
        return new(bitmap, $"{size.Width:N0} × {size.Height:N0} px · {codec.EncodedFormat} · Fit to window · First frame");
    }
}

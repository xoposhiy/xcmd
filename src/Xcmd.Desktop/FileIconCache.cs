using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed class FileIconCache : IDisposable
{
    private readonly Dictionary<string, Task<WriteableBitmap?>> icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Task> bindings = [];
    private readonly List<WriteableBitmap> loaded = [];
    private bool disposed;
    public Task Completion => Task.WhenAll(bindings.ToArray());
    public string? Error { get; private set; }

    public void Bind(Image image, FileRow row)
    {
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
        var scale = TopLevel.GetTopLevel(image)?.RenderScaling ?? 1;
        image.LayoutUpdated += (_, _) =>
        {
            var current = TopLevel.GetTopLevel(image)?.RenderScaling ?? 1;
            if (scale != current) { scale = current; TrackBinding(image, row); }
            SizeImage(image);
        };
        TrackBinding(image, row);
    }

    private void TrackBinding(Image image, FileRow row)
    {
        var task = BindAsync(image, row);
        bindings.Add(task);
        _ = RemoveCompletedAsync(task);
    }

    private async Task RemoveCompletedAsync(Task task) { await task; bindings.Remove(task); }

    private async Task BindAsync(Image image, FileRow row)
    {
        if (disposed || !OperatingSystem.IsWindows()) return;
        var entry = row.Entry;
        var extension = Path.GetExtension(entry.FullPath);
        var minimumSize = (int)Math.Ceiling(16 * (TopLevel.GetTopLevel(image)?.RenderScaling ?? 1));
        var identity = entry.IsDirectory || extension.ToLowerInvariant() is ".exe" or ".lnk" or ".ico" ? entry.FullPath : extension;
        var key = identity + "|" + minimumSize;
        if (!icons.TryGetValue(key, out var task))
        {
            if (icons.Count >= 512) return;
            task = LoadAsync(entry, minimumSize); icons[key] = task;
        }
        var bitmap = await task;
        if (!disposed && image.DataContext == row && minimumSize == (int)Math.Ceiling(16 * (TopLevel.GetTopLevel(image)?.RenderScaling ?? 1)))
        { image.Source = bitmap; SizeImage(image); }
    }

    private static void SizeImage(Image image)
    {
        if (image.Source is not WriteableBitmap bitmap) return;
        var scale = TopLevel.GetTopLevel(image)?.RenderScaling ?? 1;
        image.Width = bitmap.PixelSize.Width / scale;
        image.Height = bitmap.PixelSize.Height / scale;
    }

    private async Task<WriteableBitmap?> LoadAsync(FileEntry entry, int minimumSize)
    {
        try
        {
            var pixels = await Task.Run(() => SystemIcons.Read(entry.FullPath, entry.IsDirectory, minimumSize));
            if (disposed) return null;
            var bitmap = new WriteableBitmap(new PixelSize(pixels.Size, pixels.Size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using var buffer = bitmap.Lock();
            for (var row = 0; row < pixels.Size; row++) Marshal.Copy(pixels.Bytes, row * pixels.Size * 4, buffer.Address + row * buffer.RowBytes, pixels.Size * 4);
            loaded.Add(bitmap);
            return bitmap;
        }
        catch (Exception error) { Error = error.Message; return null; }
    }

    public void Dispose()
    {
        disposed = true;
        foreach (var bitmap in loaded) bitmap.Dispose();
        loaded.Clear();
    }
}

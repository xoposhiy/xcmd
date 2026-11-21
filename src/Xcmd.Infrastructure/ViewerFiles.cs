// @implements StreamingReads
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class ViewerFiles : IViewerFiles
{
    public const int MaximumImageBytes = 32 * 1024 * 1024;
    public Task<FileTextMatch?> FindAsync(FileTextQuery query, CancellationToken cancellationToken) => FileTextSearch.FindAsync(query, cancellationToken);

    public Task<Stream> OpenTextAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, FileSlice.PageSize, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    public Task<ViewerFileVersion?> VersionAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = new FileInfo(path);
        return Task.FromResult<ViewerFileVersion?>(new(file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc));
    }
    public async Task<FileSlice> ReadAsync(string path, long offset, CancellationToken cancellationToken)
    {
        await using var stream = Open(path);
        var length = stream.Length;
        offset = Math.Clamp(offset, 0, Math.Max(0, (length - 1) / FileSlice.PageSize * FileSlice.PageSize));
        stream.Position = offset;
        var bytes = new byte[(int)Math.Min(FileSlice.PageSize + 4, length - offset)];
        var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
        if (count != bytes.Length) Array.Resize(ref bytes, count);
        return new(offset, length, bytes);
    }

    public async Task<byte[]> ReadImageAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = Open(path);
        if (stream.Length > MaximumImageBytes) throw new IOException("Image preview is limited to 32 MiB. Use Open externally for this image.");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }

    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete, FileSlice.PageSize, FileOptions.Asynchronous | FileOptions.RandomAccess);
}

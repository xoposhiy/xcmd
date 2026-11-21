// @implements ArchiveAccess
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class ArchiveViewerFiles(IArchiveContents archives) : IViewerFiles
{
    private readonly ViewerFiles files = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> reports = new(StringComparer.Ordinal);
    public bool IsArchiveReport(string path) => reports.GetValueOrDefault(path);

    public async Task<FileSlice> ReadAsync(string path, long offset, CancellationToken cancellationToken)
    {
        var source = await archives.ViewerSourceAsync(path, cancellationToken);
        reports[path] = source.IsArchiveReport;
        return await files.ReadAsync(source.Path, offset, cancellationToken);
    }

    public async Task<byte[]> ReadImageAsync(string path, CancellationToken cancellationToken) =>
        await files.ReadImageAsync(await archives.MaterializeAsync(path, cancellationToken), cancellationToken);

    public async Task<FileTextMatch?> FindAsync(FileTextQuery query, CancellationToken cancellationToken) =>
        await files.FindAsync(query with { Path = (await archives.ViewerSourceAsync(query.Path, cancellationToken)).Path }, cancellationToken);

    public async Task<Stream> OpenTextAsync(string path, CancellationToken cancellationToken) =>
        await files.OpenTextAsync((await archives.ViewerSourceAsync(path, cancellationToken)).Path, cancellationToken);

    public async Task<ViewerFileVersion?> VersionAsync(string path, CancellationToken cancellationToken) =>
        await files.VersionAsync((await archives.ViewerSourceAsync(path, cancellationToken)).Path, cancellationToken);
}

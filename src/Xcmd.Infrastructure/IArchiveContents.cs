// @implements ArchiveAccess
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public interface IArchiveContents : IDisposable
{
    Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken token);
    Task<string> MaterializeAsync(string path, CancellationToken token);
    Task<ViewerSource> ViewerSourceAsync(string path, CancellationToken token);
    Task<long> ExtractAsync(string source, string destination, bool replace, IProgress<long> progress, CancellationToken token);
}

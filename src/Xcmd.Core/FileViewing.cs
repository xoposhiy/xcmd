// @implements StreamingReads, RepositoryDocuments
namespace Xcmd.Core;

public enum FileViewMode { Text, Binary, Hex, Image }

public sealed record FileTextQuery(string Path, string Text, int CodePage, long Start, bool Backward);
public sealed record FileTextMatch(long Offset, int Length, bool Wrapped = false);
public sealed record ViewerSource(string Path, bool IsArchiveReport = false);
public sealed record ViewerDocument(string Path, string Caption, bool IsReadOnly = true);

public sealed record FileSlice(long Offset, long Length, byte[] Bytes)
{
    public const int PageSize = 16 * 1024;
    public int Count => Offset + Bytes.Length == Length ? Bytes.Length : Math.Min(PageSize, Bytes.Length);
    public bool HasNext => Offset + Count < Length;
}

public interface IViewerFiles
{
    bool IsArchiveReport(string path) => false;
    Task<FileSlice> ReadAsync(string path, long offset, CancellationToken cancellationToken);
    Task<byte[]> ReadImageAsync(string path, CancellationToken cancellationToken);
    Task<FileTextMatch?> FindAsync(FileTextQuery query, CancellationToken cancellationToken);
}

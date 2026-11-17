// @implements MeasureFolders
namespace Xcmd.Core;

public sealed record DirectorySize(long Bytes, long Files, int SkippedLinks, int Errors)
{
    public bool Incomplete => Errors > 0;
}
public sealed record DirectoryMeasurement(string Name, DirectorySize Size, DateTimeOffset MeasuredAt);

public sealed class DirectorySizeCalculator(IFileSystem fileSystem)
{
    public async Task<DirectorySize> CalculateAsync(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>(); pending.Push(root);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long bytes = 0, files = 0; int links = 0, errors = 0;
        while (pending.TryPop(out var path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(path)) continue;
            IReadOnlyList<FileEntry> entries;
            try { entries = await fileSystem.ReadDirectoryAsync(path, cancellationToken); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { errors++; continue; }
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.IsParent) continue;
                if (entry.IsLink) { links++; continue; }
                if (entry.IsDirectory) pending.Push(entry.FullPath);
                else { bytes += entry.Size; files++; }
            }
        }
        return new(bytes, files, links, errors);
    }
}

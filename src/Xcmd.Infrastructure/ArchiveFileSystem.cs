using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class ArchiveFileSystem(IFileSystem fileSystem, IArchiveContents archives) : IFileSystem, IFileOperationStore
{
    private readonly IFileOperationStore operations = fileSystem as IFileOperationStore ?? new LocalFileOperations(fileSystem);

    public Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken cancellationToken) =>
        ArchivePath.IsEntry(path) ? archives.ReadDirectoryAsync(path, cancellationToken) : fileSystem.ReadDirectoryAsync(path, cancellationToken);

    public async Task<FileEntry?> InspectAsync(string path, CancellationToken cancellationToken)
    {
        if (!ArchivePath.IsEntry(path)) return await operations.InspectAsync(path, cancellationToken);
        if (ArchivePath.EntryName(path).Length == 0)
        {
            await ReadDirectoryAsync(path, cancellationToken);
            return new(path, Path.GetFileName(ArchivePath.Source(path)), true, 0, DateTime.MinValue);
        }
        var entries = await ReadDirectoryAsync(ArchivePath.Parent(path)!, cancellationToken);
        return entries.FirstOrDefault(entry => entry.FullPath == path);
    }

    public Task RenameAsync(FileEntry entry, string newName)
    { ArchivePath.RequireWritable(entry.FullPath); return fileSystem.RenameAsync(entry, newName); }

    public Task CreateDirectoryAsync(string parent, string name)
    { ArchivePath.RequireWritable(parent); return fileSystem.CreateDirectoryAsync(parent, name); }

    public Task CopyFileAsync(FileEntry entry, string destination) => CopyAsync(entry.FullPath, Path.Combine(destination, entry.Name), false, new Progress<long>(), CancellationToken.None);

    public Task<bool> AreIdenticalAsync(FileEntry source, FileEntry destination, CancellationToken cancellationToken) =>
        ArchivePath.IsEntry(source.FullPath) ? Task.FromResult(false) : operations.AreIdenticalAsync(source, destination, cancellationToken);

    public Task CreateFolderAsync(string path, CancellationToken cancellationToken)
    { ArchivePath.RequireWritable(path); return operations.CreateFolderAsync(path, cancellationToken); }

    public Task<long> CopyAsync(string source, string destination, bool replace, IProgress<long> progress, CancellationToken cancellationToken)
    {
        ArchivePath.RequireWritable(destination);
        return ArchivePath.IsEntry(source) ? archives.ExtractAsync(source, destination, replace, progress, cancellationToken)
            : operations.CopyAsync(source, destination, replace, progress, cancellationToken);
    }

    public Task<bool> TryMoveAsync(FileEntry source, string destination, bool replace, CancellationToken cancellationToken)
    { ArchivePath.RequireWritable(source.FullPath); ArchivePath.RequireWritable(destination); return operations.TryMoveAsync(source, destination, replace, cancellationToken); }

    public Task RemoveAsync(FileEntry entry, bool recycle, CancellationToken cancellationToken)
    { ArchivePath.RequireWritable(entry.FullPath); return operations.RemoveAsync(entry, recycle, cancellationToken); }
}

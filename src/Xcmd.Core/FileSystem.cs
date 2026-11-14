namespace Xcmd.Core;

public interface IFileSystem
{
    Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken cancellationToken);
    Task RenameAsync(FileEntry entry, string newName);
    Task CreateDirectoryAsync(string parent, string name);
}

using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class LocalFileSystem : IFileSystem
{
    public Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken cancellationToken) => Task.Run<IReadOnlyList<FileEntry>>(() =>
    {
        var entries = new List<FileEntry>();
        foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new FileEntry(info.FullName, info.Name, info is DirectoryInfo,
                info is FileInfo file ? file.Length : 0, info.LastWriteTimeUtc, IsLink: info.Attributes.HasFlag(FileAttributes.ReparsePoint)));
        }
        return entries;
    }, cancellationToken);

    public Task RenameAsync(FileEntry entry, string newName) => Task.Run(() =>
    {
        if (entry.IsParent) throw new IOException("The parent navigation item cannot be renamed.");
        var target = Path.Combine(Path.GetDirectoryName(entry.FullPath)!, newName);
        if (entry.FullPath.Equals(target, StringComparison.Ordinal)) return;
        if (entry.IsDirectory) Directory.Move(entry.FullPath, target);
        else File.Move(entry.FullPath, target, overwrite: false);
    });

    public Task CreateDirectoryAsync(string parent, string name) => Task.Run(() =>
    {
        var target = Path.Combine(parent, name);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("The destination already exists.");
        Directory.CreateDirectory(target);
    });
}

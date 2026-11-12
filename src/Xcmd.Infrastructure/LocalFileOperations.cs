using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class LocalFileOperations(IFileSystem fileSystem) : IFileOperationStore
{
    public Task<FileEntry?> InspectAsync(string path, CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            var attributes = File.GetAttributes(path);
            var directory = attributes.HasFlag(FileAttributes.Directory);
            return new FileEntry(path, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), directory,
                directory ? 0 : new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), IsLink: attributes.HasFlag(FileAttributes.ReparsePoint));
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }, cancellationToken);

    public Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken cancellationToken) => fileSystem.ReadDirectoryAsync(path, cancellationToken);

    public Task<bool> AreIdenticalAsync(FileEntry source, FileEntry destination, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        await using var left = new FileStream(source.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var right = new FileStream(destination.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        const FileAttributes metadata = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive;
        if (left.Length != right.Length || File.GetLastWriteTimeUtc(source.FullPath) != File.GetLastWriteTimeUtc(destination.FullPath) ||
            (File.GetAttributes(source.FullPath) & metadata) != (File.GetAttributes(destination.FullPath) & metadata)) return false;
        if (FileCopyMetadata.Streams(source.FullPath).Length > 0 || FileCopyMetadata.Streams(destination.FullPath).Length > 0) return false;
        var first = new byte[131072]; var second = new byte[131072];
        var remaining = left.Length;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = (int)Math.Min(first.Length, remaining);
            await left.ReadExactlyAsync(first.AsMemory(0, length), cancellationToken);
            await right.ReadExactlyAsync(second.AsMemory(0, length), cancellationToken);
            if (!first.AsSpan(0, length).SequenceEqual(second.AsSpan(0, length))) return false;
            remaining -= length;
        }
        return true;
    }, cancellationToken);

    public Task CreateFolderAsync(string path, CancellationToken cancellationToken) => Task.Run(() =>
    {
        Directory.CreateDirectory(path);
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("The destination became a link. The operation stopped.");
    }, cancellationToken);

    public Task<long> CopyAsync(string source, string destination, bool replace, IProgress<long> progress, CancellationToken cancellationToken) =>
        Task.Run(() => CopyCoreAsync(source, destination, replace, progress, cancellationToken), cancellationToken);

    public Task CopyMetadataAsync(string source, string destination, CancellationToken cancellationToken) =>
        Task.Run(() => FileCopyMetadata.Apply(source, destination), cancellationToken);

    private static Task<long> CopyCoreAsync(string source, string destination, bool replace, IProgress<long> progress, CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".xcmd-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var copied = FileCopyMetadata.Copy(source, temporary, progress, cancellationToken);
            FileCopyMetadata.Apply(source, temporary);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destination) && File.GetAttributes(destination).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("The destination became a link. The operation stopped.");
            File.Move(temporary, destination, replace);
            return Task.FromResult(copied);
        }
        finally
        {
            if (File.Exists(temporary))
            { File.SetAttributes(temporary, FileAttributes.Normal); File.Delete(temporary); }
        }
    }

    public Task<bool> TryMoveAsync(FileEntry source, string destination, bool replace, CancellationToken cancellationToken) => Task.Run(() =>
    {
        if (!string.Equals(Path.GetPathRoot(source.FullPath), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            if (source.IsDirectory) Directory.Move(source.FullPath, destination);
            else File.Move(source.FullPath, destination, replace);
            return true;
        }
        catch (IOException error) when ((error.HResult & 0xFFFF) == 17) { return false; }
    }, cancellationToken);

    public Task RemoveAsync(FileEntry entry, bool recycle, CancellationToken cancellationToken)
    {
        if (recycle) return WindowsRecycleBin.SendAsync(entry.FullPath, cancellationToken);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory) Directory.Delete(entry.FullPath, recursive: false);
            else File.Delete(entry.FullPath);
        }, cancellationToken);
    }
}

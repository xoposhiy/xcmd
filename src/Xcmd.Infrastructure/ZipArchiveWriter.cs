using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class ZipArchiveWriter
{
    public static Task<long> CreateAsync(FileEntry[] sources, string destination, IProgress<long> progress, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        ArchivePath.RequireWritable(destination);
        if (sources.Length == 0) throw new ArgumentException("Select files to add to the ZIP archive.");
        RequirePhysicalParents(Path.GetDirectoryName(destination)!);
        foreach (var source in sources)
        {
            ArchivePath.RequireWritable(source.FullPath);
            FileOperationRules.ValidateTarget(source, destination);
            RequirePhysicalParents(Path.GetDirectoryName(source.FullPath)!);
        }
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("An item with this name already exists. Choose another ZIP name.");
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".xcmd-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            long bytes;
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous))
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
                bytes = await WriteAsync(archive, sources, progress, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
            return bytes;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }, cancellationToken);

    private static void RequirePhysicalParents(string directory)
    {
        for (string? current = directory; current != null; current = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current)))
            if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Creating a ZIP through links or junctions is unavailable: " + current);
    }

    private static async Task<long> WriteAsync(ZipArchive archive, FileEntry[] sources, IProgress<long> progress, CancellationToken token)
    {
        var pending = new Stack<(string Path, string Name)>(sources.Reverse().Select(source => (source.FullPath, source.Name)));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new byte[131072];
        long transferred = 0, reported = 0;
        while (pending.TryPop(out var item))
        {
            token.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(item.Path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Links and junctions cannot be added to ZIP archives: " + item.Path);
            var directory = attributes.HasFlag(FileAttributes.Directory);
            var name = item.Name.Replace('\\', '/') + (directory ? "/" : "");
            if (!names.Add(name)) throw new IOException("Several selected items have the same ZIP entry name: " + name);
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            var modified = File.GetLastWriteTimeUtc(item.Path);
            entry.LastWriteTime = new DateTimeOffset(modified.Year is >= 1980 and <= 2107 ? modified : new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            if (directory)
            {
                foreach (var child in Directory.EnumerateFileSystemEntries(item.Path)) pending.Push((child, item.Name + "/" + Path.GetFileName(child)));
                continue;
            }
            await using var input = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = entry.Open();
            int count;
            while ((count = await input.ReadAsync(buffer, token)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, count), token);
                transferred += count;
                if (reported == 0 || Stopwatch.GetElapsedTime(reported).TotalMilliseconds >= 80)
                { progress.Report(transferred); reported = Stopwatch.GetTimestamp(); }
            }
        }
        progress.Report(transferred);
        return transferred;
    }
}

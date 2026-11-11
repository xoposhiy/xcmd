using System.Text;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed record ArchiveItem(string Name, bool Directory, long Size, long PackedSize, DateTime Modified, bool Encrypted, bool Link, string Compression);
internal sealed record ArchiveCatalog(string File, long Length, DateTime Modified, string Format, bool Solid, bool Complete, ArchiveItem[] Items, bool Sequential = false, CompressionType? StreamCompression = null);

public sealed class ArchiveContents(string? cacheDirectory = null) : IArchiveContents
{
    private readonly string temporaryRoot = Path.Combine(cacheDirectory ?? Path.Combine(Path.GetTempPath(), "Xcmd"), "archives-" + Guid.NewGuid().ToString("N"));
    private readonly object gate = new();
    private readonly Dictionary<string, ArchiveCatalog> catalogs = new(StringComparer.Ordinal);
    private readonly Dictionary<(ArchiveCatalog Catalog, string Name), string> extracted = [];
    private readonly Dictionary<ArchiveCatalog, string> reports = [];
    private const long PreviewLimit = 256 * 1024 * 1024;

    public Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken token) => Work<IReadOnlyList<FileEntry>>(() =>
    {
        var catalog = Get(ArchivePath.Source(path), token);
        var prefix = ArchivePath.EntryName(path);
        if (prefix.Length > 0 && !catalog.Items.Any(item => item.Name == prefix && item.Directory || item.Name.StartsWith(prefix + "/", StringComparison.Ordinal)))
            throw new DirectoryNotFoundException("The archive folder no longer exists.");
        if (prefix.Length > 0) prefix += "/";
        var rows = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in catalog.Items)
        {
            token.ThrowIfCancellationRequested();
            if (!item.Name.StartsWith(prefix, StringComparison.Ordinal) || item.Name.Length == prefix.Length) continue;
            var rest = item.Name[prefix.Length..];
            var separator = rest.IndexOf('/');
            var name = separator < 0 ? rest : rest[..separator];
            var directory = separator >= 0 || item.Directory;
            var row = new FileEntry(Path.Combine(path, name), name, directory, directory ? 0 : item.Size, item.Modified, IsLink: item.Link);
            if (!rows.TryAdd(name, row) && rows[name].Name != name)
                throw new IOException("This archive contains names that differ only by letter case. They cannot share a Windows panel.");
        }
        return rows.Values.ToArray();
    }, token);

    public Task<string> MaterializeAsync(string path, CancellationToken token) => Work(() => Materialize(path, token), token);

    public Task<ViewerSource> ViewerSourceAsync(string path, CancellationToken token) => Work(() =>
    {
        var physical = Materialize(path, token);
        using var input = new ArchiveInput(physical, token);
        bool archive;
        try { archive = HasArchiveContent(input, path); }
        catch (Exception error) when (IsArchiveError(error)) { archive = false; }
        if (!archive && !ArchivePath.OpensOnEnter(path)) return new ViewerSource(physical);
        var catalog = Get(path, token);
        if (reports.TryGetValue(catalog, out var report)) return new ViewerSource(report, true);
        report = TemporaryFile("archive-info.txt");
        File.WriteAllText(report, FormatReport(path, catalog), new UTF8Encoding(false));
        reports[catalog] = report;
        return new ViewerSource(report, true);
    }, token);

    public Task<long> ExtractAsync(string source, string destination, bool replace, IProgress<long> progress, CancellationToken token) => Task.Run(() =>
    {
        ArchivePath.RequireWritable(destination);
        ArchiveCatalog catalog;
        lock (gate) catalog = Get(ArchivePath.Source(source), token);
        return WriteEntry(catalog, ArchivePath.EntryName(source), destination, replace, long.MaxValue, progress, token);
    }, token);

    private Task<T> Work<T>(Func<T> action, CancellationToken token) => Task.Run(() =>
    {
        lock (gate) { token.ThrowIfCancellationRequested(); return action(); }
    }, token);

    private bool HasArchiveContent(ArchiveInput input, string path)
    {
        if (!ArchiveFactory.IsArchive(input, out var type)) return ArchiveCompression.Detect(input) != null;
        if (type != ArchiveType.Tar || ArchivePath.OpensOnEnter(path) || catalogs.ContainsKey(path)) return true;
        // Empty TAR headers are all zero, which is also common in ordinary binary files.
        input.Position = 0;
        Span<byte> header = stackalloc byte[512];
        var count = input.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        return header[..count].ContainsAnyExcept((byte)0);
    }

    private ArchiveCatalog Get(string path, CancellationToken token)
    {
        var physical = Materialize(path, token);
        var info = new FileInfo(physical);
        if (catalogs.TryGetValue(path, out var existing) && existing.File == physical && existing.Length == info.Length && existing.Modified == info.LastWriteTimeUtc) return existing;
        try
        {
            using var input = new ArchiveInput(physical, token);
            var result = ReadCatalog(input, physical, info, token);
            catalogs[path] = result;
            return result;
        }
        catch (Exception error) when (IsArchiveError(error))
        { throw new IOException("Could not read this archive. It may be damaged, encrypted, or unsupported. " + error.Message, error); }
    }

    private static ArchiveCatalog ReadCatalog(ArchiveInput input, string physical, FileInfo info, CancellationToken token)
    {
        IArchive archive;
        try { archive = ArchiveFactory.OpenArchive(input, new ReaderOptions { LeaveStreamOpen = true }); }
        catch (ArchiveOperationException)
        {
            input.Position = 0;
            try
            {
                using var reader = ReaderFactory.OpenReader(input, new ReaderOptions { LeaveStreamOpen = true });
                var items = Collect(SequentialEntries(reader), physical, token);
                return new(physical, info.Length, info.LastWriteTimeUtc, reader.Type.ToString(), false, true, items, true);
            }
            catch (InvalidFormatException) when (ArchiveCompression.Detect(input) is { })
            {
                var compression = ArchiveCompression.Detect(input)!.Value;
                var size = ArchiveCompression.Measure(input, compression, token);
                var item = new ArchiveItem(ItemName(null, physical), false, size, info.Length, info.LastWriteTimeUtc, false, false, compression.ToString());
                return new(physical, info.Length, info.LastWriteTimeUtc, compression + " stream", false, true, [item], StreamCompression: compression);
            }
        }
        using (archive)
            return new(physical, info.Length, info.LastWriteTimeUtc, archive.Type.ToString(), archive.IsSolid, archive.IsComplete, Collect(archive.Entries, physical, token));
    }

    private static IEnumerable<IEntry> SequentialEntries(IReader reader)
    {
        while (reader.MoveToNextEntry()) yield return reader.Entry;
    }

    private static ArchiveItem[] Collect(IEnumerable<IEntry> entries, string file, CancellationToken token)
    {
        var items = new List<ArchiveItem>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            if (items.Count >= 100_000) throw new IOException("Archive browsing is limited to 100,000 entries.");
            var name = ItemName(entry.Key, file);
            if (name.Length == 0) continue;
            if (!names.Add(name)) throw new IOException("The archive contains duplicate entry paths.");
            items.Add(new(name, entry.IsDirectory, entry.Size, entry.CompressedSize, entry.LastModifiedTime?.ToUniversalTime() ?? DateTime.MinValue,
                entry.IsEncrypted, entry.LinkTarget != null, entry.CompressionType.ToString()));
        }
        return items.ToArray();
    }

    private string Materialize(string path, CancellationToken token)
    {
        if (!ArchivePath.IsEntry(path)) return path;
        if (path.Count(character => character == '|') > 8) throw new IOException("Nested archives are limited to eight levels.");
        var catalog = Get(ArchivePath.Source(path), token);
        var name = ArchivePath.EntryName(path);
        if (extracted.TryGetValue((catalog, name), out var cached)) return cached;
        var target = TemporaryFile(Path.GetFileName(name));
        WriteEntry(catalog, name, target, false, PreviewLimit, null, token);
        extracted[(catalog, name)] = target;
        return target;
    }

    private static long WriteEntry(ArchiveCatalog catalog, string name, string destination, bool replace, long limit, IProgress<long>? progress, CancellationToken token)
    {
        var item = catalog.Items.SingleOrDefault(entry => entry.Name == name) ?? throw new FileNotFoundException("The archive entry no longer exists.");
        if (item.Directory || item.Link) throw new IOException("Archive folders and links cannot be opened as files.");
        if (item.Encrypted) throw new IOException("This entry is encrypted. Extract it with an archive tool that accepts a password.");
        if (item.Size > limit) throw new IOException("Archive preview is limited to 256 MiB per file. Copy the file out with F5 first.");
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".xcmd-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using var input = new ArchiveInput(catalog.File, token);
            using var archive = catalog.Sequential || catalog.StreamCompression != null ? null : ArchiveFactory.OpenArchive(input, new ReaderOptions { LeaveStreamOpen = true });
            using var reader = catalog.Sequential ? ReaderFactory.OpenReader(input, new ReaderOptions { LeaveStreamOpen = true }) : null;
            long total = 0;
            using (var data = catalog.StreamCompression is { } compression ? ArchiveCompression.Open(input, compression)
                : reader == null ? archive!.Entries.Single(entry => ItemName(entry.Key, catalog.File) == name).OpenEntryStream() : FindStream(reader, name, catalog.File, token))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[131072];
                int count;
                while ((count = data.Read(buffer)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    total += count;
                    if (total > limit) throw new IOException("The decompressed file exceeds the preview limit.");
                    output.Write(buffer, 0, count);
                    progress?.Report(total);
                }
            }
            token.ThrowIfCancellationRequested();
            if (item.Size >= 0 && total != item.Size) throw new IOException("The archive entry is incomplete.");
            if (item.Modified.Year >= 1601) File.SetLastWriteTimeUtc(temporary, item.Modified.ToUniversalTime());
            if (File.Exists(destination) && File.GetAttributes(destination).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("The destination is a link.");
            File.Move(temporary, destination, replace);
            return total;
        }
        catch (Exception error) when (IsArchiveError(error)) { throw new IOException("Could not extract this archive entry. " + error.Message, error); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static Stream FindStream(IReader reader, string name, string file, CancellationToken token)
    {
        while (reader.MoveToNextEntry())
        {
            token.ThrowIfCancellationRequested();
            if (ItemName(reader.Entry.Key, file) == name) return reader.OpenEntryStream();
        }
        throw new FileNotFoundException("The archive entry no longer exists.");
    }

    private string TemporaryFile(string name)
    {
        var directory = Path.Combine(temporaryRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name);
    }

    private static string SafeName(string? key)
    {
        var name = (key ?? "").Replace('\\', '/').TrimEnd('/');
        while (name.StartsWith("./", StringComparison.Ordinal)) name = name[2..];
        if (name == ".") return "";
        if (name.StartsWith('/') || name.Split('/').Any(part => part == ".." || part.Length == 0 || PanelRules.ValidateName(part) != null) && name.Length > 0)
            throw new IOException("The archive contains an unsafe or unsupported entry path.");
        return name;
    }

    private static string ItemName(string? key, string file) => SafeName(string.IsNullOrEmpty(key) ? Path.GetFileNameWithoutExtension(file) : key);

    private static string FormatReport(string path, ArchiveCatalog catalog)
    {
        var files = catalog.Items.Where(item => !item.Directory).ToArray();
        var text = new StringBuilder($"Archive: {ArchivePath.Display(path)}\nFormat: {catalog.Format}\nArchive size: {catalog.Length:N0} bytes\n" +
            $"Files: {files.Length:N0}\nExplicit folders: {catalog.Items.Count(item => item.Directory):N0}\nUnpacked size: {files.Sum(item => item.Size):N0} bytes\n" +
            $"Solid: {catalog.Solid}\nComplete: {catalog.Complete}\nEncrypted entries: {files.Count(item => item.Encrypted):N0}\n" +
            $"Compression: {string.Join(", ", files.Select(item => item.Compression).Distinct())}\n\nContents\n\n");
        foreach (var item in catalog.Items)
            text.AppendLine($"{item.Name}{(item.Directory ? "/" : "")}\t{(item.Directory ? "<DIR>" : $"{item.Size:N0} bytes; packed {item.PackedSize:N0}")}\t{item.Modified:yyyy-MM-dd HH:mm:ss}{(item.Encrypted ? " [encrypted]" : "")}{(item.Link ? " [link]" : "")}");
        return text.ToString();
    }

    private static bool IsArchiveError(Exception error) => error is InvalidOperationException or NotSupportedException or ArgumentException or SharpCompressException or System.Security.Cryptography.CryptographicException;

    public void Dispose()
    {
        lock (gate)
        {
            try { if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

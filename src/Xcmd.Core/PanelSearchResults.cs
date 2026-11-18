// @implements SearchResultPanel, SortEntries, RenameEntry, RefreshEntries
namespace Xcmd.Core;

public sealed class PanelSearchResults(string root, IReadOnlyList<FileSearchResult> results)
{
    private FileSearchResult[] items = results.DistinctBy(result => result.Entry.FullPath, StringComparer.OrdinalIgnoreCase).ToArray();
    public string Root { get; } = root;

    public FileSearchResult[] Capture() => items.ToArray();
    public string? LabelFor(string? path) => items.FirstOrDefault(result =>
        result.Entry.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))?.RelativePath;

    public IEnumerable<(FileEntry Entry, string Label)> Sort(SortOrder order)
    {
        var labels = items.ToDictionary(result => result.Entry.FullPath, result => result.RelativePath, StringComparer.OrdinalIgnoreCase);
        return PanelRules.Sort(items.Select(result => result.Entry), order).Select(entry => (entry, labels[entry.FullPath]));
    }

    public (string Previous, string Renamed) Rename(FileEntry entry, string name)
    {
        var previous = items.First(result => result.Entry.FullPath.Equals(entry.FullPath, StringComparison.OrdinalIgnoreCase)).RelativePath;
        var renamed = Path.Combine(Path.GetDirectoryName(previous) ?? "", name);
        items = items.Select(result => result.Entry.FullPath.Equals(entry.FullPath, StringComparison.OrdinalIgnoreCase)
            ? result with
            {
                Entry = entry with { Name = name, FullPath = Path.Combine(ArchivePath.Parent(entry.FullPath)!, name) },
                RelativePath = renamed
            } : result).ToArray();
        return (previous, renamed);
    }

    public async Task<bool> RefreshAsync(IFileSystem fileSystem, Func<bool> canApply)
    {
        var snapshot = items;
        var updated = new List<FileSearchResult>();
        foreach (var group in snapshot.GroupBy(result => ArchivePath.Parent(result.Entry.FullPath), StringComparer.OrdinalIgnoreCase))
        {
            if (group.Key == null) { updated.AddRange(group); continue; }
            try
            {
                var entries = await fileSystem.ReadDirectoryAsync(group.Key, CancellationToken.None);
                var byPath = entries.ToDictionary(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase);
                foreach (var result in group)
                    if (byPath.TryGetValue(result.Entry.FullPath, out var entry)) updated.Add(result with { Entry = entry });
            }
            catch (DirectoryNotFoundException) { }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { updated.AddRange(group); }
        }
        if (items != snapshot || !canApply()) return false;
        items = updated.ToArray();
        return true;
    }
}

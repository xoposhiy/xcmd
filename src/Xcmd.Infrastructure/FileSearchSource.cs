using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class FileSearchSource(string root, string? cacheRoot = null) : IFileSearchSource
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<SearchFailure> failures = new();
    public IReadOnlyList<SearchFailure> Failures => failures.ToArray();
    private readonly LocalFileSystem files = new();
    private readonly SearchGitScope git = new();
    private readonly ArchiveContents archives = new(cacheRoot);
    private bool explicitIgnoredRoot;
    private bool scopeInitialized;

    public async Task<SearchCandidate> ReadFileAsync(string path, CancellationToken token)
    {
        if (ArchivePath.IsEntry(path))
        {
            var items = await archives.ReadDirectoryAsync(ArchivePath.Parent(path)!, token).ConfigureAwait(false);
            return new(items.FirstOrDefault(entry => entry.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException("The archive entry no longer exists.", path));
        }
        return await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var file = new FileInfo(path);
            return new SearchCandidate(new(path, file.Name, false, file.Length, file.LastWriteTimeUtc, IsLink: file.Attributes.HasFlag(FileAttributes.ReparsePoint)));
        }, token).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SearchCandidate>> ReadDirectoryAsync(string path, CancellationToken token)
    {
        if (ArchivePath.IsEntry(path))
        {
            var items = await archives.ReadDirectoryAsync(path, token).ConfigureAwait(false);
            return items.Select(entry => new SearchCandidate(entry)).ToArray();
        }
        var entries = await files.ReadDirectoryAsync(path, token).ConfigureAwait(false);
        (Dictionary<string, string> ignored, HashSet<string> tracked) classification;
        try
        {
            if (!scopeInitialized) { explicitIgnoredRoot = await git.IsIgnoredRootAsync(root, token).ConfigureAwait(false); scopeInitialized = true; }
            classification = await git.ClassifyAsync(path, entries.Select(entry => entry.FullPath).ToArray(), token).ConfigureAwait(false);
        }
        catch (IOException error)
        {
            failures.Enqueue(new(path, error.Message, "Git", true));
            return entries.Select(entry => new SearchCandidate(entry)).ToArray();
        }
        var (ignored, tracked) = classification;
        return entries.Select(entry => new SearchCandidate(entry, !explicitIgnoredRoot && ignored.ContainsKey(entry.FullPath), tracked.Contains(entry.FullPath), ignored.GetValueOrDefault(entry.FullPath))).ToArray();
    }

    public async Task<SearchTextMatch?> FindTextAsync(string path, FileSearchQuery query, CancellationToken token)
    {
        var physical = ArchivePath.IsEntry(path) ? await archives.MaterializeAsync(path, token).ConfigureAwait(false) : path;
        return await SearchText.FindAsync(physical, query, token).ConfigureAwait(false);
    }

    public void Dispose() => archives.Dispose();
}

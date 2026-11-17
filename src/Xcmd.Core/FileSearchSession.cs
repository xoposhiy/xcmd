// @implements FindFiles, FindFileText, PrioritizeSearch, InspectSearchScope, ControlSearch
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Xcmd.Core;

public sealed class FileSearchSession(FileSearchQuery query, IFileSearchSource source, Action<FileSearchResult> found)
{
    private readonly SearchNameMatcher names = new(query);
    private readonly ConcurrentQueue<SearchCandidate> deferred = new();
    private readonly ConcurrentQueue<SearchFailure> failures = new();
    private readonly HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);
    private int visited;
    private int resultCount;
    private int running;
    private bool started;
    public int Visited => Volatile.Read(ref visited);
    public int ResultCount => Volatile.Read(ref resultCount);
    public int DeferredCount => deferred.Count;
    public IReadOnlyList<DeferredSearchLocation> DeferredLocations => deferred.Select(candidate =>
        new DeferredSearchLocation(candidate.Entry.FullPath, candidate.Entry.IsDirectory, FileSearchScope.DeferredReason(candidate) ?? "Deferred", candidate.IgnoredBy)).ToArray();
    public IReadOnlyList<SearchFailure> Failures => failures.Concat(source.Failures).ToArray();
    public bool Stopped { get; private set; }
    public bool Limited { get; private set; }
    public bool IsRunning => Volatile.Read(ref running) != 0;

    public async Task RunAsync(bool includeDeferred, CancellationToken token)
    {
        if (Interlocked.Exchange(ref running, 1) != 0) throw new InvalidOperationException("This search is already running.");
        var small = Channel.CreateUnbounded<FileEntry>(new() { SingleWriter = false });
        var large = Channel.CreateUnbounded<FileEntry>(new() { SingleWriter = false });
        var archiveText = Channel.CreateUnbounded<FileEntry>(new() { SingleWriter = false });
        var archives = Channel.CreateUnbounded<string>(new() { SingleReader = true, SingleWriter = true });
        try
        {
            query.Validate();
            var roots = new Queue<string>();
            var files = new List<SearchCandidate>();
            if (!started)
            {
                if (query.RootIsFile) { files.Add(await source.ReadFileAsync(query.Root, token).ConfigureAwait(false)); Interlocked.Increment(ref visited); }
                else roots.Enqueue(query.Root);
                started = true;
            }
            if (includeDeferred)
                while (deferred.TryDequeue(out var candidate))
                { if (candidate.Entry.IsDirectory) { EmitName(candidate.Entry); roots.Enqueue(candidate.Entry.FullPath); } else files.Add(candidate); }
            var workers = new[] { ConsumeAsync(small.Reader, token), ConsumeAsync(small.Reader, token), ConsumeAsync(large.Reader, token), ConsumeAsync(archiveText.Reader, token) };
            var archiveWorker = ReadArchivesAsync(archives.Reader, archiveText.Writer, includeDeferred, token);
            try
            {
                foreach (var candidate in files) Schedule(candidate, small.Writer, large.Writer, archives.Writer);
                await TraverseAsync(roots, small.Writer, large.Writer, archives.Writer, includeDeferred, token).ConfigureAwait(false);
            }
            finally
            {
                archives.Writer.TryComplete();
                try { await archiveWorker.ConfigureAwait(false); }
                finally { small.Writer.TryComplete(); large.Writer.TryComplete(); archiveText.Writer.TryComplete(); await Task.WhenAll(workers).ConfigureAwait(false); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { Stopped = true; }
        finally { Volatile.Write(ref running, 0); }
    }

    private async Task TraverseAsync(Queue<string> directories, ChannelWriter<FileEntry> small, ChannelWriter<FileEntry> large,
        ChannelWriter<string>? archives, bool includeDeferred, CancellationToken token)
    {
        while (directories.TryDequeue(out var directory) && !Limited)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var entries = await source.ReadDirectoryAsync(directory, token).ConfigureAwait(false);
                foreach (var candidate in entries.OrderBy(item => item.Entry.IsDirectory).ThenBy(item => item.Entry.Size))
                {
                    token.ThrowIfCancellationRequested();
                    if (Interlocked.Increment(ref visited) > 200_000) { Limited = true; break; }
                    if (!includeDeferred && FileSearchScope.IsDeferred(candidate)) { deferred.Enqueue(candidate); continue; }
                    var entry = candidate.Entry;
                    if (entry.IsLink) { Fail(entry.FullPath, "Links are not followed.", "Link", entry.IsDirectory); continue; }
                    if (entry.IsDirectory) { EmitName(entry); directories.Enqueue(entry.FullPath); }
                    else Schedule(candidate, small, large, archives);
                }
            }
            catch (Exception error) when (IsSearchError(error)) { Fail(directory, error.Message, FailureReason(directory, error), true); }
        }
    }

    private void Schedule(SearchCandidate candidate, ChannelWriter<FileEntry> small, ChannelWriter<FileEntry> large, ChannelWriter<string>? archives)
    {
        var entry = candidate.Entry;
        EmitName(entry);
        if (ArchivePath.OpensOnEnter(entry.FullPath))
        {
            if (archives != null) archives.TryWrite(ArchivePath.Root(entry.FullPath));
            else Fail(entry.FullPath, "Nested archive contents are not searched.", "Archive");
        }
        else if (query.Text.Length > 0 && names.Matches(entry, Relative(entry.FullPath)))
            (entry.Size <= 1024 * 1024 ? small : large).TryWrite(entry);
    }

    private async Task ReadArchivesAsync(ChannelReader<string> archives, ChannelWriter<FileEntry> text,
        bool includeDeferred, CancellationToken token)
    {
        await foreach (var root in archives.ReadAllAsync(token).ConfigureAwait(false))
            await TraverseAsync(new Queue<string>([root]), text, text, null, includeDeferred, token).ConfigureAwait(false);
    }

    private async Task ConsumeAsync(ChannelReader<FileEntry> entries, CancellationToken token)
    {
        await foreach (var entry in entries.ReadAllAsync(token).ConfigureAwait(false))
        {
            if (Limited) continue;
            try
            {
                var match = await source.FindTextAsync(entry.FullPath, query, token).ConfigureAwait(false);
                if (match != null) Emit(entry, match);
            }
            catch (Exception error) when (IsSearchError(error)) { Fail(entry.FullPath, error.Message, FailureReason(entry.FullPath, error)); }
        }
    }

    private void EmitName(FileEntry entry)
    { if (query.Text.Length == 0 && names.Matches(entry, Relative(entry.FullPath))) Emit(entry, null); }

    private void Emit(FileEntry entry, SearchTextMatch? match)
    {
        lock (emitted)
        {
            if (resultCount >= 20_000) { Limited = true; return; }
            if (!emitted.Add(entry.FullPath)) return;
            Interlocked.Increment(ref resultCount);
            found(new(entry, Relative(entry.FullPath), match));
        }
    }

    private string Relative(string path) => ArchivePath.Display(Path.GetRelativePath(query.RootIsFile ? ArchivePath.Parent(query.Root)! : query.Root, path));
    private void Fail(string path, string message, string reason, bool directory = false) => failures.Enqueue(new(path, message, reason, directory));
    private static string FailureReason(string path, Exception error) => error switch
    {
        UnauthorizedAccessException => "Access",
        System.Text.RegularExpressions.RegexMatchTimeoutException => "Regex",
        _ => ArchivePath.IsEntry(path) || ArchivePath.OpensOnEnter(path) ? "Archive" : "Read"
    };
    private static bool IsSearchError(Exception error) => error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Text.RegularExpressions.RegexMatchTimeoutException;
}

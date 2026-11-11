using Xcmd.Core;

namespace Xcmd.Probe.Infrastructure;

public sealed class SearchWorkProbe : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly TaskCompletionSource<FileSearchResult> ordinaryResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PausedSource source;
    public FileSearchSession Session { get; }
    public Task Completion { get; }

    public SearchWorkProbe(string root)
    {
        source = new(root);
        Session = new(new(root, "", "needle"), source, result => ordinaryResult.TrySetResult(result));
        Completion = Task.Run(() => Session.RunAsync(false, cancellation.Token));
    }

    public Task<FileSearchResult> WaitForOrdinaryResultAsync() => ordinaryResult.Task.WaitAsync(TimeSpan.FromSeconds(5));
    public async Task CancelAsync() { cancellation.Cancel(); await Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
    public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); source.Dispose(); }

    private sealed class PausedSource(string root) : IFileSearchSource
    {
        private readonly TaskCompletionSource archiveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource largeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<SearchCandidate>> ReadDirectoryAsync(string path, CancellationToken token)
        {
            if (ArchivePath.IsEntry(path))
            { archiveStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            return path == root ? [Item("slow.zip"), Item("large.txt", size: 2 * 1024 * 1024), Item("notes", directory: true)]
                : [Item("notes/answer.txt")];
        }

        public async Task<SearchTextMatch?> FindTextAsync(string path, FileSearchQuery query, CancellationToken token)
        {
            if (path.EndsWith("large.txt", StringComparison.Ordinal))
            { largeStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            await Task.WhenAll(archiveStarted.Task, largeStarted.Task).WaitAsync(token);
            return new(1, "needle", 65001, "needle");
        }

        private SearchCandidate Item(string name, bool directory = false, long size = 10) => new(new(Path.Combine(root, name.Replace('/', '\\')), Path.GetFileName(name), directory, size, DateTime.MinValue));
        public void Dispose() { }
    }
}

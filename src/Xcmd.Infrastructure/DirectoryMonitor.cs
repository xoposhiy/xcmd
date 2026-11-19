// @implements RefreshEntries
using System.Diagnostics;

namespace Xcmd.Infrastructure;

public sealed class DirectoryMonitor : IDisposable
{
    private FileSystemWatcher? watcher;
    private long revision;
    private long changed = Stopwatch.GetTimestamp();
    private long lastProbe;
    private int failed;
    private bool missing;
    private bool disposed;
    public string Path { get; }
    public long Revision => Interlocked.Read(ref revision);
    public bool IsQuiet => Stopwatch.GetElapsedTime(Interlocked.Read(ref changed)) >= TimeSpan.FromMilliseconds(180);

    public DirectoryMonitor(string path) { Path = path; Start(); }

    private void Start()
    {
        watcher?.Dispose(); watcher = null;
        try
        {
            watcher = new FileSystemWatcher(Path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.Attributes,
                IncludeSubdirectories = false
            };
            watcher.Created += OnChanged; watcher.Deleted += OnChanged; watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += (_, _) => { Interlocked.Exchange(ref failed, 1); Invalidate(); };
            watcher.EnableRaisingEvents = true;
            Interlocked.Exchange(ref failed, 0);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { Interlocked.Exchange(ref failed, 1); }
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Invalidate();
    private void OnRenamed(object sender, RenamedEventArgs e) => Invalidate();
    private void Invalidate()
    {
        if (disposed) return;
        Interlocked.Increment(ref revision);
        Interlocked.Exchange(ref changed, Stopwatch.GetTimestamp());
    }

    public async Task ProbeAsync()
    {
        if (Stopwatch.GetElapsedTime(lastProbe) < TimeSpan.FromSeconds(2)) return;
        lastProbe = Stopwatch.GetTimestamp();
        var exists = await Task.Run(() => Directory.Exists(Path));
        if (disposed) return;
        if (!exists && !missing) Invalidate();
        missing = !exists;
        if (exists && Volatile.Read(ref failed) != 0) { Start(); Invalidate(); }
    }

    public void Dispose() { disposed = true; watcher?.Dispose(); }
}

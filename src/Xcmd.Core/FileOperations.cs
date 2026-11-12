namespace Xcmd.Core;

public enum FileOperationKind { Copy, Move, Recycle, Delete }
public enum FileOperationState { Queued, Counting, Running, WaitingForConflict, WaitingForError, Cancelling, Completed, CompletedWithSkippedItems, CompletedWithErrors, Cancelled, Failed }
public enum FileOutcomeKind { Completed, Skipped, Failed, Partial, Cancelled, NotStarted }
public enum ConflictAction { Skip, Rename, Replace, Cancel }

public sealed record FileOperationRequest(FileOperationKind Kind, FileEntry[] Sources, string? Destination = null, string? NewName = null);
public sealed record FileOutcome(string Source, string? Destination, FileOutcomeKind Kind, string? Message = null, bool IsDirectory = false);
public sealed record FileConflict(FileEntry Source, FileEntry Destination);
public sealed record ConflictDecision(ConflictAction Action, string? NewName = null, bool ApplyToAll = false);
public enum FileErrorAction { Retry, Skip, SkipAll, Cancel }
public sealed record FileOperationError(FileEntry Source, string? Destination, string Message);

public static class FileOperationNames
{
    public static string CopyName(FileEntry entry, int number = 1)
    {
        var extension = entry.IsDirectory || entry.Name.LastIndexOf('.') <= 0 ? "" : Path.GetExtension(entry.Name);
        var stem = entry.Name[..(entry.Name.Length - extension.Length)];
        return stem + (number == 1 ? " (copy)" : $" (copy {number})") + extension;
    }
}

public interface IFileOperationStore
{
    Task<FileEntry?> InspectAsync(string path, CancellationToken cancellationToken);
    Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken cancellationToken);
    Task<bool> AreIdenticalAsync(FileEntry source, FileEntry destination, CancellationToken cancellationToken);
    Task CreateFolderAsync(string path, CancellationToken cancellationToken);
    Task<long> CopyAsync(string source, string destination, bool replace, IProgress<long> progress, CancellationToken cancellationToken);
    Task<bool> TryMoveAsync(FileEntry source, string destination, bool replace, CancellationToken cancellationToken);
    Task RemoveAsync(FileEntry entry, bool recycle, CancellationToken cancellationToken);
    Task CopyMetadataAsync(string source, string destination, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FileOperationJob(int id, FileOperationRequest request)
{
    private readonly CancellationTokenSource cancellation = new();
    public int Id { get; } = id;
    public FileOperationRequest Request { get; } = request;
    public FileOperationState State { get; internal set; } = FileOperationState.Queued;
    public string? CurrentPath { get; internal set; }
    public long BytesTransferred { get; internal set; }
    public long TotalFiles { get; internal set; }
    public long TotalBytes { get; internal set; }
    public long ProcessedFiles { get; private set; }
    public long ProcessedBytes { get; private set; }
    public long CompletedFiles { get; private set; }
    public long SkippedFiles { get; private set; }
    public long FailedEntries { get; private set; }
    public long CurrentFileBytes { get; internal set; }
    public bool TotalsComplete { get; internal set; } = true;
    internal Dictionary<string, OperationWork> CountedWork { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FileOutcome> Outcomes { get; } = [];
    public bool IsFinished => State is FileOperationState.Completed or FileOperationState.CompletedWithSkippedItems or FileOperationState.CompletedWithErrors or FileOperationState.Cancelled or FileOperationState.Failed;
    public CancellationToken Token => cancellation.Token;
    internal void Cancel() { if (!IsFinished) { State = FileOperationState.Cancelling; cancellation.Cancel(); } }
    internal void DisposeCancellation() => cancellation.Dispose();

    internal void Record(FileEntry source, string? destination, FileOutcomeKind kind, string? message = null, bool wholeTree = false)
    {
        Outcomes.Add(new(source.FullPath, destination, kind, message, source.IsDirectory));
        if (kind == FileOutcomeKind.Failed) FailedEntries++;
        if (kind is not (FileOutcomeKind.Completed or FileOutcomeKind.Skipped or FileOutcomeKind.Failed)) return;
        var work = source.IsDirectory ? wholeTree ? CountedWork.GetValueOrDefault(source.FullPath, new(0, 0)) : new(0, 0)
            : new OperationWork(1, source.IsLink ? 0 : source.Size);
        ProcessedFiles += work.Files; ProcessedBytes += work.Bytes;
        TotalFiles = Math.Max(TotalFiles, ProcessedFiles); TotalBytes = Math.Max(TotalBytes, ProcessedBytes);
        if (kind == FileOutcomeKind.Completed) CompletedFiles += work.Files;
        if (kind == FileOutcomeKind.Skipped) SkippedFiles += work.Files;
        CurrentFileBytes = 0;
    }
}

public sealed class FileOperationQueue(FileOperationRunner runner)
{
    private readonly List<FileOperationJob> jobs = [];
    private bool draining;
    public IReadOnlyList<FileOperationJob> Jobs => jobs;
    public bool HasPendingWork => jobs.Any(job => !job.IsFinished);
    public bool IsWaitingForDecision => jobs.Any(job => job.State is FileOperationState.WaitingForConflict or FileOperationState.WaitingForError);
    public Task Completion { get; private set; } = Task.CompletedTask;
    public event Action? Changed;
    public event Action<FileOperationJob>? Finished;

    public FileOperationJob Enqueue(FileOperationRequest request)
    {
        var job = new FileOperationJob(jobs.Count == 0 ? 1 : jobs[^1].Id + 1, request with { Sources = request.Sources.Where(entry => !entry.IsParent).ToArray() });
        jobs.Add(job);
        Changed?.Invoke();
        if (!draining) Completion = DrainAsync();
        return job;
    }

    public void Cancel(FileOperationJob job)
    {
        if (job.IsFinished) return;
        var queued = job.State == FileOperationState.Queued;
        job.Cancel();
        if (queued)
        {
            job.Outcomes.AddRange(job.Request.Sources.Select(entry => new FileOutcome(entry.FullPath, null, FileOutcomeKind.NotStarted)));
            job.State = FileOperationState.Cancelled;
            job.DisposeCancellation();
        }
        Changed?.Invoke();
    }

    public void CancelAll() { foreach (var job in jobs.ToArray()) Cancel(job); }

    private async Task DrainAsync()
    {
        draining = true;
        try
        {
            while (jobs.FirstOrDefault(job => job.State == FileOperationState.Queued) is { } job)
            {
                try { await runner.RunAsync(job, () => Changed?.Invoke()); }
                catch (Exception error)
                {
                    job.Outcomes.Add(new(job.CurrentPath ?? "", null, FileOutcomeKind.Failed, error.Message));
                    job.State = FileOperationState.Failed;
                }
                finally { job.DisposeCancellation(); }
                Changed?.Invoke();
                Finished?.Invoke(job);
            }
        }
        finally { draining = false; Changed?.Invoke(); }
    }
}

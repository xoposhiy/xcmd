namespace Xcmd.Core;

public sealed record OperationWork(long Files, long Bytes);

public static class FileOperationInventory
{
    public static async Task CountAsync(FileOperationJob job, IFileOperationStore store, Action changed)
    {
        foreach (var source in job.Request.Sources)
        {
            job.Token.ThrowIfCancellationRequested();
            try
            {
                var current = await store.InspectAsync(source.FullPath, job.Token);
                if (current == null) { job.TotalsComplete = false; continue; }
                await CountEntryAsync(current, job, store, changed);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { job.TotalsComplete = false; }
        }
    }

    private static async Task<OperationWork> CountEntryAsync(FileEntry entry, FileOperationJob job, IFileOperationStore store, Action changed)
    {
        job.Token.ThrowIfCancellationRequested();
        job.CurrentPath = entry.FullPath;
        if (!entry.IsDirectory)
        {
            var file = new OperationWork(1, entry.IsLink ? 0 : entry.Size);
            job.TotalFiles++; job.TotalBytes += file.Bytes;
            if (job.TotalFiles % 256 == 0) { changed(); await Task.Yield(); }
            return file;
        }
        long files = 0, bytes = 0;
        if (!entry.IsLink)
        {
            try
            {
                foreach (var child in await store.ReadDirectoryAsync(entry.FullPath, job.Token))
                {
                    var work = await CountEntryAsync(child, job, store, changed);
                    files += work.Files; bytes += work.Bytes;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { job.TotalsComplete = false; }
        }
        var total = new OperationWork(files, bytes);
        job.CountedWork[entry.FullPath] = total;
        changed();
        return total;
    }
}

public static class FileOperationRules
{
    public static void ValidateRequest(FileOperationRequest request)
    {
        if (request.Kind != FileOperationKind.Copy)
            foreach (var source in request.Sources) ArchivePath.RequireWritable(source.FullPath);
        if (request.Destination != null) ArchivePath.RequireWritable(request.Destination);
        if (request.Kind is FileOperationKind.Recycle or FileOperationKind.Delete) return;
        if (request.Destination == null) throw new ArgumentException("Choose a destination folder.");
        if (request.NewName != null && PanelRules.ValidateName(request.NewName) is { } error) throw new ArgumentException(error);
        foreach (var source in request.Sources)
            ValidateTarget(source, Path.Combine(request.Destination, request.NewName ?? source.Name));
    }

    public static void ValidateTarget(FileEntry source, string destination)
    {
        var from = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.FullPath));
        var to = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a different name or folder. An item cannot be copied or moved onto itself.");
        if (source.IsDirectory && to.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("A folder cannot be copied or moved into itself.");
    }
}

public static class FileOperationProgress
{
    public static double Fraction(FileOperationJob job)
    {
        if (job.State is FileOperationState.Queued or FileOperationState.Counting) return 0;
        if (job.TotalBytes > 0) return Math.Clamp((job.ProcessedBytes + (double)job.CurrentFileBytes) / job.TotalBytes, 0, 1);
        if (job.TotalFiles > 0) return Math.Clamp((double)job.ProcessedFiles / job.TotalFiles, 0, 1);
        return job.IsFinished ? 1 : 0;
    }

    public static TimeSpan? Remaining(FileOperationJob job, TimeSpan elapsed)
    {
        var fraction = Fraction(job);
        if (elapsed.TotalSeconds < 1 || fraction <= 0 || fraction >= 1 || !job.TotalsComplete) return null;
        var seconds = Math.Min(TimeSpan.MaxValue.TotalSeconds - 1, elapsed.TotalSeconds * (1 - fraction) / fraction);
        return TimeSpan.FromSeconds(seconds);
    }
}

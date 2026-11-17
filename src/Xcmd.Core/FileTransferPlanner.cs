// @implements TransferEntries
namespace Xcmd.Core;

public static class FileTransferPlanner
{
    public static async Task<FileOperationRequest> PlanAsync(IFileOperationStore store, FileOperationKind kind, FileEntry[] sources,
        string sourceDirectory, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Enter a destination.");
        var target = Path.GetFullPath(text.Trim(), sourceDirectory);
        var entry = await store.InspectAsync(target, cancellationToken);
        FileOperationRequest request;
        if (entry?.IsDirectory == true)
        {
            var sameDirectory = Path.TrimEndingDirectorySeparator(target).Equals(Path.TrimEndingDirectorySeparator(sourceDirectory), StringComparison.OrdinalIgnoreCase);
            if (sameDirectory) throw new IOException("Choose a different folder, or enter a new name for one item.");
            request = new(kind, sources, target);
        }
        else
        {
            if (Path.EndsInDirectorySeparator(text) || sources.Length != 1) throw new IOException("Choose an existing destination folder.");
            request = new(kind, sources, Path.GetDirectoryName(target), Path.GetFileName(target));
        }
        FileOperationRules.ValidateRequest(request);
        return request;
    }
}

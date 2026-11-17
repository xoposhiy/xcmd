// @implements ClipboardTransfer
namespace Xcmd.Core;

public static class ClipboardTransferPlanner
{
    public static async Task<IReadOnlyList<FileOperationRequest>> PlanAsync(IFileOperationStore store,
        FileEntry[] sources, string destination, bool cut, CancellationToken cancellationToken)
    {
        if (await store.InspectAsync(destination, cancellationToken) is not { IsDirectory: true })
            throw new IOException("The destination folder is no longer available.");
        var distinct = sources.DistinctBy(source => source.FullPath, StringComparer.OrdinalIgnoreCase).ToArray();
        var roots = distinct.Where(source => !distinct.Any(parent => parent != source && parent.IsDirectory &&
            source.FullPath.StartsWith(Path.TrimEndingDirectorySeparator(parent.FullPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))).ToArray();
        var requests = new List<FileOperationRequest>();
        var transfers = new List<FileEntry>();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, source.Name);
            if (!source.FullPath.Equals(target, StringComparison.OrdinalIgnoreCase)) { transfers.Add(source); continue; }
            if (cut) continue;
            var number = 0;
            string name;
            do { name = FileOperationNames.CopyName(source, ++number); target = Path.Combine(destination, name); }
            while (reserved.Contains(target) || await store.InspectAsync(target, cancellationToken) != null);
            reserved.Add(target);
            requests.Add(new(FileOperationKind.Copy, [source], destination, name));
        }
        if (transfers.Count > 0) requests.Add(new(cut ? FileOperationKind.Move : FileOperationKind.Copy, transfers.ToArray(), destination));
        foreach (var request in requests) FileOperationRules.ValidateRequest(request);
        return requests;
    }
}

// @implements HeadlessVerification
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public sealed class ReadGate(string path)
{
    public string Path { get; } = path;
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool WasCancelled { get; set; }
}

public sealed class ControlledFileSystem : IFileSystem, IFileOperationStore
{
    private readonly IFileSystem actual = new LocalFileSystem();
    private readonly LocalFileOperations operations;
    private ReadGate? nextRead;
    private ReadGate? nextCopy;
    private ReadGate? nextRemoval;
    private int removalsBeforePause;
    private readonly HashSet<string> deniedReads = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<FileEntry>> suppliedDirectories = new(StringComparer.OrdinalIgnoreCase);
    public void SupplyDirectory(string path, IReadOnlyList<FileEntry> entries) => suppliedDirectories[path] = entries;
    public bool ForceCopyMove { get; set; }
    public ControlledFileSystem() => operations = new LocalFileOperations(this);
    public void DenyRead(string path) => deniedReads.Add(path);
    public ReadGate PauseNextCopy(string path) => nextCopy = new ReadGate(path);
    public ReadGate PauseNextRead(string path) => nextRead = new ReadGate(path);
    public ReadGate PauseRemovalsAfter(int completedFiles)
    {
        removalsBeforePause = completedFiles;
        return nextRemoval = new ReadGate("deletion");
    }

    public async Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (deniedReads.Contains(path)) throw new UnauthorizedAccessException("Controlled directory access failure.");
        if (suppliedDirectories.TryGetValue(path, out var supplied)) return supplied;
        if (nextRead is { } gate && path.Equals(gate.Path, StringComparison.OrdinalIgnoreCase))
        {
            nextRead = null;
            gate.Entered.TrySetResult();
            try { await gate.Release.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { gate.WasCancelled = true; throw; }
        }
        return await actual.ReadDirectoryAsync(path, cancellationToken);
    }

    public Task RenameAsync(FileEntry entry, string newName) => actual.RenameAsync(entry, newName);
    public Task CreateDirectoryAsync(string parent, string name) => actual.CreateDirectoryAsync(parent, name);
    public Task<FileEntry?> InspectAsync(string path, CancellationToken cancellationToken) => operations.InspectAsync(path, cancellationToken);
    public Task ValidateDirectoryAsync(string path, CancellationToken cancellationToken) => operations.ValidateDirectoryAsync(path, cancellationToken);
    public Task<bool> AreIdenticalAsync(FileEntry source, FileEntry destination, CancellationToken cancellationToken) => operations.AreIdenticalAsync(source, destination, cancellationToken);
    public Task CreateFolderAsync(string path, CancellationToken cancellationToken) => operations.CreateFolderAsync(path, cancellationToken);
    public Task CopyMetadataAsync(string source, string destination, CancellationToken cancellationToken) => operations.CopyMetadataAsync(source, destination, cancellationToken);
    public Task<long> CreateZipAsync(FileEntry[] sources, string destination, IProgress<long> progress, CancellationToken cancellationToken) =>
        operations.CreateZipAsync(sources, destination, progress, cancellationToken);
    public Task<bool> TryMoveAsync(FileEntry source, string destination, bool replace, CancellationToken cancellationToken) =>
        ForceCopyMove ? Task.FromResult(false) : operations.TryMoveAsync(source, destination, replace, cancellationToken);
    public async Task RemoveAsync(FileEntry entry, bool recycle, CancellationToken cancellationToken)
    {
        if (!entry.IsDirectory && removalsBeforePause == 0 && nextRemoval is { } gate)
        {
            nextRemoval = null;
            gate.Entered.TrySetResult();
            try { await gate.Release.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { gate.WasCancelled = true; throw; }
        }
        await operations.RemoveAsync(entry, recycle, cancellationToken);
        if (!entry.IsDirectory && nextRemoval != null) removalsBeforePause--;
    }
    public async Task<long> CopyAsync(string source, string destination, bool replace, IProgress<long> progress, CancellationToken cancellationToken)
    {
        if (nextCopy is { } gate && source.Equals(gate.Path, StringComparison.OrdinalIgnoreCase))
        {
            nextCopy = null;
            gate.Entered.TrySetResult();
            try { await gate.Release.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { gate.WasCancelled = true; throw; }
        }
        return await operations.CopyAsync(source, destination, replace, progress, cancellationToken);
    }
}

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

public sealed class ControlledFileSystem : IFileSystem
{
    private readonly IFileSystem actual = new LocalFileSystem();
    private ReadGate? nextRead;
    private ReadGate? nextCopy;
    private readonly HashSet<string> deniedReads = new(StringComparer.OrdinalIgnoreCase);
    public void DenyRead(string path) => deniedReads.Add(path);
    public ReadGate PauseNextCopy(string path) => nextCopy = new ReadGate(path);
    public ReadGate PauseNextRead(string path) => nextRead = new ReadGate(path);

    public async Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (deniedReads.Contains(path)) throw new UnauthorizedAccessException("Controlled directory access failure.");
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
    public async Task CopyFileAsync(FileEntry entry, string destination)
    {
        if (nextCopy is { } gate && entry.FullPath.Equals(gate.Path, StringComparison.OrdinalIgnoreCase))
        {
            nextCopy = null;
            gate.Entered.TrySetResult();
            await gate.Release.Task;
        }
        await actual.CopyFileAsync(entry, destination);
    }
}

using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public sealed class SyntaxReadGate : IViewerFiles
{
    private readonly ViewerFiles files = new();
    private TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool armed;
    private bool tailRead;
    public bool WasCancelled { get; private set; }

    public void Arm()
    {
        entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        armed = true; tailRead = false; WasCancelled = false;
    }

    public Task WaitForContextAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

    public async Task ReleaseAsync(ProbeContext context)
    {
        released.TrySetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await context.SettleAsync();
    }

    public async Task<FileSlice> ReadAsync(string path, long offset, CancellationToken cancellationToken)
    {
        if (armed && offset > 0) tailRead = true;
        if (!armed || !tailRead || offset != 0) return await files.ReadAsync(path, offset, cancellationToken);
        armed = false;
        entered.TrySetResult();
        await released.Task;
        WasCancelled = cancellationToken.IsCancellationRequested;
        var result = await files.ReadAsync(path, offset, CancellationToken.None);
        completed.TrySetResult();
        return result;
    }

    public Task<byte[]> ReadImageAsync(string path, CancellationToken cancellationToken) => files.ReadImageAsync(path, cancellationToken);
    public Task<FileTextMatch?> FindAsync(FileTextQuery query, CancellationToken cancellationToken) => files.FindAsync(query, cancellationToken);
}

// @implements HeadlessVerification
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public sealed class ControlledArchiveContents(string cacheDirectory) : IArchiveContents
{
    private readonly ArchiveContents actual = new(cacheDirectory);
    private ReadGate? nextRead;
    private ReadGate? nextViewerRead;
    private ReadGate? nextExtraction;
    public ReadGate PauseNextRead(string path) => nextRead = new ReadGate(path);
    public ReadGate PauseNextViewerRead(string path) => nextViewerRead = new ReadGate(path);
    public ReadGate PauseNextExtraction(string path) => nextExtraction = new ReadGate(path);

    public async Task<IReadOnlyList<FileEntry>> ReadDirectoryAsync(string path, CancellationToken token)
    {
        if (nextRead is { } gate && gate.Path == path)
        {
            nextRead = null;
            await WaitForReleaseAsync(gate, token);
        }
        return await actual.ReadDirectoryAsync(path, token);
    }

    public Task<long> ExtractAsync(string source, string destination, bool replace, IProgress<long> progress, CancellationToken token)
    {
        if (nextExtraction is not { } gate || gate.Path != source) return actual.ExtractAsync(source, destination, replace, progress, token);
        nextExtraction = null;
        return actual.ExtractAsync(source, destination, replace, new PausingProgress(progress, gate, token), token);
    }

    private sealed class PausingProgress(IProgress<long> progress, ReadGate gate, CancellationToken token) : IProgress<long>
    {
        private bool entered;
        public void Report(long value)
        {
            progress.Report(value);
            if (entered) return;
            entered = true;
            gate.Entered.TrySetResult();
            try { gate.Release.Task.WaitAsync(token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { gate.WasCancelled = true; throw; }
        }
    }

    public Task<string> MaterializeAsync(string path, CancellationToken token) => actual.MaterializeAsync(path, token);
    public async Task<ViewerSource> ViewerSourceAsync(string path, CancellationToken token)
    {
        if (nextViewerRead is { } gate && gate.Path == path)
        {
            nextViewerRead = null;
            await WaitForReleaseAsync(gate, token);
        }
        return await actual.ViewerSourceAsync(path, token);
    }

    private static async Task WaitForReleaseAsync(ReadGate gate, CancellationToken token)
    {
        gate.Entered.TrySetResult();
        try { await gate.Release.Task.WaitAsync(token); }
        catch (OperationCanceledException) { gate.WasCancelled = true; throw; }
    }
    public void Dispose() => actual.Dispose();
}

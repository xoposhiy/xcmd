namespace Xcmd.Desktop;

internal sealed class GitActionRunner(CommanderWindow owner, CancellationToken lifetime)
{
    private CancellationTokenSource? current;
    public bool IsOpeningViewer { get; private set; }
    public bool Cancel()
    {
        if (current == null) return false;
        current.Cancel(); return true;
    }

    public void Start(string message, Func<CancellationToken, Task> action, bool opensViewer = false) => owner.Execute(() => RunAsync(message, action, opensViewer));

    public async Task RunAsync(string message, Func<CancellationToken, Task> action, bool opensViewer = false)
    {
        current?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        current = request;
        IsOpeningViewer = opensViewer;
        owner.SetStatusMessage(message + " · Esc cancels");
        var completed = false;
        try { await action(request.Token); request.Token.ThrowIfCancellationRequested(); completed = true; }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        { if (current == request && !lifetime.IsCancellationRequested) owner.SetStatusMessage("Git operation canceled."); }
        finally
        {
            if (current == request)
            { current = null; IsOpeningViewer = false; if (completed && owner.LastError == null) owner.SetStatusMessage("Ready"); }
        }
    }
}

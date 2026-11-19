// @implements BrowseHistory, InspectCommitChanges, ReadDiff

namespace Xcmd.Desktop.Features.Git;

internal sealed class GitActionRunner(UiActions actions, Func<bool> hasError, CancellationToken lifetime)
{
    private CancellationTokenSource? current;
    public bool IsOpeningViewer { get; private set; }
    public bool Cancel()
    {
        if (current == null) return false;
        current.Cancel(); return true;
    }

    public void Start(string message, Func<CancellationToken, Task> action, bool opensViewer = false) => actions.Execute(() => RunAsync(message, action, opensViewer));

    public async Task RunAsync(string message, Func<CancellationToken, Task> action, bool opensViewer = false)
    {
        current?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        current = request;
        IsOpeningViewer = opensViewer;
        actions.SetStatus(message + " · Esc cancels");
        var completed = false;
        try { await action(request.Token); request.Token.ThrowIfCancellationRequested(); completed = true; }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        { if (current == request && !lifetime.IsCancellationRequested) actions.SetStatus("Git operation canceled."); }
        finally
        {
            if (current == request)
            { current = null; IsOpeningViewer = false; if (completed && !hasError()) actions.SetStatus("Ready"); }
        }
    }
}

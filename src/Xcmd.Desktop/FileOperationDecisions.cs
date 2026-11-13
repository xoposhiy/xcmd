using Avalonia.Controls;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class FileOperationDecisions(CommanderWindow owner)
{
    public async Task<ConflictDecision> ResolveConflictAsync(FileConflict conflict, CancellationToken cancellationToken)
    {
        await WaitForInteractionAsync(cancellationToken);
        var completion = new TaskCompletionSource<ConflictDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new FileConflictDialog(conflict, owner.Feedback) { Background = owner.Background }.WithFeedbackId("FileOperationDecisions.dialog");
        dialog.Decided += decision => { completion.TrySetResult(decision); dialog.Close(); };
        return await ShowAsync(dialog, completion, new(ConflictAction.Cancel), cancellationToken);
    }

    public async Task<FileErrorAction> ResolveErrorAsync(FileOperationError failure, CancellationToken cancellationToken)
    {
        await WaitForInteractionAsync(cancellationToken);
        var completion = new TaskCompletionSource<FileErrorAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new FileErrorDialog(failure, owner.Feedback) { Background = owner.Background }.WithFeedbackId("FileOperationDecisions.dialog.2");
        dialog.Decided += action => { completion.TrySetResult(action); dialog.Close(); };
        return await ShowAsync(dialog, completion, FileErrorAction.Cancel, cancellationToken);
    }

    private async Task WaitForInteractionAsync(CancellationToken cancellationToken)
    {
        while (owner.HasOpenDialog || owner.Feedback.IsActive || owner.IsMenuOpen || owner.Left.EditingRow != null || owner.Right.EditingRow != null || owner.CommandLine.HasKeyboardFocus)
            await Task.Delay(80, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<T> ShowAsync<T>(Window dialog, TaskCompletionSource<T> completion, T cancelled, CancellationToken cancellationToken)
    {
        owner.OperationDialog = dialog;
        owner.AttachOperationDialog(dialog);
        dialog.Closed += (_, _) =>
        {
            owner.OperationDialog = null;
            completion.TrySetResult(cancelled);
            owner.RecordActivity(); owner.ActivePanel.FocusCurrent(scrollIntoView: false);
        };
        using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(() => dialog.Close()));
        _ = dialog.ShowDialog(owner);
        return await completion.Task.WaitAsync(cancellationToken);
    }
}

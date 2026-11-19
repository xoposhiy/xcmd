// @implements RenameEntry, ResolveConflicts, RecoverOperations, TransferEntries, DuplicateEntry, DeleteEntries
using Avalonia.Controls;
using Avalonia.Threading;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Features.Feedback;

namespace Xcmd.Desktop.Features.FileOperations;

internal sealed class FileOperationDecisions(OperationDialogHost host, IFileOperationStore store, UiActions ui,
    Func<bool> canShowDialog, Action focusActivePanel)
{
    public event Action<FileOperationRequest>? Confirmed;

    public void ShowTransfer(FileOperationKind kind, FileEntry[] selected, string sourceDirectory, string targetDirectory, bool samePanel)
    {
        var dialog = new FileTransferDialog(kind, selected, targetDirectory, host.Feedback, samePanel) { Background = host.Window.Background }.WithFeedbackId("CommanderWindow.FileOperations.dialog");
        TrackConfirmation(dialog);
        dialog.Submitted += text => ui.Execute(async () =>
        {
            if (dialog.IsSubmitting) return;
            dialog.IsSubmitting = true;
            try
            {
                var request = await FileTransferPlanner.PlanAsync(store, kind, selected, sourceDirectory, text, CancellationToken.None);
                if (!dialog.IsVisible) return;
                dialog.Close(); Confirmed?.Invoke(request);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { dialog.Error.Text = error.Message; }
            finally { dialog.IsSubmitting = false; }
        });
        _ = dialog.ShowDialog(host.Window);
    }

    public void ShowDelete(FileOperationRequest request)
    {
        var dialog = new FileDeleteDialog(request, host.Feedback) { Background = host.Window.Background }.WithFeedbackId("CommanderWindow.FileOperations.dialog.2");
        TrackConfirmation(dialog);
        dialog.Confirmed += kind => { dialog.Close(); ui.SetError(null); Confirmed?.Invoke(request with { Kind = kind }); };
        _ = dialog.ShowDialog(host.Window);
    }

    private void TrackConfirmation(Window dialog)
    {
        host.Track(dialog);
        dialog.Closed += (_, _) => { host.Track(null); host.RecordActivity(); focusActivePanel(); };
    }

    public void ShowRenameError(FileOperationError failure, PanelView panel, Action completed)
    {
        var dialog = new FileErrorDialog(failure, host.Feedback, panel.EditingRow?.DraftName, panel.CommitRenameAsync)
        { Background = host.Window.Background }.WithFeedbackId("CommanderWindow.renameError");
        host.Track(dialog);
        dialog.Decided += action => { if (action == FileErrorAction.Cancel) dialog.Close(); };
        dialog.Closed += (_, _) =>
        {
            host.Track(null);
            host.RecordActivity();
            if (panel.EditingRow == null) completed();
            else panel.RenameEditor?.Focus();
        };
        _ = dialog.ShowDialog(host.Window);
    }

    public async Task<ConflictDecision> ResolveConflictAsync(FileConflict conflict, CancellationToken cancellationToken)
    {
        await WaitForInteractionAsync(cancellationToken);
        var completion = new TaskCompletionSource<ConflictDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new FileConflictDialog(conflict, host.Feedback) { Background = host.Window.Background }.WithFeedbackId("FileOperationDecisions.dialog");
        dialog.Decided += decision => { completion.TrySetResult(decision); dialog.Close(); };
        return await ShowAsync(dialog, completion, new(ConflictAction.Cancel), cancellationToken);
    }

    public async Task<FileErrorAction> ResolveErrorAsync(FileOperationError failure, CancellationToken cancellationToken)
    {
        await WaitForInteractionAsync(cancellationToken);
        var completion = new TaskCompletionSource<FileErrorAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new FileErrorDialog(failure, host.Feedback) { Background = host.Window.Background }.WithFeedbackId("FileOperationDecisions.dialog.2");
        dialog.Decided += action => { completion.TrySetResult(action); dialog.Close(); };
        return await ShowAsync(dialog, completion, FileErrorAction.Cancel, cancellationToken);
    }

    private async Task WaitForInteractionAsync(CancellationToken cancellationToken)
    {
        while (!canShowDialog())
            await Task.Delay(80, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<T> ShowAsync<T>(Window dialog, TaskCompletionSource<T> completion, T cancelled, CancellationToken cancellationToken)
    {
        host.Track(dialog);
        dialog.Closed += (_, _) =>
        {
            host.Track(null);
            completion.TrySetResult(cancelled);
            host.RecordActivity(); focusActivePanel();
        };
        using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(() => dialog.Close()));
        _ = dialog.ShowDialog(host.Window);
        return await completion.Task.WaitAsync(cancellationToken);
    }
}

internal sealed record OperationDialogHost(Window Window, FeedbackController Feedback, Action<Window?> Track, Action RecordActivity);

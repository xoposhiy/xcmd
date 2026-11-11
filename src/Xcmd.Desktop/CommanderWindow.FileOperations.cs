using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    private readonly IFileOperationStore operationStore;
    public FileOperationQueue Operations { get; }
    public FileOperationsView OperationsView { get; }
    public Window? OperationDialog { get; private set; }

    private FileEntry[] SelectedOperationEntries()
    {
        var selected = ActivePanel.Table.SelectedItems!.OfType<FileRow>().Select(row => row.Entry).Where(entry => !entry.IsParent).ToArray();
        return selected.Length > 0 ? selected : ActivePanel.Current is { Entry.IsParent: false } current ? [current.Entry] : [];
    }

    private void TransferSelected(FileOperationKind kind, bool samePanel = false)
    {
        if (HasOpenDialog) return;
        var selected = SelectedOperationEntries();
        if (selected.Length == 0) return;
        if (kind != FileOperationKind.Copy && selected.Any(entry => ArchivePath.IsEntry(entry.FullPath)))
        { SetError("Archives are read-only. Use F5 to copy files out."); return; }
        ResetQuickJump();
        SetError(null);
        var sourceDirectory = ActivePanel.Session.DirectoryPath;
        var targetDirectory = samePanel ? sourceDirectory : OppositePanel.Session.DirectoryPath;
        if (ArchivePath.IsEntry(targetDirectory)) { SetError("Archives are read-only. Choose a regular destination folder."); return; }
        var dialog = new FileTransferDialog(kind, selected, targetDirectory, Feedback, samePanel) { Background = Background };
        OperationDialog = dialog;
        AttachOperationDialog(dialog);
        dialog.Submitted += text => Execute(async () =>
        {
            if (dialog.IsSubmitting) return;
            dialog.IsSubmitting = true;
            try
            {
                var request = await FileTransferPlanner.PlanAsync(operationStore, kind, selected, sourceDirectory, text, CancellationToken.None);
                if (!dialog.IsVisible) return;
                dialog.Close(); Operations.Enqueue(request);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { dialog.Error.Text = error.Message; }
            finally { dialog.IsSubmitting = false; }
        });
        dialog.Closed += (_, _) => { OperationDialog = null; RecordActivity(); ActivePanel.FocusCurrent(scrollIntoView: false); };
        _ = dialog.ShowDialog(this);
    }

    private void DeleteSelected(bool permanently)
    {
        if (HasOpenDialog) return;
        if (ArchivePath.IsEntry(ActivePanel.Session.DirectoryPath)) { SetError("Archives are read-only. Entries cannot be deleted."); return; }
        var selected = SelectedOperationEntries();
        if (selected.Length == 0) return;
        ResetQuickJump();
        var request = new FileOperationRequest(permanently ? FileOperationKind.Delete : FileOperationKind.Recycle, selected);
        var dialog = new FileDeleteDialog(request, Feedback) { Background = Background };
        OperationDialog = dialog;
        AttachOperationDialog(dialog);
        dialog.Confirmed += kind => { dialog.Close(); SetError(null); Operations.Enqueue(request with { Kind = kind }); };
        dialog.Closed += (_, _) => { OperationDialog = null; RecordActivity(); ActivePanel.FocusCurrent(scrollIntoView: false); };
        _ = dialog.ShowDialog(this);
    }

    private async Task<ConflictDecision> ResolveFileConflictAsync(FileConflict conflict, CancellationToken cancellationToken)
    {
        while (HasOpenDialog || Feedback.IsActive || IsMenuOpen || Left.EditingRow != null || Right.EditingRow != null || CommandLine.HasKeyboardFocus)
            await Task.Delay(80, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<ConflictDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new FileConflictDialog(conflict, Feedback) { Background = Background };
        OperationDialog = dialog;
        AttachOperationDialog(dialog);
        dialog.Decided += decision => { completion.TrySetResult(decision); dialog.Close(); };
        dialog.Closed += (_, _) =>
        {
            OperationDialog = null;
            completion.TrySetResult(new(ConflictAction.Cancel));
            RecordActivity(); ActivePanel.FocusCurrent(scrollIntoView: false);
        };
        using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(() => dialog.Close()));
        _ = dialog.ShowDialog(this);
        return await completion.Task.WaitAsync(cancellationToken);
    }

    private void AttachOperationDialog(Window dialog)
    {
        Feedback.Attach(dialog);
        dialog.AddHandler(KeyUpEvent, (_, e) => launchKeysHeld.Remove(e.Key), RoutingStrategies.Tunnel);
    }

    private async Task RefreshOperationPanelsAsync()
    {
        foreach (var panel in new[] { Left, Right })
        {
            bool CanApply() => panel.EditingRow == null && !HasOpenDialog && !Feedback.IsActive && !IsNameSearchActive;
            if (CanApply()) await panel.RefreshAsync(CanApply, () => ActivePanel == panel && panel.Table.IsKeyboardFocusWithin);
        }
    }
}

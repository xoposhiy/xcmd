// @implements TransferEntries, DuplicateEntry, DeleteEntries, QueueOperations, ExtractEntries
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xcmd.Core;
using Xcmd.Desktop.Features.FileOperations;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Workspace;

public sealed partial class CommanderWindow
{
    private readonly IFileOperationStore operationStore;
    private readonly FileOperationDecisions decisions;
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
        if (kind != FileOperationKind.Copy && selected.FirstOrDefault(entry => ArchivePath.IsEntry(entry.FullPath)) is { } virtualSource)
        { SetError(ArchivePath.ReadOnlyMessage(virtualSource.FullPath, "Archives are read-only. Use F5 to copy files out.")); return; }
        ResetQuickJump();
        SetError(null);
        var sourceDirectory = ActivePanel.OperationDirectory;
        if (!samePanel && (OppositePanel.IsSearchResults || OppositePanel.HasHistory)) { SetError("Open a destination folder in the other panel first (Escape leaves search results or Git history)."); return; }
        var targetDirectory = samePanel ? sourceDirectory : OppositePanel.Session.DirectoryPath;
        if (ArchivePath.IsEntry(targetDirectory)) { SetError(ArchivePath.ReadOnlyMessage(targetDirectory, "Archives are read-only. Choose a regular destination folder.")); return; }
        decisions.ShowTransfer(kind, selected, sourceDirectory, targetDirectory, samePanel);
    }

    private void DeleteSelected(bool permanently)
    {
        if (HasOpenDialog) return;
        if (ArchivePath.IsEntry(ActivePanel.Session.DirectoryPath)) { SetError(ArchivePath.ReadOnlyMessage(ActivePanel.Session.DirectoryPath, "Archives are read-only. Entries cannot be deleted.")); return; }
        var selected = SelectedOperationEntries();
        if (selected.Length == 0) return;
        if (selected.FirstOrDefault(entry => ArchivePath.IsEntry(entry.FullPath)) is { } virtualEntry) { SetError(ArchivePath.ReadOnlyMessage(virtualEntry.FullPath, "Archives are read-only. Entries cannot be deleted.")); return; }
        ResetQuickJump();
        var request = new FileOperationRequest(permanently ? FileOperationKind.Delete : FileOperationKind.Recycle, selected);
        decisions.ShowDelete(request);
    }

    private void FocusAfterOperationDialog() => ActivePanel.FocusCurrent(scrollIntoView: false);

    private bool CanShowOperationDecision() => !HasOpenDialog && !Feedback.IsActive && !IsMenuOpen
        && Left.EditingRow == null && Right.EditingRow == null && !CommandLine.HasKeyboardFocus;

    private void TrackOperationDialog(Window? dialog)
    {
        OperationDialog = dialog;
        if (dialog == null) return;
        Feedback.Attach(dialog);
        dialog.AddHandler(KeyUpEvent, (_, e) => launchKeysHeld.Remove(e.Key), RoutingStrategies.Tunnel);
    }

    private void AttachOperationEvents()
    {
        Operations.Finished += job =>
        {
            Execute(RefreshOperationPanelsAsync);
            var failure = job.Outcomes.FirstOrDefault(result => result.Kind == FileOutcomeKind.Failed);
            if (failure != null) SetError(Path.GetFileName(failure.Source) + ": " + failure.Message);
            else status.Text = job.Request.Kind == FileOperationKind.CreateZip && job.State == FileOperationState.Completed
                ? $"ZIP created: {job.Request.NewName} · {job.CompletedFiles:N0} files"
                : $"{job.Request.Kind}: {job.ProcessedFiles:N0}/{job.TotalFiles:N0} files · {job.SkippedFiles:N0} skipped · {job.State}";
        };
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

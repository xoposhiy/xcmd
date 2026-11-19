// @implements CreateArchive
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.FileOperations;

internal sealed class ZipPackingController(OperationDialogHost host, IFileOperationStore store, FileOperationQueue operations,
    Action<Func<Task>> execute, Func<bool> hasOpenDialog)
{
    public void Open(PanelView panel, FileEntry[] sources)
    {
        if (hasOpenDialog() || sources.Length == 0) return;
        var directory = Path.GetDirectoryName(sources[0].FullPath)!;
        var stem = sources.Length == 1 ? sources[0].IsDirectory || sources[0].Name.LastIndexOf('.') <= 0
            ? sources[0].Name : Path.GetFileNameWithoutExtension(sources[0].Name) : Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
        var dialog = new ZipNameDialog(directory, sources.Length, (string.IsNullOrEmpty(stem) ? "Archive" : stem) + ".zip", host.Feedback)
        { Background = host.Window.Background }.WithFeedbackId("ZipPackingController.dialog");
        host.Track(dialog);
        var submitting = false;
        dialog.Submitted += text => execute(async () =>
        {
            if (submitting) return;
            submitting = true;
            try
            {
                if (PanelRules.ValidateName(text) is { } error) throw new ArgumentException(error);
                var name = text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? text : text + ".zip";
                var request = new FileOperationRequest(FileOperationKind.CreateZip, sources, directory, name);
                FileOperationRules.ValidateRequest(request);
                if (await store.InspectAsync(Path.Combine(directory, name), CancellationToken.None) != null)
                    throw new IOException("An item with this name already exists. Choose another ZIP name.");
                if (!dialog.IsVisible) return;
                dialog.Close(); operations.Enqueue(request);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
            { dialog.Error.Text = failure.Message; }
            finally { submitting = false; }
        });
        dialog.Closed += (_, _) => { host.Track(null); host.RecordActivity(); panel.FocusCurrent(scrollIntoView: false); };
        _ = dialog.ShowDialog(host.Window);
    }
}

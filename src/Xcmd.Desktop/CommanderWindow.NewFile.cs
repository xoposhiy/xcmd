using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    private void OpenCreateFileDialog()
    {
        if (HasOpenDialog) return;
        var panel = ActivePanel;
        var directory = panel.Session.DirectoryPath;
        if (panel.IsSearchResults || ArchivePath.IsEntry(directory))
        { SetError("Open a regular destination folder before creating a file."); return; }
        ResetQuickJump();
        var dialog = new NameDialog(Feedback, file: true) { Background = Background };
        Feedback.Attach(dialog);
        ActiveDialog = dialog;
        var submitting = false;
        dialog.Submitted += name => Execute(async () =>
        {
            if (submitting) return;
            if (PanelRules.ValidateName(name) is { } error) { dialog.Error.Text = error; return; }
            submitting = true;
            try
            {
                var launch = await viewing.CreateFileAsync(Path.Combine(directory, name), directory);
                dialog.Close();
                if (panel.Session.DirectoryPath == directory) await panel.LoadAsync(directory, selectName: name);
                await LaunchExternalAsync(launch);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
            { if (dialog.IsVisible) dialog.Error.Text = failure.Message; else SetError(failure.Message); }
            finally { submitting = false; }
        });
        dialog.Closed += (_, _) => { ActiveDialog = null; RecordActivity(); panel.FocusCurrent(); };
        _ = dialog.ShowDialog(this);
        dialog.Editor.Focus();
    }
}

// @implements RenameEntry, RecoverOperations
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class PanelRename(TableView table, IFileSystem fileSystem, Func<FileRow?> current, Func<FileEntry, string, Task> applyRename)
{
    public FileRow? EditingRow { get; private set; }
    public bool IsCommitting { get; private set; }
    public TextBox? Editor => table.GetVisualDescendants().OfType<TextBox>()
        .FirstOrDefault(editor => editor.Name == "RenameEditor" && editor.IsVisible && editor.DataContext == EditingRow);

    public void Begin()
    {
        if (current() is not { Entry.IsParent: false } row) return;
        Cancel();
        EditingRow = row;
        row.DraftName = row.Entry.Name;
        row.IsEditing = true;
        table.ScrollIntoView(row);
        table.UpdateLayout();
        FocusEditor();
        Dispatcher.UIThread.Post(FocusEditor, DispatcherPriority.Input);
    }

    public void FocusEditor()
    {
        if (Editor is not { } editor) return;
        editor.Focus();
        editor.SelectAll();
    }

    public void CancelOnFocusChange(FileRow row)
    {
        if (EditingRow != null && row != EditingRow && !IsCommitting) Cancel();
    }

    public void Cancel()
    {
        if (EditingRow == null) return;
        EditingRow.IsEditing = false;
        EditingRow = null;
    }

    public async Task<FileOperationError?> CommitAsync()
    {
        if (IsCommitting || EditingRow is not { } row) return null;
        var name = row.DraftName;
        var validation = PanelRules.ValidateName(name);
        if (validation != null) return new(row.Entry, null, validation);
        IsCommitting = true;
        var editor = Editor;
        if (editor != null) editor.IsReadOnly = true;
        try
        {
            await fileSystem.RenameAsync(row.Entry, name);
            Cancel();
            await applyRename(row.Entry, name);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return FileOperationError.FromException(row.Entry, null, error);
        }
        finally
        {
            IsCommitting = false;
            if (editor != null) editor.IsReadOnly = false;
        }
    }
}

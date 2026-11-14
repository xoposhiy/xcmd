using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class FileViewerWindow
{
    private readonly ViewerDocument? document;
    public bool IsDocumentReadOnly => document?.IsReadOnly == true;
    public string DocumentCaption => document?.Caption ?? CurrentPath;

    private bool AllowExternalAction()
    {
        if (!IsDocumentReadOnly) return true;
        ShowError("This Git view is read-only. Open the working file in a regular folder to edit it.");
        return false;
    }

    private void UpdateDocumentTitle()
    {
        Title = DocumentCaption + " — Xcmd Viewer";
        titlePath.Text = document?.Caption ?? Path.GetFileName(CurrentPath);
        Avalonia.Controls.ToolTip.SetTip(titlePath, DocumentCaption);
    }
}

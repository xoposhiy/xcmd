// @implements OpenViewers, QuickViewFile, OpenViewedFile, BrowseSnapshot
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

public sealed partial class FileViewer
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
        titlePath.Text = !HasFile ? "Quick view" : document?.Caption ?? Path.GetFileName(CurrentPath);
        if (IsArchiveReport) titlePath.Text += " · Archive contents";
        CaptionChanged?.Invoke(DocumentCaption + (IsArchiveReport ? " — Archive contents" : ""));
        var tip = IsEmbedded ? "Ctrl+Tab switches focus between the viewer and the file list." : "";
        Avalonia.Controls.ToolTip.SetTip(titlePath, HasFile ? (IsEmbedded ? DocumentCaption + "\n" + tip : DocumentCaption) : tip);
    }
}

// @implements ReadablePanels, ViewSelectionSize, BrowseHistory, SearchResultPanel, BrowseArchives
using Avalonia.Controls;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels.Git;

namespace Xcmd.Desktop.Features.Panels;

internal sealed class PanelFooter(TextBlock summary, GitLogPresentation logPresentation)
{
    public void Update(PanelSession session, IReadOnlyList<FileRow> rows, IReadOnlyCollection<FileRow> markedRows, bool searchResults)
    {
        if (GitPath.IsEntry(session.DirectoryPath))
        {
            summary.Text = session.Error ?? logPresentation.Footer(session.DirectoryPath, rows, markedRows.Count);
            ToolTip.SetTip(summary, summary.Text);
            return;
        }
        var prefix = LocationPrefix(session.DirectoryPath, searchResults);
        var count = rows.Count(row => !row.Entry.IsParent);
        var itemCount = count == 1 ? "1 item" : $"{count:N0} items";
        var text = session.Error ?? prefix + itemCount;
        string? tip = null;
        if (session.Error == null && markedRows.Count > 0)
        {
            var bytes = markedRows.Sum(row => row.Entry.IsDirectory ? row.Measurement?.Size.Bytes ?? 0 : row.Entry.Size);
            var bound = markedRows.Any(IsIncompleteDirectory) ? "≥ " : "";
            text += $"    {markedRows.Count:N0} selected · {bound}{FileSizeText.Format(bytes)}";
            tip = $"Selected: {bound}{bytes:N0} bytes";
        }
        summary.Text = text;
        ToolTip.SetTip(summary, tip);
    }

    private static bool IsIncompleteDirectory(FileRow row) => row.Entry.IsDirectory && (row.Measurement == null || row.Measurement.Size.Incomplete);

    private static string LocationPrefix(string path, bool searchResults)
    {
        if (searchResults) return "Search results · Esc returns · Ctrl+←/→ locates · ";
        return ArchivePath.IsEntry(path) ? "Archive (read-only) · " : "";
    }
}

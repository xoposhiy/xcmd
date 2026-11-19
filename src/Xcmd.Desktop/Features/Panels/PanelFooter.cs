// @implements ReadablePanels, ViewSelectionSize, BrowseHistory, SearchResultPanel, BrowseArchives
using Avalonia.Controls;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels.Git;

namespace Xcmd.Desktop.Features.Panels;

internal sealed class PanelFooter(TextBlock summary, GitLogPresentation logPresentation)
{
    public void Update(PanelSession session, IReadOnlyList<FileRow> rows, string[] selectedNames, bool searchResults)
    {
        var selected = selectedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (GitPath.IsEntry(session.DirectoryPath))
        {
            summary.Text = session.Error ?? logPresentation.Footer(session.DirectoryPath, rows, selected.Count);
            ToolTip.SetTip(summary, summary.Text);
            return;
        }
        var selectedRows = rows.Where(row => selected.Contains(row.Name) && !row.Entry.IsParent).ToArray();
        var selectedBytes = selectedRows.Sum(row => row.Entry.IsDirectory ? row.Measurement?.Size.Bytes ?? 0 : row.Entry.Size);
        var incomplete = selectedRows.Any(IsIncompleteDirectory);
        var total = (incomplete ? "≥ " : "") + FileSizeText.Format(selectedBytes) + IncompleteNote(incomplete);
        var prefix = LocationPrefix(session.DirectoryPath, searchResults);
        summary.Text = session.Error ?? $"{prefix}{rows.Count(row => !row.Entry.IsParent):N0} items    {selected.Count:N0} selected · {total}";
        ToolTip.SetTip(summary, session.Error == null ? $"Selected: {selectedBytes:N0} bytes{IncompleteNote(incomplete)}" : null);
    }

    private static bool IsIncompleteDirectory(FileRow row) => row.Entry.IsDirectory && (row.Measurement == null || row.Measurement.Size.Incomplete);
    private static string IncompleteNote(bool incomplete) => incomplete ? " (incomplete)" : "";

    private static string LocationPrefix(string path, bool searchResults)
    {
        if (searchResults) return "Search results · Esc returns · Ctrl+←/→ locates · ";
        return ArchivePath.IsEntry(path) ? "Archive (read-only) · " : "";
    }
}

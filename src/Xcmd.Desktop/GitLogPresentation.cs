// @implements BrowseHistory, BrowseSnapshot, InspectCommitChanges, InspectWorkingChanges
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal sealed class GitLogPresentation(TableView table)
{
    private sealed record ColumnState(object? Header, IDataTemplate? Template, GridLength Width, double MinWidth, double MaxWidth, HorizontalAlignment Alignment);
    private ColumnState[]? fileColumns;
    private GitFileSystem? files;
    public bool IsLog { get; private set; }

    public void Update(PanelSession session)
    {
        files = session.FileSystem as GitFileSystem;
        var log = GitPath.IsLog(session.DirectoryPath);
        if (log == IsLog) return;
        IsLog = log;
        if (log)
        {
            fileColumns = table.Columns.Select(column => new ColumnState(column.Header, column.CellTemplate, column.Width,
                column.MinWidth, column.MaxWidth, column.HorizontalContentAlignment)).ToArray();
            SetColumn(0, "Commit", 104, 104);
            SetColumn(1, "Subject", 0, 90);
            SetColumn(2, "Author", 110, 85);
            SetColumn(4, "Date", 145, 130);
            table.Columns[^1].CanUserResize = false;
            table.Columns[3].IsVisible = false;
        }
        else if (fileColumns != null)
        {
            table.Columns[^1].CanUserResize = true;
            for (var index = 0; index < table.Columns.Count; index++)
            {
                var column = table.Columns[index]; var saved = fileColumns[index];
                column.Header = saved.Header; column.CellTemplate = saved.Template;
                column.Width = saved.Width; column.MinWidth = saved.MinWidth; column.MaxWidth = saved.MaxWidth;
                column.HorizontalContentAlignment = saved.Alignment;
            }
        }
    }

    private void SetColumn(int index, string header, double width, double minimum)
    {
        var column = table.Columns[index];
        column.Header = new TextBlock
        {
            Text = header,
            Margin = new Thickness(8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
            FontSize = UiPalette.BodySize
        }.WithFeedbackId("GitLogPresentation.header");
        column.MinWidth = minimum; column.MaxWidth = double.PositiveInfinity;
        column.Width = width == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width);
        column.HorizontalContentAlignment = HorizontalAlignment.Left;
        column.CellTemplate = new FuncDataTemplate<FileRow>((row, _) =>
        {
            var text = new TextBlock
            {
                Text = row == null ? "" : CellText(row, index),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            }.WithFeedbackId("GitLogPresentation.cell");
            if (index == 0) text.FontFamily = new FontFamily("Consolas");
            var commitId = index == 0 && row is { Entry.IsParent: false } && GitPath.HasRevision(row.Entry.FullPath) ? files!.Commit(row.Entry.FullPath).Id : null;
            ToolTip.SetTip(text, commitId ?? text.Text);
            return text;
        });
    }

    private string CellText(FileRow row, int index)
    {
        if (row.Entry.IsParent) return index == 1 ? ".." : "";
        if (!GitPath.IsEntry(row.Entry.FullPath)) return index == 1 ? row.Name : "";
        var location = GitPath.Parse(row.Entry.FullPath);
        if (location.Kind != "tree") return index == 1 ? row.Name : index == 2 && location.Kind == "working" ? "Uncommitted" : "";
        var commit = files!.Commit(row.Entry.FullPath);
        return index switch
        {
            0 => commit.ShortId,
            1 => commit.Subject,
            2 => commit.Author,
            _ => commit.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        };
    }

    public void Adapt()
    {
        table.Columns[0].IsVisible = true; table.Columns[1].IsVisible = true;
        table.Columns[2].IsVisible = table.Bounds.Width >= 340;
        table.Columns[3].IsVisible = false;
        table.Columns[4].IsVisible = table.Bounds.Width >= 500;
    }

    public bool ResetWidths()
    {
        if (!IsLog) return false;
        table.Columns[0].Width = new GridLength(104);
        table.Columns[1].Width = new GridLength(1, GridUnitType.Star);
        table.Columns[2].Width = new GridLength(110);
        table.Columns[4].Width = new GridLength(145);
        return true;
    }

    public string Footer(string directory, IReadOnlyList<FileRow> rows, int selected)
    {
        var location = GitPath.Parse(directory);
        var count = rows.Count(row => !row.Entry.IsParent);
        if (location.Kind == "log")
        {
            var commits = rows.Count(row => !row.Entry.IsParent && GitPath.HasRevision(row.Entry.FullPath));
            return (commits == 0 ? files!.EmptyLogMessage(directory) : $"{commits:N0} commits loaded · {selected:N0} selected · newest first")!;
        }
        if (location.Kind == "working") return $"{count:N0} changed files · Refreshed {files!.RefreshedAt(directory):HH:mm:ss} · Ctrl+R refresh";
        return (location.Kind == "changes" ? "Changes · /" + location.RelativePath : "Snapshot") + $" · {count:N0} items · {selected:N0} selected · vs first parent";
    }
}

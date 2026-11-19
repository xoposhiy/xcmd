// @implements SeeRepositoryState, BrowseSnapshot
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.Panels.Git;

internal sealed class GitPanelRows
{
    private const string LiveLegend = "Left: staged · Right: working tree\n✓ staged · ● modified or mixed changes · + added · ? untracked · → renamed/copied · red − deleted · ! conflict\nGray − ignored · ! on the right: tracked despite an ignore rule\nClean files are blank. A green ✓ on the right marks a clean repository. Folder statuses include descendants.";
    private static string Legend(string directory) => GitPath.HasRevision(directory)
        ? "Changes in this commit compared with its first parent.\n● modified · + added · → renamed/copied · − deleted\nBlank cells: unchanged. Folder indicators include descendants."
        : LiveLegend;
    private GitStatus? projectedStatus;
    private GitStatusProjection projection = new(null);

    public TableViewColumn Column { get; } = CreateColumn();

    public bool Update(IReadOnlyList<FileRow> rows, GitStatusView git, string directory, bool searchResults)
    {
        var showColumn = Apply(rows, git, directory, searchResults);
        ToolTip.SetTip((Control)Column.Header!, Legend(directory));
        return showColumn;
    }

    private bool Apply(IReadOnlyList<FileRow> rows, GitStatusView git, string directory, bool searchResults)
    {
        if (GitPath.IsEntry(directory))
        {
            var commit = GitPath.HasRevision(directory);
            foreach (var row in rows) row.SetGitStatus(row.Entry.GitStatus is { } status
                ? GitRowStatus.FromEntry(status, commit) : GitRowStatus.Empty);
            return true;
        }
        if (!ReferenceEquals(projectedStatus, git.Status))
        {
            projectedStatus = git.Status;
            projection = new(projectedStatus);
        }
        var showColumn = !searchResults && !ArchivePath.IsEntry(directory) && directory.Length > 0;
        var repositories = git.ChildRepositories.Where(status => status.Root != null)
            .ToDictionary(status => status.Root!, status => new GitStatusProjection(status).For(status.Root!) with { Repository = status }, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var status = GitRowStatus.Empty;
            if (showColumn && !row.Entry.IsParent)
                status = projectedStatus == null || projection.Contains(row.Entry.FullPath)
                    ? repositories.GetValueOrDefault(row.Entry.FullPath) ?? projection.For(row.Entry.FullPath)
                    : projection.For(row.Entry.FullPath);
            row.SetGitStatus(status);
        }
        return showColumn;
    }

    private static TableViewColumn CreateColumn()
    {
        var header = new TextBlock
        {
            Text = "Git",
            FontSize = UiPalette.BodySize,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }.WithFeedbackId("PanelView.gitHeader");
        ToolTip.SetTip(header, LiveLegend);
        return new TableViewColumn
        {
            Header = header,
            Width = new GridLength(56),
            MinWidth = 52,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CellTemplate = new FuncDataTemplate<FileRow>((row, _) => GitCell(row))
        };
    }

    private static Control GitCell(FileRow? row)
    {
        var cell = new Grid { DataContext = row, ColumnDefinitions = new ColumnDefinitions("18,18"), VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("PanelView.gitCell");
        cell.Bind(ToolTip.TipProperty, new Binding("Git.Description"));
        var staged = new TextBlock { FontFamily = new FontFamily("Consolas"), FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center }.WithFeedbackId("PanelView.gitStaged");
        staged.Bind(TextBlock.TextProperty, new Binding("Git.IndexGlyph"));
        staged.Bind(TextBlock.ForegroundProperty, new Binding("Git.IndexBrush"));
        var working = new TextBlock { FontFamily = new FontFamily("Consolas"), FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center }.WithFeedbackId("PanelView.gitWorking");
        working.Bind(TextBlock.TextProperty, new Binding("Git.WorkingGlyph"));
        working.Bind(TextBlock.ForegroundProperty, new Binding("Git.WorkingBrush"));
        cell.Children.Add(staged); Grid.SetColumn(working, 1); cell.Children.Add(working);
        return cell;
    }
}

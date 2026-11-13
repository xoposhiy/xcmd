using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private const int GitColumnIndex = 3;
    private GitStatus? projectedGitStatus;
    private GitStatusProjection gitProjection = new(null);
    private bool showGitColumn;

    private void ApplyGitStatus()
    {
        if (!ReferenceEquals(projectedGitStatus, Git.Status))
        {
            projectedGitStatus = Git.Status;
            gitProjection = new(projectedGitStatus);
        }
        showGitColumn = !IsSearchResults && !ArchivePath.IsEntry(Session.DirectoryPath) && Session.DirectoryPath.Length > 0 && gitProjection.Contains(Session.DirectoryPath);
        foreach (var row in Rows)
        {
            var contained = showGitColumn && !row.Entry.IsParent && gitProjection.Contains(row.Entry.FullPath);
            row.SetGitStatus(contained ? gitProjection.For(row.Entry.FullPath) : GitRowStatus.Empty);
        }
        QueueColumnAdaptation();
    }

    private static TableViewColumn MakeGitColumn()
    {
        var header = new TextBlock
        {
            Text = "Git",
            FontSize = UiPalette.BodySize,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }.WithFeedbackId("PanelView.gitHeader");
        ToolTip.SetTip(header, "Left: staged · Right: working tree\n✓ staged · ● modified · + added · ? untracked · → renamed/copied · − deleted · ! conflict\nClean items are blank. Folder statuses include descendants.");
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

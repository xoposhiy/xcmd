using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private const int GitColumnIndex = 3;
    private readonly GitPanelRows gitRows = new();
    private bool showGitColumn;

    private void ApplyGitStatus()
    {
        showGitColumn = gitRows.Apply(Rows, Git, Session.DirectoryPath, IsSearchResults);
        ToolTip.SetTip((Control)Table.Columns[GitColumnIndex].Header!, gitRows.Legend(Session.DirectoryPath));
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
        ToolTip.SetTip(header, GitPanelRows.LiveLegend);
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

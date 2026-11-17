// @implements QueueOperations
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

internal sealed class OperationDetailsView : Grid
{
    private readonly TextBlock summary = new TextBlock() { FontSize = UiPalette.MetaSize, TextTrimming = TextTrimming.CharacterEllipsis }.WithFeedbackId("OperationDetailsView.summary");
    private readonly TableView outcomes = new TableView() { Name = "OperationOutcomes", SelectionMode = SelectionMode.Single, IsTextSearchEnabled = false }.WithFeedbackId("OperationDetailsView.outcomes");
    private FileOperationJob? shown;
    private int outcomeCount = -1;

    public OperationDetailsView()
    {
        RowDefinitions = new RowDefinitions("Auto,*");
        RowSpacing = 4; Margin = new Thickness(8, 0, 0, 0);
        UiLayout.Table(outcomes);
        AutomationProperties.SetAutomationId(outcomes, "file-operations.outcomes");
        outcomes.Columns.Add(SearchTables.Column<FileOutcome>("Result", new(100), 70, item => Cell(item?.Kind.ToString())));
        outcomes.Columns.Add(SearchTables.Column<FileOutcome>("Source", new(2, GridUnitType.Star), 90, item => Cell(item?.Source)));
        outcomes.Columns.Add(SearchTables.Column<FileOutcome>("Destination", new(2, GridUnitType.Star), 90, item => Cell(item?.Destination)));
        outcomes.Columns.Add(SearchTables.Column<FileOutcome>("Explanation", new(2, GridUnitType.Star), 90, item => Cell(item?.Message)));
        Children.Add(summary); Grid.SetRow(outcomes, 1); Children.Add(outcomes);
    }

    public void Show(FileOperationJob? job)
    {
        summary.Text = job == null ? "" : $"{job.ProcessedFiles:N0}/{job.TotalFiles:N0} files · {job.CompletedFiles:N0} completed · {job.SkippedFiles:N0} skipped · {job.FailedEntries:N0} failed";
        if (job == shown && job?.Outcomes.Count == outcomeCount) return;
        shown = job; outcomeCount = job?.Outcomes.Count ?? -1;
        outcomes.ItemsSource = job?.Outcomes.TakeLast(100).ToArray();
        ToolTip.SetTip(summary, job == null ? null : "Source: " + string.Join(", ", job.Request.Sources.Select(entry => entry.FullPath)) +
            "\nDestination: " + (job.Request.Destination ?? (job.Request.Kind == FileOperationKind.Recycle ? "Recycle Bin" : "Permanent deletion")));
    }

    private static TextBlock Cell(string? value)
    {
        var text = SearchTables.Text(value);
        ToolTip.SetTip(text, value);
        return text;
    }
}

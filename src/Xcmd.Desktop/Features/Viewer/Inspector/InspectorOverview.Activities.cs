// @implements InspectAgentSession
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class InspectorOverview
{
    private enum ActivityColumn { ToolTime = 1, Cost, Calls }
    private static readonly (ActivityColumn Column, string Title)[] ActivityColumns =
        [(ActivityColumn.ToolTime, "Tool time"), (ActivityColumn.Cost, "API cost"), (ActivityColumn.Calls, "Tool calls")];
    private ActivityColumn sortActivityBy = ActivityColumn.Cost;
    private bool descendingActivities = true;
    private readonly Dictionary<ActivityColumn, Button> activityHeaders = [];
    private readonly TextBlock activityLabel = Text("Target/action", 12, UiPalette.Secondary);
    private readonly StackPanel activityRows = Stack();
    private readonly CheckBox groupActions = new CheckBox
    {
        Content = "Combine actions by target",
        IsChecked = true,
        FontSize = 12,
        Margin = new Thickness(9, 0, 6, 2)
    }.WithFeedbackId("SessionInspector.groupActions");

    private static Grid ActivityGrid()
    {
        var row = Grid("Auto,Auto,Auto,Auto");
        row.ColumnSpacing = 6;
        row.MinHeight = 26;
        for (var i = 0; i < row.ColumnDefinitions.Count; i++) row.ColumnDefinitions[i].SharedSizeGroup = $"ActivityColumn{i}";
        var separator = new Border { Width = 1, Background = GridLine, HorizontalAlignment = HorizontalAlignment.Left }
            .WithFeedbackId("SessionInspector.activityCallsSeparator");
        Put(row, separator, (int)ActivityColumn.Calls);
        return row;
    }

    private void BuildActivityPanel()
    {
        activities.Children.Add(SectionTitle("Activity breakdown", null));
        activities.Children.Add(groupActions);
        groupActions.IsCheckedChanged += (_, _) =>
        {
            summary = selection.Summarize(groupActions.IsChecked == true);
            RenderActivities();
        };
        var header = ActivityGrid();
        header.Background = Subtle;
        activityLabel.Margin = new Thickness(11, 2, 9, 2);
        Put(header, activityLabel);
        foreach (var (column, title) in ActivityColumns)
        {
            var button = Action(title, () =>
            {
                descendingActivities = sortActivityBy != column || !descendingActivities;
                sortActivityBy = column;
                RenderActivities();
            }, "Sort by " + title);
            button.FontSize = 12;
            button.MinHeight = 24;
            button.Padding = new Thickness(3, 1);
            button.Margin = new Thickness(0, 0, 6, 0);
            button.HorizontalAlignment = HorizontalAlignment.Right;
            button.Classes.Add("quiet");
            activityHeaders.Add(column, button);
            Put(header, button, (int)column);
        }
        activities.Children.Add(header);
        activities.Children.Add(activityRows);
    }

    private void RenderActivities()
    {
        activityLabel.Text = groupActions.IsChecked == true ? "Target" : "Target/action";
        foreach (var (column, title) in ActivityColumns)
        {
            var active = sortActivityBy == column;
            activityHeaders[column].Content = title + (active ? descendingActivities ? " ↓" : " ↑" : "");
            AutomationProperties.SetName(activityHeaders[column], "Sort by " + title + (active ? descendingActivities ? ", descending" : ", ascending" : ""));
        }
        decimal Metric(ActivityTotal row) => sortActivityBy switch
        {
            ActivityColumn.ToolTime => row.Milliseconds,
            ActivityColumn.Calls => row.Calls,
            _ => row.Cost
        };
        var rows = descendingActivities ? summary.Activities.OrderByDescending(Metric) : summary.Activities.OrderBy(Metric);
        var total = summary.Activities.Sum(AxisAmount);
        activityRows.Children.Clear();
        foreach (var activity in rows)
            activityRows.Children.Add(TableRow(activity, total > 0 ? (double)(AxisAmount(activity) / total) : 0));
        if (summary.Activities.Count == 0) activityRows.Children.Add(Note("No tool activity in this range."));
        else if (summary.Results.Count == 0) activityRows.Children.Add(Note("No tool calls finished in this range."));
    }
}

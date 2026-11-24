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
    private ActivityColumn sortActivityBy = ActivityColumn.Cost;
    private bool descendingActivities = true;
    private readonly Dictionary<ActivityColumn, (Button Button, string Title)> activityHeaders = [];
    private readonly TextBlock activityLabel = Text("SessionInspector.activityTargetHeading", "Target/action", 12, UiPalette.Secondary);
    private readonly StackPanel activityRows = Stack("SessionInspector.activityRows");
    private readonly CheckBox groupActions = new CheckBox
    {
        Content = "Combine actions by target",
        IsChecked = true,
        FontSize = 12,
        Margin = new Thickness(9, 0, 6, 2)
    }.WithFeedbackId("SessionInspector.groupActions");

    private static Grid ActivityGrid([FeedbackId] string id)
    {
        var row = Grid(id, "Auto,Auto,Auto,Auto");
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
        activities.Children.Add(SectionTitle("SessionInspector.activityPanelHeader",
            Text("SessionInspector.activityPanelTitle", "Activity breakdown", 16, strong: true)));
        activities.Children.Add(groupActions);
        groupActions.IsCheckedChanged += (_, _) =>
        {
            summary = selection.Summarize(groupActions.IsChecked == true);
            RenderActivities();
        };
        var header = ActivityGrid("SessionInspector.activityColumnHeadings");
        header.Background = Subtle;
        activityLabel.Margin = new Thickness(11, 2, 9, 2);
        Put(header, activityLabel);
        AddSortHeader("SessionInspector.sortActivityByToolTime", ActivityColumn.ToolTime, "Tool time");
        AddSortHeader("SessionInspector.sortActivityByCost", ActivityColumn.Cost, "API cost");
        AddSortHeader("SessionInspector.sortActivityByCallCount", ActivityColumn.Calls, "Tool calls");
        activities.Children.Add(header);
        activities.Children.Add(activityRows);

        void AddSortHeader([FeedbackId] string id, ActivityColumn column, string title)
        {
            var button = Action(id, title, () =>
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
            activityHeaders.Add(column, (button, title));
            Put(header, button, (int)column);
        }
    }

    private void RenderActivities()
    {
        activityLabel.Text = groupActions.IsChecked == true ? "Target" : "Target/action";
        foreach (var (column, (button, title)) in activityHeaders)
        {
            var active = sortActivityBy == column;
            button.Content = title + (active ? descendingActivities ? " ↓" : " ↑" : "");
            AutomationProperties.SetName(button, "Sort by " + title + (active ? descendingActivities ? ", descending" : ", ascending" : ""));
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
        if (summary.Activities.Count == 0) activityRows.Children.Add(Note("SessionInspector.activityEmpty", "No tool activity in this range."));
        else if (summary.Results.Count == 0) activityRows.Children.Add(Note("SessionInspector.activityNoCompletedCalls", "No tool calls finished in this range."));
    }
}

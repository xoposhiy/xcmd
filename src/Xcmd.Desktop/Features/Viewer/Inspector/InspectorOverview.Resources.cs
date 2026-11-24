// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class InspectorOverview
{
    private const double ResourceRowHeight = 20;
    private readonly HashSet<string> collapsedGroups = [];
    private readonly StackPanel resourceRows = Stack();
    private readonly TextBlock resourceCount = Text("", 12, UiPalette.Secondary);
    private readonly Dictionary<ToolResource, SessionResourceActivity> visibleActivity = [];
    private readonly ResourceFilter resourceType = new("Type",
        new[] { "file", "directory", "url", "issue", "repository", "other" }
            .Select(key => (key, SessionResourceGroup.KindName(key))), initial: "file");
    private readonly ResourceFilter resourceTarget = new("Target",
        new[] { "source", "tests", "docs", "agent_instructions", "tracker", "git", "build_release", "project", "unknown" }
            .Select(key => (key, TimelineCaption.Name(key))));
    private readonly ResourceFilter resourceAction = new("Action", ResourceTimeline.Actions.Where(a => a.Action != "other"),
        hint: "Selections also include Other.");

    private void BuildResourcePanel()
    {
        Avalonia.Controls.Grid.SetIsSharedSizeScope(resourceRows, true);
        var heading = Grid("*,Auto");
        heading.Margin = new Thickness(11, 8);
        Put(heading, Text("Touched resources", 16, strong: true));
        Put(heading, resourceCount, 1);
        resources.Children.Add(heading);
        var filters = new WrapPanel { Margin = new Thickness(10, 0, 4, 2) }.WithFeedbackId("SessionInspector.resourceFilters");
        foreach (var filter in new[] { resourceType, resourceTarget, resourceAction })
        {
            filter.Button.Margin = new Thickness(0, 0, 6, 4);
            filters.Children.Add(filter.Button);
            filter.Changed += RenderResources;
        }
        resources.Children.Add(filters);
        resources.Children.Add(resourceRows);
    }

    private void RenderResources()
    {
        resourceRows.Children.Clear();
        visibleActivity.Clear();
        var visible = new List<TouchedResource>();
        foreach (var row in summary.Resources.Where(r => resourceType.Selected.Contains(r.Resource.Kind)))
        {
            var activity = SessionResourceActivity.For(selection.Trace, row, resourceTarget.Selected, resourceAction.Selected);
            if (activity.Intervals.Length == 0) continue;
            visibleActivity.Add(row.Resource, activity);
            visible.Add(row with { Calls = activity.Intervals.Select(interval => interval.Call).Distinct().ToArray() });
        }
        resourceCount.Text = $"{visible.Count:N0} of {summary.Resources.Count:N0}";
        var missing = summary.Results.Count(c => c.Classification == null);
        if (missing > 0)
            resourceRows.Children.Add(Note($"Resource lists unavailable for {missing:N0} of {summary.Results.Count:N0} calls in this range."));
        if (visible.Count == 0)
        {
            if (summary.Resources.Count > 0) resourceRows.Children.Add(Note("No resources match the selected filters."));
            else if (missing == 0) resourceRows.Children.Add(Note(summary.Results.Count == 0
                ? "No tool calls finished in this range." : "Luna identified no resources in this range."));
            return;
        }
        resourceRows.Children.Add(ResourceHeader());
        var root = SessionResourceGroup.Build(selection.Trace, projectName, visible);
        AddResourceChildren(root, resourceRows, 0);
    }

    private Control ResourceHeader()
    {
        var header = new ResourceRowGrid { MinHeight = ResourceRowHeight }.WithFeedbackId("SessionInspector.resourceHeader");
        header.Background = Subtle;
        header.Margin = new Thickness(8, 4, 8, 0);
        Put(header, Text("Resource", 12, UiPalette.Secondary));
        var calls = Text("Calls", 12, UiPalette.Secondary);
        calls.HorizontalAlignment = HorizontalAlignment.Right;
        ToolTip.SetTip(calls, "Completed tool calls for each resource");
        Put(header, calls, 1);
        var axis = Grid("*,Auto");
        var unit = selection.CostAxis ? "API cost" : "Time";
        Put(axis, Text($"{unit} {RangeLabel(true)}", 11, UiPalette.Secondary));
        Put(axis, Text(RangeLabel(false), 11, UiPalette.Secondary), 1);
        Put(header, axis, 2);
        return header;
    }

    private void AddResourceChildren(SessionResourceGroup parent, StackPanel list, int depth)
    {
        foreach (var group in parent.Children) AddResourceGroup(group, list, depth);
        foreach (var row in parent.Resources) list.Children.Add(ResourceButton(row, depth));
    }

    private void AddResourceGroup(SessionResourceGroup first, StackPanel list, int depth)
    {
        var (label, group) = first.CompactPath();
        if (group.Directory is { } leaf && !group.HasChildren)
        {
            list.Children.Add(ResourceButton(leaf, depth, label));
            return;
        }

        var children = Stack();
        var toggle = new Button
        {
            Margin = new Thickness(Indent(depth), 0, 0, 0),
            Padding = new Thickness(3, 0),
            FontSize = 13,
            MinHeight = ResourceRowHeight,
            Height = ResourceRowHeight,
            HorizontalAlignment = HorizontalAlignment.Left
        }.WithFeedbackId("SessionInspector.resourceGroup");
        toggle.Classes.Add("quiet");
        if (group.Directory is { } directory)
        {
            var header = Grid("*");
            Put(header, ResourceButton(directory, depth, label, 20));
            Put(header, toggle);
            list.Children.Add(header);
        }
        else
        {
            var heading = new ResourceRowGrid().WithFeedbackId("SessionInspector.resourceGroupRow");
            heading.Margin = new Thickness(8, 0);
            toggle.Margin = new Thickness(Indent(depth) - 8, 0, 0, 0);
            Put(heading, toggle);
            list.Children.Add(heading);
        }
        list.Children.Add(children);
        toggle.Click += (_, _) =>
        {
            if (!collapsedGroups.Remove(group.Key)) collapsedGroups.Add(group.Key);
            UpdateGroup();
        };
        UpdateGroup();

        void UpdateGroup()
        {
            var expanded = !collapsedGroups.Contains(group.Key);
            var arrow = expanded ? "▾" : "▸";
            toggle.Content = group.Directory == null ? arrow + "  " + label : arrow;
            var action = (expanded ? "Collapse " : "Expand ") + label;
            AutomationProperties.SetName(toggle, action);
            ToolTip.SetTip(toggle, action);
            if (expanded) AddResourceChildren(group, children, depth + 1);
            else children.Children.Clear();
        }
    }

    private static double Indent(int depth) => 8 + Math.Min(depth, 8) * 12;

    private Button ResourceButton(TouchedResource row, int depth, string? groupName = null, int toggleWidth = 0)
    {
        var value = row.Resource.Value;
        var name = row.Resource.Kind is "file" or "directory" ? value.Replace('\\', '/').TrimEnd('/').Split('/')[^1] : value;
        var activity = visibleActivity[row.Resource];
        var target = activity.Targets.FirstOrDefault() ?? "unknown";
        var title = Text(groupName ?? (name.Length == 0 ? value : name), 13, Color(target));
        title.Margin = new Thickness(Indent(depth) - 8 + toggleWidth, 0, 0, 0);
        var content = new ResourceRowGrid().WithFeedbackId("SessionInspector.resourceRow");
        Put(content, title);
        var calls = Text(Number(row.Calls.Count), 12, UiPalette.Secondary);
        calls.HorizontalAlignment = HorizontalAlignment.Right;
        DescribeNumber(calls, $"{calls.Text} completed tool calls");
        Put(content, calls, 1);
        Put(content, new ResourceTimeline(activity.Intervals, selection, responseFocus, value, ShowResourceResponse)
            .WithFeedbackId("SessionInspector.resourceTimeline"), 2);
        var tip = value + "  ·  " + string.Join(", ", activity.Targets);
        var button = Action("", () => ShowResource(row), tip);
        button.Content = content;
        button.HorizontalAlignment = button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Padding = new Thickness(8, 0);
        button.MinHeight = button.Height = ResourceRowHeight;
        button.Classes.Add("quiet");
        AutomationProperties.SetName(button, $"{value}, {row.Resource.Kind}, {target}, {row.Calls.Count:N0} calls");
        return button;
    }

    private void ShowResource(TouchedResource row)
    {
        var body = Grid("*", "Auto,Auto,*");
        Put(body, SessionInspectorWindow.Paragraph(row.Resource.Value));
        Put(body, SectionTitle("Tool calls", $"{row.Resource.Kind}   ·   {Number(row.Calls.Count)}"), 0, 1);
        Put(body, CallList(row.Calls), 0, 2);
        detail("Resource details", body);
    }

    private void ShowResourceResponse(ResponseHit hit)
    {
        ShowResponse(hit);
        selectedCalls.BringIntoView();
    }
}

internal sealed class ResourceRowGrid : Grid
{
    internal ResourceRowGrid()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*");
        ColumnDefinitions[0].SharedSizeGroup = "ResourceName";
        ColumnDefinitions[1].SharedSizeGroup = "ResourceCalls";
        ColumnSpacing = 8;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Leave room for the count and time axis even when a path fills the panel.
        ColumnDefinitions[0].MaxWidth = Math.Max(0, availableSize.Width - 150);
        return base.MeasureOverride(availableSize);
    }
}

// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class InspectorOverview : UserControl
{
    private readonly SessionSelection selection;
    private readonly Action<string, Control> detail;
    private readonly Action<TraceCall> viewToolCall;
    private readonly SessionTimeline timeline;
    private readonly StackPanel selectedCalls = Stack("SessionInspector.selectedCalls", 3);
    private readonly ComboBox scope = new ComboBox { MinWidth = 116, MaxWidth = 200, FontSize = 13 }.WithFeedbackId("SessionInspector.scope");
    private readonly Button timeAxis, costAxis, zoom, full, fromStart;
    private readonly TextBlock stamp = Text("SessionInspector.selectedRangeInterval", "", 14, Accent, true);
    private readonly TextBlock hint = Text("SessionInspector.timelineHint", "", 12, UiPalette.Secondary);
    private readonly TextBlock rangeTotals = Text("SessionInspector.rangeTotals", "", 12, UiPalette.Secondary);
    private readonly Button analyze;
    private readonly TextBlock analysisStatus = SessionInspectorWindow.Paragraph("SessionInspector.analysisStatus", "");
    private readonly StackPanel resources = Stack("SessionInspector.resourceContent");
    private readonly StackPanel activities = Stack("SessionInspector.activityContent");
    private readonly string projectName;
    private readonly bool hasClassifications;
    private readonly bool hasCalls;
    private readonly ResponseFocus responseFocus = new();
    private SessionSummary summary;

    internal InspectorOverview(AgentSession session, SessionTrace trace, Action<string, Control> detail,
        Action<TraceCall> viewToolCall, Action startAnalysis)
    {
        this.detail = detail;
        this.viewToolCall = viewToolCall;
        projectName = session.ProjectName;
        selection = new(trace);
        selection.SetAxis(true);
        summary = selection.Summarize();
        timeline = new SessionTimeline(selection, responseFocus, Refresh, Filter).WithFeedbackId("SessionInspector.timeline");
        timeline.EventSelected += SetTimelineHint;
        timeline.BlockSelected += ShowBlock;
        timeline.ResponseSelected += ShowResponse;
        timeAxis = AxisLink("SessionInspector.timeAxis", "Time", false);
        costAxis = AxisLink("SessionInspector.costAxis", "API cost", true, "Compare model responses at standard global API rates");
        zoom = Action("SessionInspector.zoomRange", "Zoom to range", () => { selection.ToggleZoom(); Refresh(); });
        full = Action("SessionInspector.fullSession", "Full session", () => Select(trace.Start, trace.End));
        fromStart = Action("SessionInspector.rangeFromStart", "From start", SelectFromStart);
        var content = Stack("SessionInspector.overviewContent", 12); content.Margin = new Thickness(18, 12, 18, 16);
        content.Children.Add(BuildInfo(session, trace));
        if (trace.Note != null) content.Children.Add(SessionInspectorWindow.Paragraph("SessionInspector.transcriptNote", trace.Note));
        var analysisAction = trace.ClassificationError != null ? "Retry analysis" :
            trace.HasOutdatedClassifications ? "Update with Luna" : "Analyze with Luna";
        analyze = Action("SessionInspector.analyzeCalls", analysisAction, startAnalysis);
        if (trace.HasOutdatedClassifications || trace.Calls.Any(c => c.Classification == null))
        {
            analyze.HorizontalAlignment = HorizontalAlignment.Left;
            content.Children.Add(analyze);
            content.Children.Add(analysisStatus);
            SetAnalysisStatus(trace.ClassificationError);
        }
        content.Children.Add(BuildTimeline());
        content.Children.Add(selectedCalls);
        content.Children.Add(BuildRange());
        hasClassifications = trace.Calls.Any(c => c.Classification != null);
        hasCalls = trace.Responses.Any(response => response.Calls.Count > 0);
        if (hasCalls)
        {
            BuildActivityPanel();
            var activityPanel = Box("SessionInspector.activityPanel", activities);
            if (hasClassifications)
            {
                BuildResourcePanel();
                content.Children.Add(BuildTables(activityPanel));
            }
            else content.Children.Add(activityPanel);
        }
        Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }.WithFeedbackId("SessionInspector.scroll");
        scope.ItemsSource = new[] { "All agents" }.Concat(trace.Agents).ToArray();
        scope.SelectedIndex = 0;
        AutomationProperties.SetName(scope, "Agent scope");
        scope.SelectionChanged += (_, _) => { selection.Agent = scope.SelectedIndex > 0 ? scope.SelectedItem as string : null; Refresh(); };
        Refresh();
    }

    private Control BuildTimeline()
    {
        var panel = Stack("SessionInspector.timelineContent");
        var header = Stack("SessionInspector.timelineHeader", 10);
        header.Orientation = Orientation.Horizontal;
        header.Margin = new Thickness(12, 8);
        header.Children.Add(Text("SessionInspector.timelineTitle", "Session timeline", 17, strong: true));
        header.Children.Add(timeAxis);
        header.Children.Add(costAxis);
        panel.Children.Add(header); panel.Children.Add(timeline);
        var footer = Grid("SessionInspector.timelineFooter", "*,Auto"); footer.Margin = new Thickness(12, 3, 10, 7);
        Put(footer, hint); Put(footer, zoom, 1); panel.Children.Add(footer);
        return Box("SessionInspector.timelinePanel", panel);
    }

    private HyperlinkButton AxisLink([FeedbackId] string id, string title, bool cost, string? tip = null)
    {
        var link = new HyperlinkButton { Content = title, FontSize = 12, MinHeight = 22, Padding = new Thickness(2, 0) }
            .WithFeedbackId(id);
        link.Click += (_, _) => Axis(cost);
        if (tip != null) ToolTip.SetTip(link, tip);
        return link;
    }

    private static void SetAxisLink(Button link, bool active)
    {
        link.Foreground = active ? Accent : UiPalette.Secondary;
        link.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
        AutomationProperties.SetItemStatus(link, active ? "Selected" : "");
    }

    private Control BuildRange()
    {
        var row = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("SessionInspector.rangeHeader");
        AutomationProperties.SetAutomationId(rangeTotals, "session-inspector.range-totals");
        var title = Text("SessionInspector.selectedRangeTitle", "Selected range", 18, strong: true);
        foreach (var control in new Control[] { title, stamp, rangeTotals, fromStart, full, scope })
        { control.Margin = new Thickness(0, 2, 10, 2); row.Children.Add(control); }
        return row;
    }

    private Grid BuildTables(Border activityPanel)
    {
        var tables = Grid("SessionInspector.activityAndResourcePanels", "Auto,*", "Auto,Auto");
        tables.ColumnSpacing = 12;
        Avalonia.Controls.Grid.SetIsSharedSizeScope(activities, true);
        activityPanel.HorizontalAlignment = HorizontalAlignment.Left;
        var resourcePanel = Box("SessionInspector.resourcePanel", resources);
        resourcePanel.VerticalAlignment = activityPanel.VerticalAlignment = VerticalAlignment.Top;
        Put(tables, activityPanel);
        Put(tables, resourcePanel, 1);
        SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 820;
            if (narrow == (Avalonia.Controls.Grid.GetRow(resourcePanel) == 1)) return;
            tables.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "Auto,*");
            tables.RowSpacing = narrow ? 10 : 0;
            Avalonia.Controls.Grid.SetColumn(resourcePanel, narrow ? 0 : 1);
            Avalonia.Controls.Grid.SetRow(resourcePanel, narrow ? 1 : 0);
        };
        return tables;
    }

    private void Filter(string? agent) => scope.SelectedItem = agent ?? "All agents";
    internal void SetAnalysisStatus(string? message, bool busy = false)
    {
        analyze.IsEnabled = !busy;
        if (!busy && selection.Trace.HasOutdatedClassifications)
            message = $"The classification prompt changed. Update with Luna sends outdated and unclassified calls to OpenAI. Saved results are still available.\n{message}".TrimEnd();
        analysisStatus.Text = message;
        analysisStatus.IsVisible = !string.IsNullOrEmpty(message);
    }

    internal void RestoreView(InspectorOverview prior)
    {
        resourceType.CopySelection(prior.resourceType);
        resourceTarget.CopySelection(prior.resourceTarget);
        resourceAction.CopySelection(prior.resourceAction);
        collapsedGroups.UnionWith(prior.collapsedGroups);
        sortActivityBy = prior.sortActivityBy;
        descendingActivities = prior.descendingActivities;
        groupActions.IsChecked = prior.groupActions.IsChecked;
        var saved = prior.Capture();
        if (prior.selection.FullRange)
            saved = saved with { From = selection.Trace.Start, To = selection.Trace.End, LastResponse = selection.Trace.Responses.Count };
        Restore(saved);
    }
    internal void FocusTimeline() => timeline.Focus(NavigationMethod.Directional);
    internal InspectorRange Capture() => new(selection.From, selection.To, selection.ZoomFrom, selection.ZoomTo, selection.Zoomed, selection.Agent, selection.CostAxis)
    {
        FirstResponse = selection.CostAxis ? selection.FirstResponse : null,
        LastResponse = selection.CostAxis ? selection.LastResponse : null,
        ZoomFirstResponse = selection.CostAxis ? selection.ZoomFirstResponse : null,
        ZoomLastResponse = selection.CostAxis ? selection.ZoomLastResponse : null
    };

    internal void Restore(InspectorRange saved)
    {
        selection.SetAxis(saved.CostAxis);
        if (saved.Zoomed)
        {
            RestoreRange(saved.ZoomFrom, saved.ZoomTo, saved.ZoomFirstResponse, saved.ZoomLastResponse);
            selection.ToggleZoom();
        }
        if (saved.Agent != null && selection.Trace.Agents.Contains(saved.Agent)) Filter(saved.Agent);
        RestoreRange(saved.From, saved.To, saved.FirstResponse, saved.LastResponse);
        Refresh();
    }

    private void RestoreRange(long from, long to, int? first, int? last)
    {
        if (selection.CostAxis && first.HasValue && last.HasValue) selection.SelectResponses(first.Value, last.Value);
        else selection.Select(from, to);
    }

    private void Axis(bool cost) { selection.SetAxis(cost); Refresh(); }
    private void Select(long start, long end) { selection.Select(start, end); Refresh(); }

    private void SelectFromStart()
    {
        if (selection.CostAxis) selection.SelectResponses(0, selection.LastResponse);
        else selection.Select(selection.Trace.Start, selection.To);
        Refresh();
    }

    private string RangeLabel(bool start)
    {
        var trace = selection.Trace;
        if (selection.CostAxis) return Usd(trace.CostAxis.At(start ? selection.FirstResponse : selection.LastResponse).Usd);
        return Time(trace, start ? selection.From : selection.To);
    }

    private void SetTimelineHint(string value)
    {
        hint.Text = value;
        ToolTip.SetTip(hint, value);
    }

    private void ResetTimelineHint() => SetTimelineHint(selection.Trace.Responses.Count == 0
        ? "No model responses in this session." : "Drag to select a range, or click an event.");

    private void Refresh()
    {
        ClearSelectedCalls();
        responseFocus.Hover(null);
        var t = selection.Trace;
        SetAxisLink(timeAxis, !selection.CostAxis);
        SetAxisLink(costAxis, selection.CostAxis);
        fromStart.IsEnabled = selection.CostAxis ? selection.FirstResponse > 0 : selection.From > t.Start;
        full.IsEnabled = !selection.FullRange;
        zoom.IsEnabled = selection.Zoomed || full.IsEnabled &&
            (selection.CostAxis ? selection.FirstResponse < selection.LastResponse : selection.From < selection.To);
        zoom.Content = selection.Zoomed ? "Reset zoom" : "Zoom to range";
        stamp.Text = $"{RangeLabel(true)} – {RangeLabel(false)}";
        ResetTimelineHint();
        summary = selection.Summarize(groupActions.IsChecked == true);
        timeline.UpdateRangeHandles();
        RenderSummary();
    }
}

// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class InspectorOverview : UserControl
{
    private readonly SessionSelection selection;
    private readonly Action<string, Control> detail;
    private readonly SessionTimeline timeline;
    private readonly ComboBox scope = new ComboBox { MinWidth = 116, MaxWidth = 200, FontSize = 13 }.WithFeedbackId("SessionInspector.scope");
    private readonly Button timeAxis, tokenAxis, zoom, full, fromStart;
    private readonly TextBlock stamp = Text("", 14, Accent, true);
    private readonly TextBlock hint = Text("", 12, UiPalette.Secondary);
    private readonly Grid metrics = Grid("*,*,*,*");
    private readonly Grid tables = Grid("*,12,*", "Auto,Auto");
    private readonly Grid ranges = Grid("Auto,*,Auto,*");
    private readonly Slider startSlider, endSlider;
    private readonly Button analyze;
    private readonly TextBlock analysisStatus = SessionInspectorWindow.Paragraph("");
    private readonly StackPanel resources = Stack(), activities = Stack();
    private bool updating;
    private bool narrow;
    private SessionSummary summary;

    internal InspectorOverview(AgentSession session, SessionTrace trace, Action<string, Control> detail, Action startAnalysis)
    {
        this.detail = detail;
        selection = new(trace);
        summary = selection.Summarize();
        timeline = new SessionTimeline(selection, Refresh, Filter).WithFeedbackId("SessionInspector.timeline");
        timeline.EventSelected += value => { hint.Text = value; ToolTip.SetTip(hint, value); };
        timeline.RangeMoving += UpdateStamp;
        timeAxis = Action("Time", () => Axis(false));
        tokenAxis = Action("Est. tokens", () => Axis(true), "Estimate tool input and output tokens");
        zoom = Action("Zoom to range", () => { selection.ToggleZoom(); Refresh(); });
        full = Action("Full session", () => Select(trace.Start, trace.End));
        fromStart = Action("From start", () => Select(trace.Start, selection.To));
        startSlider = Slider("Range start", true);
        endSlider = Slider("Range end", false);
        var content = Stack(12); content.Margin = new Thickness(18, 12, 18, 16);
        var heading = Text(session.DisplayTopic, 25, strong: true); ToolTip.SetTip(heading, session.DisplayTopic);
        var intro = Stack(3); intro.Children.Add(heading);
        var started = DateTimeOffset.FromUnixTimeMilliseconds(trace.Start).ToLocalTime();
        var meta = $"{AgentSession.AgentName(session.Agent)}   ·   {session.ProjectName}   ·   {started:dd MMM yyyy, HH:mm}";
        var metadata = Text(meta, 13, UiPalette.Secondary); ToolTip.SetTip(metadata, session.ProjectDirectory + "\n" + session.Id);
        intro.Children.Add(metadata);
        var totals = Text($"Full session   {Duration(trace.End - trace.Start)}   ·   {Number(trace.TotalTokens)} est. tokens   ·   {Number(trace.Calls.Count)} calls", 13, UiPalette.Secondary);
        var identity = Grid("*,Auto", "Auto,Auto"); identity.ColumnSpacing = 12;
        Put(identity, intro); Put(identity, totals, 1); content.Children.Add(identity);
        identity.SizeChanged += (_, e) =>
        {
            var small = e.NewSize.Width < 800;
            Avalonia.Controls.Grid.SetColumn(totals, small ? 0 : 1);
            Avalonia.Controls.Grid.SetRow(totals, small ? 1 : 0);
            totals.Margin = new Thickness(0, small ? 4 : 0, 0, 0);
        };
        if (trace.Note != null) content.Children.Add(SessionInspectorWindow.Paragraph(trace.Note));
        var analysisAction = trace.ClassificationError != null ? "Retry analysis" :
            trace.HasOutdatedClassifications ? "Update with Luna" : "Analyze with Luna";
        analyze = Action(analysisAction, startAnalysis);
        if (trace.HasOutdatedClassifications || trace.Calls.Any(c => c.Classification == null))
        {
            analyze.HorizontalAlignment = HorizontalAlignment.Left;
            content.Children.Add(analyze);
            content.Children.Add(analysisStatus);
            SetAnalysisStatus(trace.ClassificationError);
        }
        content.Children.Add(BuildTimeline());
        content.Children.Add(BuildRange());
        content.Children.Add(metrics); content.Children.Add(tables);
        Put(tables, Box(resources)); Put(tables, Box(activities), 2);
        Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }.WithFeedbackId("SessionInspector.scroll");
        SizeChanged += (_, e) => { var compact = e.NewSize.Width < 820; if (compact != narrow) { narrow = compact; ArrangeTables(); } };
        scope.ItemsSource = new[] { "All agents" }.Concat(trace.Agents).ToArray();
        scope.SelectedIndex = 0;
        AutomationProperties.SetName(scope, "Agent scope");
        scope.SelectionChanged += (_, _) => { selection.Agent = scope.SelectedIndex > 0 ? scope.SelectedItem as string : null; Refresh(); };
        Refresh();
    }

    private Control BuildTimeline()
    {
        var panel = Stack();
        var header = Grid("*,Auto,Auto");
        header.Margin = new Thickness(12, 8); header.ColumnSpacing = 4;
        Put(header, Text("Session timeline", 17, strong: true)); Put(header, timeAxis, 1); Put(header, tokenAxis, 2);
        panel.Children.Add(header); panel.Children.Add(timeline);
        var legend = new WrapPanel { Margin = new Thickness(10, 4) }.WithFeedbackId("SessionInspector.legend");
        foreach (var cat in selection.Trace.Calls.Select(c => c.Labels.First().Target).Distinct().Order())
        {
            var chip = Text("■  " + (cat.Length == 0 ? "Unclassified" : cat), 12, Color(cat));
            chip.Margin = new Thickness(0, 1, 14, 1); legend.Children.Add(chip);
        }
        panel.Children.Add(legend);
        ranges.Margin = new Thickness(12, 2); ranges.ColumnSpacing = 7;
        Put(ranges, Text("Start", 12, UiPalette.Secondary)); Put(ranges, startSlider, 1);
        Put(ranges, Text("End", 12, UiPalette.Secondary), 2); Put(ranges, endSlider, 3);
        panel.Children.Add(ranges);
        var footer = Grid("*,Auto"); footer.Margin = new Thickness(12, 3, 10, 7);
        Put(footer, hint); Put(footer, zoom, 1); panel.Children.Add(footer);
        return Box(panel);
    }

    private Control BuildRange()
    {
        var row = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("SessionInspector.rangeHeader");
        foreach (var control in new Control[] { Text("Selected range", 18, strong: true), stamp, fromStart, full, scope })
        { control.Margin = new Thickness(0, 2, 10, 2); row.Children.Add(control); }
        return row;
    }

    private void ArrangeTables()
    {
        tables.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,12,*");
        Avalonia.Controls.Grid.SetColumn(tables.Children[1], narrow ? 0 : 2);
        Avalonia.Controls.Grid.SetRow(tables.Children[1], narrow ? 1 : 0);
        tables.RowSpacing = narrow ? 10 : 0;
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

    internal void FocusTimeline() => timeline.Focus(NavigationMethod.Directional);
    internal InspectorRange Capture() => new(selection.From, selection.To, selection.ZoomFrom, selection.ZoomTo, selection.Zoomed, selection.Agent, timeline.Tokens);
    internal void Restore(InspectorRange saved)
    {
        if (saved.Zoomed)
        {
            selection.Select(saved.ZoomFrom, saved.ZoomTo);
            selection.ToggleZoom();
        }
        if (saved.Agent != null && selection.Trace.Agents.Contains(saved.Agent)) Filter(saved.Agent);
        timeline.Tokens = saved.Tokens;
        Select(saved.From, saved.To);
    }
    private void Axis(bool tokens) { timeline.Tokens = tokens; Refresh(); }
    private void Select(long start, long end) { selection.Select(start, end); Refresh(); }

    private void UpdateStamp() => stamp.Text = $"{Time(selection.Trace, selection.From)} – {Time(selection.Trace, selection.To)}";

    private Slider Slider(string name, bool first)
    {
        var slider = new Slider { MinHeight = 22, Minimum = 0, Maximum = 1 }.WithFeedbackId("SessionInspector.rangeSlider");
        AutomationProperties.SetName(slider, name); ToolTip.SetTip(slider, name);
        slider.AddHandler(KeyDownEvent, (_, e) => StepSlider(first, e), RoutingStrategies.Tunnel);
        slider.ValueChanged += (_, _) =>
        {
            if (updating) return;
            var t = timeline.Tokens ? selection.Trace.TimeAtTokens(slider.Value) : selection.ZoomFrom + (long)Math.Round(slider.Value);
            if (first) Select(Math.Min(t, selection.To), selection.To);
            else Select(selection.From, Math.Max(t, selection.From));
        };
        return slider;
    }

    private void StepSlider(bool first, KeyEventArgs e)
    {
        if (!timeline.Tokens) return;
        var min = first ? selection.ZoomFrom : selection.From;
        var max = first ? selection.To : selection.ZoomTo;
        var step = e.Key switch { Key.Left or Key.Down => -1, Key.Right or Key.Up => 1, Key.PageUp => 5, Key.PageDown => -5, _ => 0 };
        if (step == 0 && e.Key is not (Key.Home or Key.End)) return;
        var at = first ? selection.From : selection.To;
        if (e.KeyModifiers == KeyModifiers.Shift) step *= 5;
        var next = e.Key switch { Key.Home => min, Key.End => max, _ => selection.Trace.StepResult(at, step, min, max) };
        e.Handled = true;
        if (first) Select(next, selection.To); else Select(selection.From, next);
    }

    private void UpdateSliders()
    {
        var t = selection.Trace;
        updating = true;
        var token = timeline.Tokens;
        foreach (var (slider, at) in new[] { (startSlider, selection.From), (endSlider, selection.To) })
        {
            slider.Minimum = token ? t.TokensAt(selection.ZoomFrom) : 0;
            slider.Maximum = Math.Max(slider.Minimum + 1, token ? t.TokensAt(selection.ZoomTo) : selection.ZoomTo - selection.ZoomFrom);
            slider.SmallChange = token ? Math.Max(1, (slider.Maximum - slider.Minimum) / 100) : 1000;
            slider.LargeChange = slider.SmallChange * 10;
            slider.Value = token ? t.TokensAt(at) : at - selection.ZoomFrom;
        }
        updating = false;
    }

    private void Refresh()
    {
        UpdateSliders();
        var t = selection.Trace;
        var token = timeline.Tokens;
        timeAxis.Classes.Set("primary", !token); tokenAxis.Classes.Set("primary", token);
        tokenAxis.IsEnabled = t.TotalTokens > 0 && (token || t.TokensAt(selection.ZoomFrom) != t.TokensAt(selection.ZoomTo));
        fromStart.IsEnabled = selection.From > t.Start;
        full.IsEnabled = selection.From != t.Start || selection.To != t.End;
        zoom.IsEnabled = selection.Zoomed || full.IsEnabled && selection.From < selection.To && (!token || t.TokensAt(selection.From) < t.TokensAt(selection.To));
        zoom.Content = selection.Zoomed ? "Reset zoom" : "Zoom to range";
        UpdateStamp();
        hint.Text = t.Calls.Count == 0 ? "No completed tool calls in this session." : "Drag to select a range, or click an event.";
        ToolTip.SetTip(hint, hint.Text);
        summary = selection.Summarize();
        timeline.InvalidateVisual();
        RenderSummary();
    }
}

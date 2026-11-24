// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed class SessionInspectorWindow : Window
{
    private readonly AgentSession session;
    private readonly IAgentSessions source;
    private readonly ContentControl page = new ContentControl
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    }.WithFeedbackId("SessionInspector.page");
    private readonly CancellationTokenSource lifetime = new();
    private readonly Button back;
    private readonly Button guide;
    private Control? overview;
    private Control? overviewFocus;
    private bool detail;
    private readonly WorkspaceWindowPlacement placement;
    private InspectorRange? savedRange;

    internal SessionInspectorWindow(AgentSession session, IAgentSessions source)
    {
        this.session = session;
        this.source = source;
        placement = new(this);
        Title = session.DisplayTopic + " — Session inspector";
        Width = 1120; Height = 800; MinWidth = 480; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowHeader.Configure(this);
        var chrome = Grid("Auto,*,Auto,Auto");
        chrome.Height = 32;
        chrome.Background = UiPalette.Surface;
        WindowDecorationProperties.SetElementRole(chrome, WindowDecorationsElementRole.TitleBar);
        back = Action("Close", Back, "Close this inspector (Esc)");
        back.Classes.Add("quiet");
        guide = Action("Reading guide", ShowGuide);
        guide.Classes.Add("quiet");
        foreach (var b in new[] { back, guide }) WindowDecorationProperties.SetElementRole(b, WindowDecorationsElementRole.User);
        Put(chrome, back);
        var title = Text("  " + session.ProjectName + "   /   " + session.DisplayTopic, 13, UiPalette.Secondary);
        ToolTip.SetTip(title, Title);
        Put(chrome, title, 1); Put(chrome, guide, 2); Put(chrome, WindowHeader.CaptionButtons(this, "Inspector"), 3);
        var layout = Grid("*", "Auto,*");
        Put(layout, chrome); Put(layout, page, 0, 1); Content = layout;
        AutomationProperties.SetName(this, "Session inspector");
        overview = Message("Reading session…", session.DisplayTopic);
        page.Content = overview;
        Opened += async (_, _) => await LoadAsync();
        Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled) { e.Handled = true; Back(); }
        }, RoutingStrategies.Bubble);
    }

    internal InspectorHandoff Capture() => new(session, placement.Capture(), (overview as InspectorOverview)?.Capture() ?? savedRange);

    internal void OpenFor(Window owner, InspectorHandoff? saved = null)
    {
        if (owner.Screens.ScreenFromWindow(owner) is { } screen)
        {
            Width = Math.Clamp(screen.WorkingArea.Width / screen.Scaling - 24, MinWidth, Width);
            Height = Math.Clamp(screen.WorkingArea.Height / screen.Scaling - 24, MinHeight, Height);
        }
        placement.Track();
        if (saved != null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            placement.Restore(saved.Window with { Active = false });
            savedRange = saved.Range;
            ShowActivated = false;
        }
        Show();
    }

    private async Task LoadAsync(bool analyze = false)
    {
        var token = lifetime.Token;
        var prior = overview as InspectorOverview;
        try
        {
            prior?.SetAnalysisStatus("Analyzing tool calls…", busy: true);
            var trace = analyze
                ? await source.AnalyzeAsync(session, token, new Progress<string>(value => prior?.SetAnalysisStatus(value, busy: true)))
                : await source.InspectAsync(session, token);
            if (token.IsCancellationRequested) return;
            var ready = new InspectorOverview(session, trace, ShowDetail, () => _ = LoadAsync(analyze: true)).WithFeedbackId("SessionInspector.overview");
            if (prior != null) ready.RestoreView(prior);
            else if (savedRange != null) ready.Restore(savedRange);
            overview = ready;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (token.IsCancellationRequested) return;
            if (prior != null) { prior.SetAnalysisStatus(error.Message); return; }
            var retry = Action("Read again", () => { overview = Message("Reading session…", session.DisplayTopic); page.Content = overview; _ = LoadAsync(); });
            overview = Message("Cannot read this session", error.Message, retry);
        }
        if (!detail)
        {
            page.Content = overview;
            if (IsActive)
            {
                if (overview is InspectorOverview ready) ready.FocusTimeline(); else back.Focus();
            }
        }
    }

    private static Control Message(string heading, string message, Control? action = null)
    {
        var stack = Stack(12); stack.Margin = new Thickness(24);
        stack.Children.Add(Text(heading, 24, strong: true));
        stack.Children.Add(Paragraph(message));
        if (action != null) stack.Children.Add(action);
        return stack;
    }

    internal static TextBlock Paragraph(string value)
    {
        var text = Text(value, 14, UiPalette.Secondary);
        text.TextWrapping = TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;
        return text;
    }

    private void ShowDetail(string heading, Control body)
    {
        overviewFocus = FocusManager?.GetFocusedElement() as Control;
        back.Content = "‹  Session overview";
        detail = true;
        ToolTip.SetTip(back, "Return to the previous view (Esc)");
        guide.IsVisible = false;
        var layout = Grid("*", "Auto,*"); layout.Margin = new Thickness(22, 16);
        var title = Text(heading, 23, strong: true);
        title.Margin = new Thickness(0, 0, 0, 12);
        ToolTip.SetTip(title, heading);
        Put(layout, title); Put(layout, body, 0, 1);
        page.Content = layout;
        back.Focus();
    }

    private void Back()
    {
        if (!detail && overview is InspectorOverview inspector && inspector.ClearSelectedCalls()) { inspector.FocusTimeline(); return; }
        if (!detail) { Close(); return; }
        detail = false;
        back.Content = "Close";
        ToolTip.SetTip(back, "Close this inspector (Esc)");
        guide.IsVisible = true;
        page.Content = overview;
        if (overviewFocus?.Focus() != true) back.Focus();
        overviewFocus = null;
    }

    private void ShowGuide()
    {
        var guideBody = Stack(9);
        void Entry(string title, string explanation)
        {
            guideBody.Children.Add(Text(title, 16, Accent, true));
            guideBody.Children.Add(Paragraph(explanation));
        }
        Entry("Select an interval", "Drag across the timeline or click a task, work phase or tool call. Drag the two purple edges to adjust the range. From start extends the range backwards. Full session restores the entire trace.");
        Entry("Use the keyboard", "Tab to the timeline. Use Up and Down to change tracks, Left and Right to browse events, and Enter to select. Tab to either Range handle and use Left or Right to move it. Esc returns to the previous view.");
        Entry("Read the axes", "API cost shows model responses at standard global API rates. Each response takes up the same interval on every track it touches. A red outline marks a response with missing cost. Info and range totals sum each priced response once. Time shows when calls ran, including calls that overlap.");
        Entry("Read tool blocks", "Source, Tests, Docs and Others group tool calls by target. Docs includes documentation, agent instructions and issue trackers. Messages without calls appear in Others. Adjacent responses can share a fill. Hover to see the response boundaries and rates. Click to see the response and its calls. A pencil marks a write and a play icon marks a run.");
        Entry("Read session information", "The top panel shows the date, agent, model and totals for the whole session. The selected range totals follow the agent filter. Cost range handles snap to whole response boundaries. Select text in the call details and press Ctrl+C to copy it.");
        Entry("Analyze a session", "Xcmd loads saved classifications when you open the inspector. Choose Analyze with Luna to send new or changed calls to OpenAI for classification. When the prompt changes, choose Update with Luna to refresh saved results.");
        Entry("Activity breakdown", "A response shares its cost equally among its calls. Each call shares its cost equally among its targets, then among the actions for each target. Clear Combine actions by target to see each action's share. Select Tool time, API cost or Tool calls to sort.");
        Entry("Read tasks and phases", "Tasks come from user messages. Work phases follow plan steps, progress messages or runs of similar tools, in that order.");
        Entry("Touched resources", "Open Type, Target or Action and check the options to show. The list updates as you check options. Each action filter includes Other for calls with no clear action on that resource. Counts, bars and resource details show matching calls in the selected range. Expand a folder to see its files. Bars use the same target colors as the session timeline. Brighter bars and icons mark edits and runs.");
        ShowDetail("Reading the timeline", new ScrollViewer { Content = guideBody }.WithFeedbackId("SessionInspector.guideScroll"));
    }
}

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
    private readonly Stack<(Control Page, Control? Focus, string Back)> previous = new();
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

    private async Task LoadAsync()
    {
        var token = lifetime.Token;
        try
        {
            var status = Text("Reading session…", 22, strong: true);
            overview = Box(status, padding: new Thickness(24));
            if (!detail) page.Content = overview;
            var trace = await source.InspectAsync(session, token, new Progress<string>(value => status.Text = value));
            if (token.IsCancellationRequested) return;
            var ready = new InspectorOverview(session, trace, ShowDetail, RetryClassification).WithFeedbackId("SessionInspector.overview");
            if (savedRange != null) ready.Restore(savedRange);
            overview = ready;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (token.IsCancellationRequested) return;
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

    private void RetryClassification()
    {
        savedRange = (overview as InspectorOverview)?.Capture();
        _ = LoadAsync();
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
        if (page.Content is Control current)
            previous.Push((current, FocusManager?.GetFocusedElement() as Control, back.Content?.ToString() ?? ""));
        back.Content = detail ? "‹  All resources" : "‹  Session overview";
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
        if (!detail) { Close(); return; }
        var prior = previous.Pop();
        detail = previous.Count > 0;
        back.Content = prior.Back;
        ToolTip.SetTip(back, detail ? "Return to the selected range (Esc)" : "Close this inspector (Esc)");
        guide.IsVisible = !detail; page.Content = detail ? prior.Page : overview;
        if (prior.Focus?.Focus() != true) back.Focus();
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
        Entry("Use the keyboard", "Tab to the timeline. Use Up and Down to change tracks, Left and Right to browse events, and Enter to select. Use the start and end sliders below the timeline for a custom range. Esc returns to the previous view.");
        Entry("Read the axes", "Time shows when each call ran. Est. tokens orders results by completion and estimates the size of tool inputs and outputs at one token per four characters. The estimate excludes model messages, images and billing.");
        Entry("Understand the totals", "Calls, tokens and touched resources count when a result arrives. Tool time includes the part of each call inside the range, including parallel calls. Outside tools counts time with no selected agent running a tool.");
        Entry("Activity breakdown", "Luna classifies each completed call by target and action. A call can appear in several rows. The session totals count each call once. Open View tool calls to see each summary and its objects. Xcmd saves validated answers locally and sends new or changed calls to OpenAI when you open the inspector.");
        Entry("Read tasks and phases", "Tasks come from user messages. Work phases follow plan steps, progress messages or runs of similar tools, in that order.");
        Entry("Touched resources", "Luna identifies files, directories, URLs, issues, repositories and other resources while classifying tool calls. The table counts completed calls in the selected range and agent scope. Open a resource to see those calls.");
        ShowDetail("Reading the timeline", new ScrollViewer { Content = guideBody }.WithFeedbackId("SessionInspector.guideScroll"));
    }
}

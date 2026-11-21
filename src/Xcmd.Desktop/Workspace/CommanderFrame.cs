// @implements DiscoverShortcuts, ReportControl, KeepFeedback, TryLocalRelease
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Desktop.Features.Feedback;

namespace Xcmd.Desktop.Workspace;

internal sealed class CommanderFrame
{
    private readonly Button feedback = FooterButton("Feedback", "StartFeedback", "feedback.start");
    private readonly Button notes = FooterButton("Notes (0)", "ShowNotes", "feedback.show-notes");
    public Grid TitleBar { get; } = new Grid() { Name = "MainTitleBar", Height = UiPalette.TitleHeight, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), Background = UiPalette.Canvas }.WithFeedbackId("CommanderFrame.TitleBar");
    public Grid StatusBar { get; } = new Grid() { Name = "MainStatusBar", ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") }.WithFeedbackId("CommanderFrame.StatusBar");

    public CommanderFrame(CommanderWindow window, Menu menu, TextBlock status, TextBlock update)
    {
        WindowHeader.Configure(window);
        WindowDecorationProperties.SetElementRole(TitleBar, WindowDecorationsElementRole.TitleBar);
        WindowDecorationProperties.SetElementRole(menu, WindowDecorationsElementRole.User);
        TitleBar.Children.Add(menu);
        var title = new TextBlock
        {
            Text = window.ReleaseCaption,
            FontSize = UiPalette.MetaSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(12, 0, 4, 0)
        }.WithFeedbackId("CommanderFrame.title");
        var uptime = new TextBlock
        {
            Text = window.UptimeText,
            FontSize = UiPalette.MetaSize,
            Foreground = UiPalette.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0)
        }.WithFeedbackId("CommanderFrame.uptime");
        ToolTip.SetTip(uptime, "Time since this application started");
        ToolTip.SetTip(title, window.Title);
        window.PropertyChanged += (_, e) => { if (e.Property == Window.TitleProperty) { title.Text = window.ReleaseCaption; uptime.Text = window.UptimeText; ToolTip.SetTip(title, window.Title); } };
        Grid.SetColumn(title, 1); TitleBar.Children.Add(title);
        Grid.SetColumn(uptime, 2); TitleBar.Children.Add(uptime);
        update.MaxWidth = 280; update.TextWrapping = TextWrapping.NoWrap; update.TextTrimming = TextTrimming.CharacterEllipsis;
        update.Margin = new Thickness(8, 0);
        update.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) ToolTip.SetTip(update, update.Text); };
        Grid.SetColumn(update, 3); TitleBar.Children.Add(update);
        var caption = WindowHeader.CaptionButtons(window, "Main");
        Grid.SetColumn(caption, 4); TitleBar.Children.Add(caption);
        status.TextTrimming = TextTrimming.CharacterEllipsis; status.VerticalAlignment = VerticalAlignment.Center;
        status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) ToolTip.SetTip(status, status.Text); };
        StatusBar.Children.Add(status);
        Grid.SetColumn(feedback, 1); StatusBar.Children.Add(feedback);
        Grid.SetColumn(notes, 2); StatusBar.Children.Add(notes);
    }

    public void AttachFeedback(FeedbackController controller)
    {
        if (controller.Connected)
        {
            feedback.IsVisible = notes.IsVisible = false;
            return;
        }
        feedback.Click += (_, _) => controller.Begin();
        notes.Click += (_, _) => controller.ShowNotes();
        controller.Notes.CollectionChanged += (_, _) => notes.Content = $"Notes ({controller.Notes.Count(note => note.Status != Xcmd.Core.FeedbackIssue.Closed)})";
    }

    private static Button FooterButton(string label, string name, string id)
    {
        var button = new Button
        {
            Content = label,
            Name = name,
            Height = 20,
            MinHeight = 0,
            Padding = new Thickness(6, 0),
            FontSize = UiPalette.MetaSize,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0)
        }.WithFeedbackId("CommanderFrame.button");
        AutomationProperties.SetAutomationId(button, id);
        return button;
    }
    public static Control BuildKeyboardHelp(CommanderWindow? window = null)
    {
        var help = new WrapPanel { Name = "KeyboardHelp" }.WithFeedbackId("CommanderFrame.help");
        void Update()
        {
            string[] hints = window?.ActivePanel.HasHistory == true
                ? GitPath.IsLog(window.ActivePanel.Session.DirectoryPath)
                    ? ["Enter  Full repository snapshot", "F3  Commit details", "Ctrl+R  Refresh", "Backspace  Back", "Esc  Return", "Tab  Other panel"]
                    : ["F3  View / changed files on ..", "Ctrl+Alt+D  Diff", "Ctrl+R  Refresh", "Backspace  Back", "Esc  Return", "Tab  Other panel"]
                :
                [
                    "Alt+F7  Find files", "F3  View", "F4  Edit", "Ctrl+Shift+C  Compare",
                    .. CommanderWindow.TransferCommands.Select(command => command.Hint),
                    "F7  New folder", "Ctrl+L  Command line", "Alt+↑ ↓  Commands", "Ctrl+Enter  Name", "Ctrl+Shift+Enter  Path"
                ];
            help.Children.Clear();
            foreach (var hint in hints) help.Children.Add(new Border
            {
                BorderThickness = new Thickness(help.Children.Count == 0 ? 0 : 1, 0, 0, 0),
                BorderBrush = UiPalette.Divider,
                Padding = new Thickness(help.Children.Count == 0 ? 0 : 8, 0, 8, 0),
                Margin = new Thickness(0, 3),
                Child = new TextBlock { Text = hint, FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary }.WithFeedbackId("CommanderFrame.BuildKeyboardHelp")
            }.WithFeedbackId("CommanderFrame.BuildKeyboardHelp.2"));
        }
        if (window != null)
            foreach (var panel in new[] { window.Left, window.Right })
            {
                panel.Activated += _ => Update(); panel.LocationVisited += _ => Update();
            }
        Update();
        return help;
    }

}

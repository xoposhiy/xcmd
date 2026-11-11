using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Xcmd.Desktop;

internal sealed class CommanderFrame
{
    private readonly Button feedback = FooterButton("Feedback", "StartFeedback", "feedback.start");
    private readonly Button notes = FooterButton("Notes (0)", "ShowNotes", "feedback.show-notes");
    public Grid TitleBar { get; } = new() { Name = "MainTitleBar", Height = 32, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Background = new SolidColorBrush(Color.Parse("#0E1420")) };
    public Grid StatusBar { get; } = new() { Name = "MainStatusBar", ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };

    public CommanderFrame(CommanderWindow window, Menu menu, TextBlock status, TextBlock update)
    {
        WindowHeader.Configure(window);
        WindowDecorationProperties.SetElementRole(TitleBar, WindowDecorationsElementRole.TitleBar);
        WindowDecorationProperties.SetElementRole(menu, WindowDecorationsElementRole.User);
        TitleBar.Children.Add(menu);
        var title = new TextBlock
        {
            Text = window.Title,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(12, 0, 4, 0)
        };
        ToolTip.SetTip(title, window.Title);
        window.PropertyChanged += (_, e) => { if (e.Property == Window.TitleProperty) { title.Text = window.Title; ToolTip.SetTip(title, window.Title); } };
        Grid.SetColumn(title, 1); TitleBar.Children.Add(title);
        update.MaxWidth = 280; update.TextWrapping = TextWrapping.NoWrap; update.TextTrimming = TextTrimming.CharacterEllipsis;
        update.Margin = new Thickness(8, 0);
        update.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) ToolTip.SetTip(update, update.Text); };
        Grid.SetColumn(update, 2); TitleBar.Children.Add(update);
        var caption = WindowHeader.CaptionButtons(window, "Main");
        Grid.SetColumn(caption, 3); TitleBar.Children.Add(caption);
        status.TextTrimming = TextTrimming.CharacterEllipsis; status.VerticalAlignment = VerticalAlignment.Center;
        status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) ToolTip.SetTip(status, status.Text); };
        StatusBar.Children.Add(status);
        Grid.SetColumn(feedback, 1); StatusBar.Children.Add(feedback);
        Grid.SetColumn(notes, 2); StatusBar.Children.Add(notes);
    }

    public void AttachFeedback(FeedbackController controller)
    {
        feedback.Click += (_, _) => controller.Begin();
        notes.Click += (_, _) => controller.ShowNotes();
        controller.Notes.CollectionChanged += (_, _) => notes.Content = $"Notes ({controller.Notes.Count(note => note.ResolvedIn == null)})";
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
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0)
        };
        AutomationProperties.SetAutomationId(button, id);
        return button;
    }
}

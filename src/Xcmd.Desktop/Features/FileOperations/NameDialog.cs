// @implements CreateFolder, CreateFile
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Xcmd.Desktop.Features.FileOperations;

public sealed class NameDialog : Window
{
    public TextBox Editor { get; } = new TextBox() { Name = "DirectoryName", PlaceholderText = "Folder name" }.WithFeedbackId("CommanderWindow.Editor");
    public TextBlock Error { get; } = UiLayout.Error();
    public event Action<string>? Submitted;
    public NameDialog(Feedback.FeedbackController? feedback = null, bool file = false)
    {
        Title = file ? "New file" : "New folder"; Width = 440; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(Editor, file ? "new-file.name" : "new-folder.name");
        Editor.PlaceholderText = file ? "File name" : "Folder name";
        var create = new Button { Content = file ? "Create and edit" : "Create", Name = file ? "CreateFile" : "CreateFolder", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right }.WithFeedbackId("CommanderWindow.create");
        var cancel = new Button { Content = "Cancel", Name = "CancelCreateFolder" }.WithFeedbackId("CommanderWindow.cancel");
        cancel.Click += (_, _) => Close();
        create.Click += (_, _) => Submitted?.Invoke(Editor.Text ?? "");
        DialogActions.FollowFocus(this, create, cancel);
        Content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children = { new TextBlock { Text = file ? "File name" : "Folder name" }.WithFeedbackId("CommanderWindow.content.4"), Editor, Error, UiLayout.Actions(create, cancel) }
        }.WithFeedbackId("CommanderWindow.content.5");
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback?.IsPickingIn(this) == true) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && Editor.IsKeyboardFocusWithin) { Submitted?.Invoke(Editor.Text ?? ""); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }
}

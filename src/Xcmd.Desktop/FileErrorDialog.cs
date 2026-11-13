using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileErrorDialog : Window
{
    public event Action<FileErrorAction>? Decided;

    public FileErrorDialog(FileOperationError failure, FeedbackController feedback)
    {
        Title = "File operation failed"; Width = 620; MinWidth = 440;
        SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var retry = Choice("_Retry", FileErrorAction.Retry);
        var skip = Choice("_Skip", FileErrorAction.Skip);
        var skipAll = Choice("Skip similar _errors", FileErrorAction.SkipAll);
        var cancel = Choice("_Cancel operation", FileErrorAction.Cancel);
        var path = new TextBlock { Text = failure.Source.FullPath, TextTrimming = TextTrimming.LeadingCharacterEllipsis }.WithFeedbackId("FileErrorDialog.path");
        ToolTip.SetTip(path, failure.Source.FullPath);
        Content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children =
            {
                path,
                new TextBlock { Text = "Error: " + failure.Message, Foreground = UiPalette.Error, TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FileErrorDialog.content"),
                UiLayout.Actions(retry, skip, skipAll, cancel)
            }
        }.WithFeedbackId("FileErrorDialog.content.2");
        DialogActions.FollowFocus(this, retry, skip, skipAll, cancel);
        Opened += (_, _) => retry.Focus();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback.IsPickingIn(this) || feedback.ActiveComment != null) return;
            if (e.Key == Key.Escape) { Decided?.Invoke(FileErrorAction.Cancel); e.Handled = true; }
        }, RoutingStrategies.Tunnel);

        Button Choice(string label, FileErrorAction action)
        {
            var button = new Button { Content = new AccessText { Text = label, ShowAccessKey = true }.WithFeedbackId("FileErrorDialog.button"), Name = "FileError" + action }.WithFeedbackId("FileErrorDialog.button.2");
            AutomationProperties.SetAutomationId(button, "file-error." + action.ToString().ToLowerInvariant());
            button.Click += (_, _) => Decided?.Invoke(action);
            return button;
        }
    }
}

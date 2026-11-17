// @implements RecoverOperations
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
    public FileUsersView? Users { get; }
    private readonly CancellationTokenSource lifetime = new();
    private bool decided;

    public FileErrorDialog(FileOperationError failure, FeedbackController feedback, bool rename = false)
    {
        Title = rename ? "Rename failed" : "File operation failed"; Width = 620; MinWidth = 440;
        SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var retry = Choice("_Retry", FileErrorAction.Retry);
        var skip = Choice("_Skip", FileErrorAction.Skip);
        var skipAll = Choice("Skip similar _errors", FileErrorAction.SkipAll);
        var cancel = Choice(rename ? "Ca_ncel" : "Ca_ncel operation", FileErrorAction.Cancel);
        var hasUsers = failure.Users.Length > 0;
        skip.IsVisible = !rename;
        skipAll.IsVisible = !rename && !hasUsers;
        var path = new TextBlock { Text = failure.Source.FullPath, Foreground = UiPalette.Secondary, FontSize = UiPalette.MetaSize, TextTrimming = TextTrimming.LeadingCharacterEllipsis }.WithFeedbackId("FileErrorDialog.path");
        ToolTip.SetTip(path, failure.Source.FullPath);
        var content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children =
            {
                path,
                new TextBlock
                {
                    Text = hasUsers ? (failure.Source.IsDirectory ? "Folder is used by:" : "File is used by:") : "Error: " + failure.Message,
                    Foreground = hasUsers ? UiPalette.Text : UiPalette.Error, TextWrapping = TextWrapping.Wrap
                }.WithFeedbackId("FileErrorDialog.content")
            }
        }.WithFeedbackId("FileErrorDialog.content.2");
        if (hasUsers)
        {
            Users = new FileUsersView(failure.Users).WithFeedbackId("FileErrorDialog.users");
            Users.AllExited += () => _ = RetryWhenReadyAsync(feedback);
            Opened += (_, _) => Users.StartWatching();
            content.Children.Add(Users);
            foreach (var link in new[] { retry, skip })
            { link.Background = Brushes.Transparent; link.BorderThickness = new Thickness(0); link.Foreground = UiPalette.Focus; }
        }
        content.Children.Add(UiLayout.Actions(retry, skip, skipAll, cancel));
        Content = content;
        DialogActions.FollowFocus(this, retry, skip, skipAll, cancel);
        Opened += (_, _) => retry.Focus();
        Closed += (_, _) => { decided = true; lifetime.Cancel(); Users?.CancelActions(); };
        AttachKeyboard(feedback, skip.IsVisible, skipAll.IsVisible);

        Button Choice(string label, FileErrorAction action)
        {
            var button = new Button { Content = new AccessText { Text = label, ShowAccessKey = true }.WithFeedbackId("FileErrorDialog.button"), Name = "FileError" + action }.WithFeedbackId("FileErrorDialog.button.2");
            AutomationProperties.SetAutomationId(button, "file-error." + action.ToString().ToLowerInvariant());
            button.Click += (_, _) => Decide(action);
            return button;
        }

    }

    private void AttachKeyboard(FeedbackController feedback, bool canSkip, bool canSkipAll) =>
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback.IsPickingIn(this) || feedback.ActiveComment != null) return;
            FileErrorAction? action = e.Key == Key.Escape ? FileErrorAction.Cancel : null;
            if (e.KeyModifiers == KeyModifiers.Alt) action = e.Key switch
            {
                Key.R => FileErrorAction.Retry,
                Key.S when canSkip => FileErrorAction.Skip,
                Key.E when canSkipAll => FileErrorAction.SkipAll,
                Key.N => FileErrorAction.Cancel,
                _ => action
            };
            if (action is not { } choice) return;
            Decide(choice); e.Handled = true;
        }, RoutingStrategies.Tunnel);

    private async Task RetryWhenReadyAsync(FeedbackController feedback)
    {
        try
        {
            while (feedback.IsActive) await Task.Delay(100, lifetime.Token);
            if (!lifetime.IsCancellationRequested) Decide(FileErrorAction.Retry);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private void Decide(FileErrorAction action)
    {
        if (decided) return;
        decided = true;
        Users?.CancelActions();
        Decided?.Invoke(action);
    }
}

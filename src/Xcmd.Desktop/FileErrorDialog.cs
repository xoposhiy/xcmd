// @implements RenameEntry, RecoverOperations
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
    public FileUsersView? Users { get; private set; }
    private readonly CancellationTokenSource lifetime = new();
    private readonly ContentControl usersHost = new ContentControl().WithFeedbackId("FileErrorDialog.usersHost");
    private readonly TextBlock message = new TextBlock { TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FileErrorDialog.content");
    private readonly TextBlock guidance = new TextBlock { TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FileErrorDialog.guidance");
    private readonly TextBlock retryStatus = new TextBlock { IsVisible = false, TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FileErrorDialog.retryStatus");
    private readonly FeedbackController feedback;
    private readonly Func<Task<FileOperationError?>>? retryRename;
    private readonly Button retry;
    private readonly Button cancel;
    private bool retrying;
    private bool decided;

    public FileErrorDialog(FileOperationError failure, FeedbackController feedback,
        string? newName = null, Func<Task<FileOperationError?>>? retryRename = null)
    {
        this.feedback = feedback;
        this.retryRename = retryRename;
        var rename = retryRename != null;
        Title = rename ? "Rename failed" : "File operation failed"; Width = 620; MinWidth = 440;
        SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        retry = Choice("_Retry", FileErrorAction.Retry);
        var skip = Choice("_Skip", FileErrorAction.Skip);
        var skipAll = Choice("Skip similar _errors", FileErrorAction.SkipAll);
        cancel = Choice(rename ? "Ca_ncel" : "Ca_ncel operation", FileErrorAction.Cancel);
        var hasUsers = failure.Users.Length > 0;
        skip.IsVisible = !rename;
        skipAll.IsVisible = !rename && !hasUsers;
        var path = new TextBlock { Text = failure.Source.FullPath, Foreground = UiPalette.Secondary, FontSize = UiPalette.MetaSize, TextTrimming = TextTrimming.LeadingCharacterEllipsis }.WithFeedbackId("FileErrorDialog.path");
        ToolTip.SetTip(path, failure.Source.FullPath);
        var content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children = { path }
        }.WithFeedbackId("FileErrorDialog.content.2");
        if (rename)
        {
            content.Children.Add(new TextBlock
            {
                Text = "New name · " + newName,
                TextWrapping = TextWrapping.Wrap
            }.WithFeedbackId("FileErrorDialog.newName"));
            content.Children.Add(retryStatus);
        }
        content.Children.Add(message);
        content.Children.Add(usersHost);
        if (rename) content.Children.Add(guidance);
        SetFailure(failure);
        if (hasUsers && !rename)
            foreach (var link in new[] { retry, skip })
            { link.Background = Brushes.Transparent; link.BorderThickness = new Thickness(0); link.Foreground = UiPalette.Focus; }
        content.Children.Add(UiLayout.Actions(retry, skip, skipAll, cancel));
        Content = content;
        DialogActions.FollowFocus(this, retry, skip, skipAll, cancel);
        Opened += (_, _) => { retry.Focus(); Users?.StartWatching(); };
        Closing += (_, e) => { if (retrying) e.Cancel = true; };
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

    private void SetFailure(FileOperationError failure)
    {
        var hasUsers = failure.Users.Length > 0;
        var rename = retryRename != null;
        message.Text = rename || !hasUsers ? failure.Message :
            failure.Source.IsDirectory ? "Folder is used by:" : "File is used by:";
        message.Foreground = rename || !hasUsers ? UiPalette.Error : UiPalette.Text;
        if (rename)
        {
            guidance.Text = hasUsers ? "The listed programs may be blocking the rename. Close them or choose Retry to check again." :
                failure.AccessDenied ? "Xcmd could not identify a program using this item. Close any program that has it open and check your permissions, then choose Retry." :
                "Choose Retry to try again, or Cancel to return to the name editor.";
        }

        Users = hasUsers ? MakeUsersView(failure.Users) : null;
        usersHost.Content = Users;
        usersHost.IsVisible = hasUsers;
    }

    private FileUsersView MakeUsersView(FileUser[] users)
    {
        var view = new FileUsersView(users).WithFeedbackId("FileErrorDialog.users");
        view.AllExited += () => _ = RetryWhenReadyAsync(view);
        return view;
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

    private async Task RetryWhenReadyAsync(FileUsersView view)
    {
        try
        {
            while (feedback.IsActive) await Task.Delay(100, lifetime.Token);
            if (!lifetime.IsCancellationRequested && Users == view) Decide(FileErrorAction.Retry);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private void Decide(FileErrorAction action)
    {
        if (decided || retrying) return;
        if (action == FileErrorAction.Retry && retryRename is { } attempt)
        {
            _ = RetryRenameAsync(attempt);
            return;
        }
        decided = true;
        Users?.CancelActions();
        Decided?.Invoke(action);
    }

    private async Task RetryRenameAsync(Func<Task<FileOperationError?>> attempt)
    {
        retrying = true;
        retry.IsEnabled = cancel.IsEnabled = false;
        Users?.CancelActions();
        usersHost.IsEnabled = false;
        retryStatus.Text = "Retrying rename…";
        retryStatus.IsVisible = true;
        FileOperationError? failure;
        try
        {
            Decided?.Invoke(FileErrorAction.Retry);
            failure = await attempt();
        }
        catch (Exception error)
        {
            retryStatus.Text = "Retry failed. " + error.Message;
            return;
        }
        finally
        {
            retrying = false;
            retry.IsEnabled = cancel.IsEnabled = true;
            retry.Focus();
        }
        if (failure == null) { Close(); return; }

        retryStatus.Text = "Rename still failed.";
        SetFailure(failure);
        usersHost.IsEnabled = true;
        Users?.StartWatching();
    }
}

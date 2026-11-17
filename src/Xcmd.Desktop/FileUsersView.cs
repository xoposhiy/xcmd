using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed class FileUsersView : StackPanel
{
    private readonly TextBlock status = new TextBlock { IsVisible = false, TextWrapping = TextWrapping.Wrap, Foreground = UiPalette.Error }.WithFeedbackId("FileUsersView.status");
    private readonly List<Button> actions = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly FileUser[] users;
    private bool busy;
    public bool IsBusy => busy;
    public Task Monitoring { get; private set; } = Task.CompletedTask;
    public IFileUserActions Actions { get; set; } = new WindowsFileUserActions();
    public event Action? AllExited;

    public FileUsersView(FileUser[] users)
    {
        this.users = users;
        Spacing = 6;
        var rows = new StackPanel { Spacing = 4 }.WithFeedbackId("FileUsersView.rows");
        foreach (var group in users.GroupBy(user => user.WindowKey ?? "process:" + user.ProcessId))
            rows.Children.Add(Row(group.ToArray()));
        Children.Add(new ScrollViewer { Content = rows, MaxHeight = 240, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }.WithFeedbackId("FileUsersView.scroll"));
        Children.Add(new TextBlock
        {
            Text = "Kill may lose unsaved changes.",
            Foreground = UiPalette.Secondary,
            TextWrapping = TextWrapping.Wrap
        }.WithFeedbackId("FileUsersView.explanation"));
        Children.Add(status);
    }

    public void StartWatching() => Monitoring = WatchAsync();

    private async Task WatchAsync()
    {
        using var monitoring = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            await Task.WhenAll(users.Select(WaitAsync));
            if (!lifetime.IsCancellationRequested) AllExited?.Invoke();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { SetError("Automatic retry is unavailable. " + error.Message + " Use Retry after releasing the file."); }

        async Task WaitAsync(FileUser user)
        {
            try { await Actions.WaitForExitAsync(user, monitoring.Token); }
            catch { monitoring.Cancel(); throw; }
        }
    }

    public void CancelActions() => lifetime.Cancel();

    private Control Row(FileUser[] group)
    {
        var user = group[0];
        var name = user.ApplicationName ?? user.Name;
        Control application;
        if (user.WindowKey != null)
        {
            var link = Link(name, "FileUserWindow", "Switch to: " + (user.WindowTitle ?? name));
            link.Click += async (_, _) => await RunAsync(() => Actions.ShowWindowAsync(user));
            application = link;
        }
        else application = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("FileUsersView.application");
        if (user.WindowKey == null) ToolTip.SetTip(application, name);
        var end = new Button
        {
            Name = "FileUserEnd",
            Content = "Kill",
            Foreground = UiPalette.Error,
            Margin = new Thickness(8, 0, 0, 0)
        }.WithFeedbackId("FileUsersView.end");
        ToolTip.SetTip(end, "Forcibly end only the listed processes, then retry automatically. Unsaved changes can be lost.");
        end.Click += async (_, _) => await RunAsync(async () =>
        {
            foreach (var process in group) await Actions.EndProcessAsync(process, lifetime.Token);
        });
        actions.Add(end);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }.WithFeedbackId("FileUsersView.row");
        Grid.SetColumn(end, 1);
        row.Children.Add(application); row.Children.Add(end);
        var processes = new TextBlock
        {
            Name = "FileUserProcesses",
            Foreground = UiPalette.Secondary,
            Text = string.Join(", ", group.Select(process => (process.ExecutableName ?? process.Name) + " (PID " + process.ProcessId + ")")),
            TextTrimming = TextTrimming.CharacterEllipsis
        }.WithFeedbackId("FileUsersView.processes");
        ToolTip.SetTip(processes, processes.Text);
        var result = new StackPanel { Spacing = 0 }.WithFeedbackId("FileUsersView.group");
        result.Children.Add(row); result.Children.Add(processes);
        return result;
    }

    private Button Link(string label, string name, string hint)
    {
        var button = new Button
        {
            Name = name,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2),
            Foreground = UiPalette.Focus,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new TextBlock { Text = label, TextDecorations = TextDecorations.Underline, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 340 }.WithFeedbackId("FileUsersView.linkText")
        }.WithFeedbackId("FileUsersView.link");
        ToolTip.SetTip(button, hint);
        actions.Add(button);
        return button;
    }

    private void SetError(string message) { status.Text = message; status.IsVisible = true; }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy || lifetime.IsCancellationRequested) return;
        busy = true;
        status.IsVisible = false;
        foreach (var control in actions) control.IsEnabled = false;
        try { await action(); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { SetError(error.Message); }
        finally
        {
            busy = false;
            foreach (var control in actions) control.IsEnabled = true;
        }
    }
}

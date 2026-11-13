using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed class GitStatusView : Border
{
    private readonly IGitStatusReader reader;
    private readonly TextBlock branch = new() { MaxWidth = 150, LineHeight = 18, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, FontFamily = new FontFamily("Consolas"), FontSize = UiPalette.BodySize, FontWeight = FontWeight.SemiBold, Foreground = UiPalette.Focus, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock state = new() { LineHeight = 18, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, FontFamily = new FontFamily("Consolas"), FontSize = UiPalette.BodySize, FontWeight = FontWeight.SemiBold, Foreground = UiPalette.Secondary, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly PathIcon remote = new()
    {
        Width = 14,
        Height = 14,
        Margin = new Thickness(0, 0, 6, 0),
        Foreground = UiPalette.Secondary,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        IsVisible = false,
        Data = Geometry.Parse("M 5,15 C 0,15 0,8 4,8 C 4,1 14,0 16,7 C 22,5 25,15 18,15 Z")
    };
    private string statusText = "";
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private CancellationTokenSource? request;
    private string directory = "";
    private string actionDirectory = "";
    private readonly TextBlock arrow = new() { Text = "▾", LineHeight = 18, FontSize = UiPalette.BodySize, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Foreground = UiPalette.Secondary, IsVisible = false };
    public Button ActionsButton { get; } = new()
    {
        Height = 24, MinHeight = 0, MinWidth = 0, Padding = new Thickness(6, 0), BorderThickness = new Thickness(0),
        Background = Brushes.Transparent, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        IsEnabled = false, IsVisible = false
    };
    public ContextMenu Actions { get; } = new() { Placement = PlacementMode.Bottom };
    public bool IsMenuOpen => Actions.IsOpen;
    public MenuItem CommitCommand { get; } = new() { Header = "_Commit" };
    public event Action<string>? CommitRequested;
    public Task PendingWork { get; private set; } = Task.CompletedTask;
    public GitStatus? Status { get; private set; }
    public string Summary => branch.Text + statusText;

    public GitStatusView(string id, IGitStatusReader? reader = null)
    {
        this.reader = reader ?? new GitStatusReader();
        Name = id + "Git";
        AutomationProperties.SetAutomationId(this, id + ".git");
        AutomationProperties.SetAutomationId(branch, id + ".git.branch");
        AutomationProperties.SetAutomationId(state, id + ".git.status");
        AutomationProperties.SetAutomationId(remote, id + ".git.remote");
        AutomationProperties.SetAutomationId(arrow, id + ".git.arrow");
        ToolTip.SetTip(remote, "Repository has a remote");
        AutomationProperties.SetName(remote, "Repository has a remote");
        Height = 24; MaxWidth = 300; ClipToBounds = true; VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center; IsVisible = false;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        grid.Children.Add(remote); Grid.SetColumn(branch, 1); grid.Children.Add(branch); Grid.SetColumn(state, 2); grid.Children.Add(state);
        Grid.SetColumn(arrow, 3); grid.Children.Add(arrow); ActionsButton.Content = grid; Child = ActionsButton;
        ActionsButton.Name = id + "GitActions";
        AutomationProperties.SetAutomationId(ActionsButton, id + ".git.actions");
        AutomationProperties.SetAutomationId(CommitCommand, id + ".git.commit");
        CommitCommand.Name = id + "GitCommit";
        ToolTip.SetTip(CommitCommand, "Open Claude Code to commit all changes in this repository.");
        Actions.ItemsSource = new[] { CommitCommand };
        Actions.Opened += (_, _) => actionDirectory = directory;
        ActionsButton.Click += (_, _) => { if (Status != null) Actions.Open(ActionsButton); };
        CommitCommand.Click += (_, _) => { if (actionDirectory.Length > 0) CommitRequested?.Invoke(actionDirectory); };
        timer.Tick += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) => { timer.Start(); Refresh(); };
        DetachedFromVisualTree += (_, _) => { timer.Stop(); request?.Cancel(); };
    }

    public void SetDirectory(string value)
    {
        if (directory == value) return;
        Actions.Close();
        directory = value;
        request?.Cancel();
        Apply(null);
        StartRead();
    }

    public void Refresh()
    {
        if (PendingWork.IsCompleted) StartRead();
    }

    private void StartRead()
    {
        if (directory.Length == 0 || ArchivePath.IsEntry(directory)) return;
        request = new CancellationTokenSource();
        PendingWork = UpdateAsync(directory, request);
    }

    private async Task UpdateAsync(string path, CancellationTokenSource owner)
    {
        try
        {
            var result = await reader.ReadAsync(path, owner.Token);
            if (!owner.IsCancellationRequested && directory == path) Apply(result);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException)
        { if (!owner.IsCancellationRequested && directory == path) Apply(null); }
        finally { owner.Dispose(); if (request == owner) request = null; }
    }

    private void Apply(GitStatus? value)
    {
        Status = value;
        IsVisible = value != null;
        ActionsButton.IsVisible = value != null;
        ActionsButton.IsEnabled = value != null;
        ActionsButton.ContextMenu = value == null ? null : Actions;
        ActionsButton.Cursor = value == null ? null : new Cursor(StandardCursorType.Hand);
        arrow.IsVisible = value != null;
        Background = value == null ? Brushes.Transparent : UiPalette.Raised;
        remote.IsVisible = value?.HasRemote == true;
        state.Inlines!.Clear(); statusText = "";
        if (value == null)
        {
            Actions.Close(); branch.Text = ""; ToolTip.SetTip(this, null); ToolTip.SetTip(ActionsButton, null);
            AutomationProperties.SetName(this, ""); AutomationProperties.SetName(ActionsButton, ""); return;
        }
        branch.Text = value.Branch;
        var clean = value.Staged + value.Changed + value.Untracked + value.Deleted + value.Conflicts == 0;
        branch.Foreground = clean ? UiPalette.Success : UiPalette.Focus;
        if (value.Conflicts > 0) AddStatus($"!{value.Conflicts}", UiPalette.Error);
        if (value.Deleted > 0) AddStatus($"−{value.Deleted}", UiPalette.Error);
        if (value.Staged > 0) AddStatus($"+{value.Staged}", UiPalette.Success);
        if (value.Changed > 0) AddStatus($"~{value.Changed}", UiPalette.Mark);
        if (value.Untracked > 0) AddStatus($"?{value.Untracked}", UiPalette.Success);
        if (value.Ahead > 0) AddStatus($"↑{value.Ahead}", UiPalette.Secondary);
        if (value.Behind > 0) AddStatus($"↓{value.Behind}", UiPalette.Secondary);
        var description = $"Git · {value.Branch} · {(clean ? "clean" : "changes")}\nWhole repository: {value.Staged} staged (+), {value.Changed} changed (~), {value.Untracked} untracked (?), {value.Deleted} deleted (−), {value.Conflicts} conflicts (!)\nUpstream: {value.Upstream ?? "not configured"} · {value.Ahead} ahead (↑), {value.Behind} behind (↓) · local status, no fetch";
        ToolTip.SetTip(this, description);
        ToolTip.SetTip(ActionsButton, description + "\nClick for repository actions.");
        AutomationProperties.SetName(ActionsButton, "Repository actions · " + value.Branch);
        AutomationProperties.SetName(this, description);
    }

    private void AddStatus(string text, IBrush foreground)
    {
        var token = (statusText.Length == 0 ? "  " : " ") + text;
        statusText += token;
        state.Inlines!.Add(new Run(token) { Foreground = foreground });
    }
}

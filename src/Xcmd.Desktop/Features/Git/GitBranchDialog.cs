// @implements SeeRepositoryState
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Git;

internal sealed class GitBranchDialog : Window
{
    private readonly ListBox branches = new ListBox().WithFeedbackId("GitBranchDialog.branches");
    private readonly Button confirm = new Button
    {
        Content = new AccessText { Text = "_Switch", ShowAccessKey = true }.WithFeedbackId("GitBranchDialog.switchLabel")
    }.WithFeedbackId("GitBranchDialog.switch");
    private readonly TextBlock error = UiLayout.Error();
    private bool switching;

    public event Action<string>? SwitchRequested;

    public GitBranchDialog(GitStatus status, GitBranchTip[] tips, string? selectedBranch = null)
    {
        Title = "Switch local branch";
        Width = 500; Height = 420; MinWidth = 360; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var current = new TextBlock { Text = "Current: " + status.Branch, TextTrimming = TextTrimming.CharacterEllipsis }
            .WithFeedbackId("GitBranchDialog.current");
        ToolTip.SetTip(current, status.Root + "\n" + status.Branch);
        var cancel = new Button
        {
            Content = new AccessText { Text = "_Cancel", ShowAccessKey = true }.WithFeedbackId("GitBranchDialog.cancelLabel"),
            IsCancel = true
        }.WithFeedbackId("GitBranchDialog.cancel");
        branches.ItemTemplate = new FuncDataTemplate<GitBranchTip>((tip, _) => BranchRow(tip));
        branches.ItemsSource = tips;
        confirm.IsEnabled = false;
        branches.SelectionChanged += (_, _) =>
        {
            confirm.IsEnabled = branches.SelectedItem is GitBranchTip tip && tip.Name != status.Branch;
            error.Text = null;
        };
        branches.SelectedItem = tips.FirstOrDefault(tip => tip.Name == (selectedBranch ?? status.Branch));
        confirm.Click += (_, _) => RequestSwitch();
        branches.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None) return;
            e.Handled = true;
            RequestSwitch();
        }, RoutingStrategies.Tunnel);
        cancel.Click += (_, _) => Close();
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 8, Margin = UiPalette.FormMargin }
            .WithFeedbackId("GitBranchDialog.layout");
        var details = new ScrollViewer { MaxHeight = 110, Content = error }.WithFeedbackId("GitBranchDialog.details");
        Control list = tips.Length > 0 ? branches :
            new TextBlock { Text = "This repository has no local branches yet." }.WithFeedbackId("GitBranchDialog.help");
        var rows = new Control[] { current, list, details, UiLayout.Actions(confirm, cancel) };
        for (var i = 0; i < rows.Length; i++) { Grid.SetRow(rows[i], i); grid.Children.Add(rows[i]); }
        Content = grid;
        DialogActions.FollowFocus(this, confirm, cancel);
        Opened += (_, _) => Dispatcher.UIThread.Post(FocusSelection, DispatcherPriority.Input);
        Closing += (_, e) => { if (switching) e.Cancel = true; };
    }

    private static Grid BranchRow(GitBranchTip tip)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 }
            .WithFeedbackId("GitBranchDialog.row");
        var name = new TextBlock { Text = tip.Name, TextTrimming = TextTrimming.CharacterEllipsis }
            .WithFeedbackId("GitBranchDialog.name");
        ToolTip.SetTip(name, tip.Name);
        var date = new TextBlock { Text = tip.CommitDate, Foreground = UiPalette.Secondary }
            .WithFeedbackId("GitBranchDialog.date");
        ToolTip.SetTip(date, "Last commit");
        Grid.SetColumn(date, 1);
        row.Children.Add(name);
        row.Children.Add(date);
        return row;
    }

    private void RequestSwitch()
    {
        if (!switching && confirm.IsEnabled && branches.SelectedItem is GitBranchTip tip) SwitchRequested?.Invoke(tip.Name);
    }

    private void FocusSelection()
    {
        var index = Math.Max(0, branches.SelectedIndex);
        if (index < branches.ItemCount) branches.ScrollIntoView(index);
        (branches.ContainerFromIndex(index) ?? branches).Focus();
    }

    public void ShowError(string message) => error.Text = message;

    public void SetSwitching(bool value)
    {
        switching = value;
        IsEnabled = !value;
        if (!value) FocusSelection();
    }
}

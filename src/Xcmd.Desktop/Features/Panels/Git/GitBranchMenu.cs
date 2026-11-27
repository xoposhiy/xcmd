// @implements SeeRepositoryState
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Panels.Git;

internal static class GitBranchMenu
{
    private static readonly FuncDataTemplate<GitBranchTip> Label = new((tip, _) =>
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinWidth = 255, MaxWidth = 420 }
            .WithFeedbackId("GitBranchMenu.row");
        var name = new TextBlock { Text = tip.Name, TextTrimming = TextTrimming.CharacterEllipsis }
            .WithFeedbackId("GitBranchMenu.name");
        var date = new TextBlock { Text = tip.CommitDate, Foreground = UiPalette.Secondary, Margin = new Thickness(12, 0, 0, 0) }
            .WithFeedbackId("GitBranchMenu.date");
        Grid.SetColumn(date, 1);
        row.Children.Add(name);
        row.Children.Add(date);
        return row;
    });

    public static MenuItem Create(Action<string> switchBranch)
    {
        var item = new MenuItem { IsVisible = false, ToggleType = MenuItemToggleType.Radio, HeaderTemplate = Label }
            .WithFeedbackId("GitBranchMenu.item");
        item.Click += (_, _) =>
        {
            if (item.IsEnabled && item.Header is GitBranchTip tip) switchBranch(tip.Name);
        };
        return item;
    }

    public static void Show(MenuItem[] items, GitStatus? status)
    {
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var tip = status?.RecentBranches.ElementAtOrDefault(i);
            item.Header = tip;
            item.IsVisible = tip != null;
            item.IsChecked = tip != null && tip.Name == status?.Branch;
            item.IsEnabled = tip != null && !item.IsChecked;
            AutomationProperties.SetName(item, tip?.ToString() ?? "");
            ToolTip.SetTip(item, tip == null ? null : $"{tip.Name}\nLast commit: {tip.CommitDate}");
        }
    }
}

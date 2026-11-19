// @implements ReadablePanels, RecentLocations, SearchResultPanel
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels.Git;

namespace Xcmd.Desktop.Features.Panels;

internal sealed class PanelLocationHeader
{
    private readonly TextBlock path = new TextBlock() { FontSize = UiPalette.BodySize, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.LeadingCharacterEllipsis }.WithFeedbackId("PanelView.path");
    private readonly GitStatusView git;

    public Border Border { get; }
    public event Action? HistoryRequested;

    public PanelLocationHeader(string id, GitStatusView git)
    {
        this.git = git;
        var historyButton = new Button
        {
            Content = "History",
            Name = id + "History",
            FontSize = UiPalette.MetaSize,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = UiPalette.Secondary,
            Padding = new Thickness(3, 4),
            Margin = new Thickness(8, 0, 0, 0)
        }.WithFeedbackId("PanelView.historyButton");
        historyButton.Classes.Add("quiet");
        AutomationProperties.SetAutomationId(historyButton, id + ".history");
        ToolTip.SetTip(historyButton, "Directory history (Alt+Down)");
        historyButton.Click += (_, _) => HistoryRequested?.Invoke();
        var pathHeader = new Grid { Name = id + "LocationHeader", ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 8 }.WithFeedbackId("PanelView.pathHeader");
        pathHeader.Children.Add(path);
        Grid.SetColumn(git, 1);
        pathHeader.Children.Add(git);
        Grid.SetColumn(historyButton, 3);
        pathHeader.Children.Add(historyButton);
        void FitLocationHeader()
        {
            git.MaxWidth = Math.Clamp(pathHeader.Bounds.Width * 0.5, 120, 300);
            path.MaxWidth = Math.Max(60, pathHeader.Bounds.Width - git.DesiredSize.Width - historyButton.DesiredSize.Width - 24);
        }
        pathHeader.SizeChanged += (_, _) => FitLocationHeader();
        git.SizeChanged += (_, _) => FitLocationHeader();
        path.VerticalAlignment = VerticalAlignment.Center;
        Border = new Border { Padding = new Thickness(8, 0), Child = pathHeader, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("PanelView.pathContainer");
    }

    public static string DisplayPath(PanelSession session) =>
        PathText.Display(session.FileSystem is GitFileSystem git ? git.Display(session.DirectoryPath) : session.DirectoryPath);

    public void ShowDirectory(PanelSession session)
    {
        path.Text = DisplayPath(session);
        path.TextTrimming = TextTrimming.LeadingCharacterEllipsis;
        ToolTip.SetTip(path, path.Text);
    }

    public void ShowSearch(string root)
    {
        path.Text = "Search results · " + PathText.Display(root);
        git.SetDirectory("");
        ToolTip.SetTip(path, path.Text + "\nEscape returns to the previous folder. Ctrl+Left/Right locates the current item.");
    }
}

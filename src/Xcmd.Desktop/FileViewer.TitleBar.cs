// @implements QuickViewFile, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Xcmd.Desktop;

public sealed partial class FileViewer
{
    private Grid BuildTitleBar()
    {
        TitleBar.Children.Add(titlePath);
        TitleBar.Children.Add(Progress);
        UpdateDocumentTitle();
        if (IsEmbedded) ConfigureQuickViewHeader();
        else
        {
            TitleBar.Children.Add(Commands);
            Grid.SetColumn(titlePath, 1);
            Grid.SetColumn(Progress, 2);
        }
        return TitleBar;
    }

    private void ConfigureQuickViewHeader()
    {
        TitleBar.Height = double.NaN;
        TitleBar.ColumnDefinitions = new ColumnDefinitions("*,Auto");
        TitleBar.RowDefinitions = new RowDefinitions("32,28");
        Grid.SetRow(titlePath, 1);
        Grid.SetRow(Progress, 1); Grid.SetColumn(Progress, 1);
        Progress.VerticalAlignment = VerticalAlignment.Center;

        var menuRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }.WithFeedbackId("FileViewer.menuRow");
        menuRow.Children.Add(Commands);
        var closeButton = new Button
        {
            Name = "QuickViewClose",
            Content = "×",
            Focusable = false,
            Width = 34,
            Height = 30,
            MinWidth = 0,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        }.WithFeedbackId("QuickViewPane.close");
        closeButton.Classes.Add("quiet");
        AutomationProperties.SetName(closeButton, "Close quick view");
        ToolTip.SetTip(closeButton, "Close quick view (Ctrl+Q)");
        closeButton.Click += (_, _) => close();
        Grid.SetColumn(closeButton, 1);
        menuRow.Children.Add(closeButton);
        Grid.SetColumnSpan(menuRow, 2);
        TitleBar.Children.Add(menuRow);
    }

    private void ConfigureReaderAppearance()
    {
        TextContent.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        foreach (var state in new[] { "Focused", "PointerOver", "Disabled" })
            TextContent.Resources["TextControlBorderBrush" + state] = Brushes.Transparent;
        TextContent.BorderBrush = Brushes.Transparent;
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Xcmd.Desktop;

public sealed partial class FileViewerWindow
{
    private Grid BuildTitleBar()
    {
        WindowDecorationProperties.SetElementRole(TitleBar, WindowDecorationsElementRole.TitleBar);
        WindowDecorationProperties.SetElementRole(Commands, WindowDecorationsElementRole.User);
        TitleBar.Children.Add(Commands);
        titlePath.Text = document?.Caption ?? Path.GetFileName(CurrentPath);
        ToolTip.SetTip(titlePath, DocumentCaption);
        Grid.SetColumn(titlePath, 1); TitleBar.Children.Add(titlePath);
        Grid.SetColumn(Progress, 2); TitleBar.Children.Add(Progress);
        var caption = WindowHeader.CaptionButtons(this, "Viewer");
        Grid.SetColumn(caption, 3); TitleBar.Children.Add(caption);
        return TitleBar;
    }

    private void ConfigureReaderAppearance()
    {
        TextContent.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        foreach (var state in new[] { "Focused", "PointerOver", "Disabled" })
            TextContent.Resources["TextControlBorderBrush" + state] = Brushes.Transparent;
        TextContent.BorderBrush = Brushes.Transparent;
    }
}

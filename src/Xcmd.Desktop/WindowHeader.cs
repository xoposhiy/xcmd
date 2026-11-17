// @implements SharedControls
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Xcmd.Desktop;

internal static class WindowHeader
{
    public static void Configure(Window window)
    {
        window.WindowDecorations = WindowDecorations.BorderOnly;
        window.ExtendClientAreaToDecorationsHint = true;
        window.ExtendClientAreaTitleBarHeightHint = 32;
    }

    public static StackPanel CaptionButtons(Window window, string prefix)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }.WithFeedbackId("WindowHeader.buttons");
        buttons.Children.Add(Button("—", "Minimize", prefix + "Minimize", WindowDecorationsElementRole.MinimizeButton, () => window.WindowState = WindowState.Minimized));
        buttons.Children.Add(Button("□", "Maximize or restore", prefix + "Maximize", WindowDecorationsElementRole.MaximizeButton,
            () => window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
        buttons.Children.Add(Button("×", "Close", prefix + "CloseWindow", WindowDecorationsElementRole.CloseButton, window.Close));
        return buttons;
    }

    private static Button Button(string symbol, string label, string name, WindowDecorationsElementRole role, Action action)
    {
        var button = new Button
        {
            Content = symbol,
            Name = name,
            Width = 36,
            Height = 32,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 16,
            Focusable = false
        }.WithFeedbackId("WindowHeader.button");
        AutomationProperties.SetAutomationId(button, name);
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        WindowDecorationProperties.SetElementRole(button, role);
        button.Click += (_, _) => action();
        return button;
    }
}

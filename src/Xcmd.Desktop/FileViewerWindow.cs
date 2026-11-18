// @implements OpenViewers, ReadWorkingFileDiff
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileViewerWindow : Window
{
    private const uint KeyDownMessage = 0x0100;
    private const uint SystemKeyDownMessage = 0x0104;

    public FileViewer Reader { get; }

    public FileViewerWindow(string[] paths, int index, IViewerFiles files, FeedbackController feedback,
        Func<string, Task> edit, Func<string, Task> open, string themeId = "dark", ViewerDocument? document = null)
    {
        Reader = new FileViewer(paths, index, files, feedback, edit, open, Close, document).WithFeedbackId("FileViewerWindow.reader");
        Width = 1120; Height = 760; MinWidth = 640; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowHeader.Configure(this);
        AutomationProperties.SetAutomationId(this, "viewer.window");
        WindowDecorationProperties.SetElementRole(Reader.TitleBar, WindowDecorationsElementRole.TitleBar);
        WindowDecorationProperties.SetElementRole(Reader.Commands, WindowDecorationsElementRole.User);
        var caption = WindowHeader.CaptionButtons(this, "Viewer");
        Grid.SetColumn(caption, 3);
        Reader.TitleBar.Children.Add(caption);
        Reader.ApplyTheme(themeId);
        SetCaption(Reader.DocumentCaption);
        Reader.CaptionChanged += SetCaption;
        AddHandler(KeyDownEvent, Reader.HandleDiffKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, Reader.ReleaseDiffKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        Win32Properties.AddWndProcHookCallback(this, RestoreKeyboardFocus);
        Activated += (_, _) => Reader.RefreshAfterActivation();
        Deactivated += (_, _) => Reader.ResetKeys();
        Closed += (_, _) => Reader.Stop();
        Content = Reader;
    }

    private nint RestoreKeyboardFocus(nint window, uint message, nint wParam, nint lParam, ref bool handled)
    {
        // Windows can send a key here while Avalonia still routes it to a different window.
        if (message is (KeyDownMessage or SystemKeyDownMessage) && IsActive && !IsKeyboardFocusWithin && !Reader.Commands.IsOpen)
        {
            Reader.ResetKeys();
            Reader.FocusReader();
        }
        return 0;
    }

    internal void FitInitialSize(Window owner)
    {
        var screen = owner.Screens.ScreenFromWindow(owner) ?? Screens.Primary;
        if (screen == null) return;
        Width = Math.Clamp(screen.WorkingArea.Width / screen.Scaling - 24, MinWidth, Width);
        Height = Math.Clamp(screen.WorkingArea.Height / screen.Scaling - 24, MinHeight, Height);
    }

    private void SetCaption(string caption) => Title = caption + " — Xcmd Viewer";
}

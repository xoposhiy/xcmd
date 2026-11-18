// @implements OpenViewers, SharedControls
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileViewerWindow : Window
{
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
        Deactivated += (_, _) => Reader.ResetKeys();
        Closed += (_, _) => Reader.Stop();
        Content = Reader;
    }

    private void SetCaption(string caption) => Title = caption + " — Xcmd Viewer";
}

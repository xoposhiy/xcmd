using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileTransferDialog : Window
{
    public TextBox Destination { get; } = new() { Name = "TransferDestination" };
    public TextBlock Error { get; } = new() { Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap };
    public bool IsSubmitting { get; set; }
    public event Action<string>? Submitted;

    public FileTransferDialog(FileOperationKind kind, FileEntry[] sources, string directory, FeedbackController feedback, bool copyHere = false)
    {
        var destination = copyHere && sources.Length == 1 ? Path.Combine(directory, FileOperationNames.CopyName(sources[0]))
            : Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        Title = kind == FileOperationKind.Copy ? "Copy" : "Move";
        Width = 620; MinWidth = 440; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Destination.Text = destination;
        AutomationProperties.SetAutomationId(Destination, "file-transfer.destination");
        var source = new TextBlock
        {
            Text = $"{Title} {(sources.Length == 1 ? sources[0].Name : $"{sources.Length} items")} to:",
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ToolTip.SetTip(source, string.Join("\n", sources.Select(entry => entry.FullPath)));
        var confirm = new Button { Content = Title, Name = "ConfirmFileTransfer", IsDefault = true };
        var cancel = new Button { Content = "Cancel", Name = "CancelFileTransfer", Margin = new Thickness(8, 0, 0, 0) };
        confirm.Click += (_, _) => Submit(); cancel.Click += (_, _) => Close();
        DialogActions.FollowFocus(this, confirm, cancel);
        Content = new StackPanel
        {
            Margin = new Thickness(14),
            Spacing = 8,
            Children =
            {
                source, Destination, Error,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { confirm, cancel } }
            }
        };
        Opened += (_, _) =>
        {
            Destination.Focus();
            if (Path.EndsInDirectorySeparator(destination)) Destination.SelectAll();
            else { Destination.SelectionStart = destination.Length - Path.GetFileName(destination).Length; Destination.SelectionEnd = destination.Length; }
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback.IsPickingIn(this) || feedback.ActiveComment != null) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && Destination.IsKeyboardFocusWithin) { Submit(); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }

    private void Submit() { if (!IsSubmitting) Submitted?.Invoke(Destination.Text ?? ""); }
}

// @implements CreateArchive
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Xcmd.Desktop.Features.FileOperations;

public sealed class ZipNameDialog : Window
{
    public TextBox Editor { get; } = new TextBox() { Name = "ZipName" }.WithFeedbackId("ZipNameDialog.Editor");
    public TextBlock Error { get; } = UiLayout.Error();
    public event Action<string>? Submitted;

    public ZipNameDialog(string directory, int count, string name, Feedback.FeedbackController feedback)
    {
        Title = "Create ZIP"; Width = 520; MinWidth = 440; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Editor.Text = name;
        AutomationProperties.SetAutomationId(Editor, "zip.name");
        var destination = new TextBlock { Text = directory, TextTrimming = TextTrimming.LeadingCharacterEllipsis }.WithFeedbackId("ZipNameDialog.destination");
        ToolTip.SetTip(destination, directory);
        var create = new Button { Content = new AccessText { Text = "_Create ZIP", ShowAccessKey = true }.WithFeedbackId("ZipNameDialog.create"), Name = "CreateZip", IsDefault = true }.WithFeedbackId("ZipNameDialog.create.2");
        var cancel = new Button { Content = new AccessText { Text = "Ca_ncel", ShowAccessKey = true }.WithFeedbackId("ZipNameDialog.cancel"), Name = "CancelZip" }.WithFeedbackId("ZipNameDialog.cancel.2");
        AutomationProperties.SetAutomationId(create, "zip.create");
        AutomationProperties.SetAutomationId(cancel, "zip.cancel");
        create.Click += (_, _) => Submitted?.Invoke(Editor.Text ?? "");
        cancel.Click += (_, _) => Close();
        DialogActions.FollowFocus(this, create, cancel);
        Content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children = { new TextBlock { Text = $"Add {count:N0} selected item(s) to a new ZIP in:" }.WithFeedbackId("ZipNameDialog.content"), destination,
                new TextBlock { Text = "Archive name" }.WithFeedbackId("ZipNameDialog.content.2"), Editor, Error, UiLayout.Actions(create, cancel) }
        }.WithFeedbackId("ZipNameDialog.content.3");
        Opened += (_, _) => { Editor.Focus(); Editor.SelectionStart = 0; Editor.SelectionEnd = name.Length - 4; };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback.IsPickingIn(this) || feedback.ActiveComment != null) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.N) { Close(); e.Handled = true; }
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.C) { Submitted?.Invoke(Editor.Text ?? ""); e.Handled = true; }
            else if (e.Key == Key.Enter && Editor.IsKeyboardFocusWithin) { Submitted?.Invoke(Editor.Text ?? ""); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }
}

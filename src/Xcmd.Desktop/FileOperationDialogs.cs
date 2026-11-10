using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileConflictDialog : Window
{
    public CheckBox ApplyToAll { get; } = new() { Content = "Apply to all", Name = "ConflictApplyToAll" };
    public TextBox NewName { get; } = new() { Name = "ConflictNewName" };
    public TextBlock Error { get; } = new() { Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap };
    public event Action<ConflictDecision>? Decided;

    public FileConflictDialog(FileConflict conflict, FeedbackController feedback)
    {
        Title = "Name conflict"; Width = 620; MinWidth = 440; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var replaceAllowed = !conflict.Source.IsDirectory && !conflict.Destination.IsDirectory && !conflict.Destination.IsLink;
        NewName.Text = FileOperationNames.CopyName(conflict.Source);
        AutomationProperties.SetAutomationId(NewName, "file-conflict.new-name");
        ToolTip.SetTip(ApplyToAll, "Apply Skip or Replace to matching conflicts in this operation.");
        var comparison = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        comparison.Children.Add(Describe("Source", conflict.Source));
        var target = Describe("Destination", conflict.Destination); target.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(target, 1); comparison.Children.Add(target);
        var nameRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        nameRow.Children.Add(new TextBlock { Text = "New name", Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(NewName, 1); nameRow.Children.Add(NewName);
        var skip = Choice("Skip", "ConflictSkip", ConflictAction.Skip);
        var replace = Choice("Replace", "ConflictReplace", ConflictAction.Replace); replace.IsEnabled = replaceAllowed;
        var rename = Choice("Rename", "ConflictRename", ConflictAction.Rename);
        var cancel = Choice("Cancel operation", "ConflictCancel", ConflictAction.Cancel);
        Content = new StackPanel
        {
            Margin = new Thickness(14),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = conflict.Source.Name + " already exists", FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                comparison, ApplyToAll, nameRow, Error,
                new WrapPanel { Orientation = Orientation.Horizontal, Children = { skip, rename, replace, cancel } }
            }
        };
        var choices = new[] { skip, rename, replace, cancel };
        void Default(Button selected) { foreach (var button in choices) button.IsDefault = button == selected; }
        Default(skip);
        NewName.GotFocus += (_, _) => Default(rename);
        foreach (var button in choices) button.GotFocus += (_, _) => Default(button);
        Opened += (_, _) => skip.Focus();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback.IsPickingIn(this) || feedback.ActiveComment != null) return;
            if (e.Key == Key.Escape) { Decided?.Invoke(new(ConflictAction.Cancel)); e.Handled = true; }
            else if (e.Key == Key.Enter && NewName.IsFocused) { Decide(ConflictAction.Rename); e.Handled = true; }
        }, RoutingStrategies.Tunnel);

        Button Choice(string label, string name, ConflictAction action)
        {
            var button = new Button { Content = label, Name = name, Margin = new Thickness(0, 0, 8, 4) };
            AutomationProperties.SetAutomationId(button, "file-conflict." + action.ToString().ToLowerInvariant());
            button.Click += (_, _) => Decide(action);
            return button;
        }
    }

    private static StackPanel Describe(string label, FileEntry entry)
    {
        var path = new TextBlock { Text = Path.GetDirectoryName(entry.FullPath), TextTrimming = TextTrimming.LeadingCharacterEllipsis };
        ToolTip.SetTip(path, entry.FullPath);
        return new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = label, Foreground = Brushes.LightGray }, path,
                new TextBlock { Text = entry.IsDirectory ? "Folder" : $"{entry.Size:N0} bytes" },
                new TextBlock { Text = $"{entry.ModifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}" }
            }
        };
    }

    private void Decide(ConflictAction action)
    {
        if (action == ConflictAction.Rename && PanelRules.ValidateName(NewName.Text ?? "") is { } error)
        { Error.Text = error; NewName.Focus(); return; }
        Decided?.Invoke(new(action, NewName.Text, action != ConflictAction.Rename && ApplyToAll.IsChecked == true));
    }
}

public sealed class FileDeleteDialog : Window
{
    public RadioButton Recycle { get; } = new() { Content = new AccessText { Text = "_Recycle Bin", ShowAccessKey = true }, Name = "DeleteToRecycleBin", GroupName = "DeleteMode" };
    public RadioButton Permanently { get; } = new() { Content = new AccessText { Text = "_Permanently", ShowAccessKey = true }, Name = "DeletePermanently", GroupName = "DeleteMode", Margin = new Thickness(16, 0, 0, 0) };
    private readonly TextBlock question = new() { FontWeight = FontWeight.SemiBold };
    public FileOperationKind SelectedKind => Permanently.IsChecked == true ? FileOperationKind.Delete : FileOperationKind.Recycle;
    public event Action<FileOperationKind>? Confirmed;

    public FileDeleteDialog(FileOperationRequest request, FeedbackController feedback)
    {
        Title = "Delete";
        Width = 460; MinWidth = 440; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Permanently.IsChecked = request.Kind == FileOperationKind.Delete;
        Recycle.IsChecked = !Permanently.IsChecked;
        UpdateQuestion();
        Recycle.PropertyChanged += (_, e) => { if (e.Property == RadioButton.IsCheckedProperty) UpdateQuestion(); };
        Permanently.PropertyChanged += (_, e) => { if (e.Property == RadioButton.IsCheckedProperty) UpdateQuestion(); };
        var confirm = new Button
        {
            Content = "Yes",
            Name = "ConfirmFileDelete",
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var cancel = new Button { Content = "Cancel", Name = "CancelFileDelete" };
        DialogActions.FollowFocus(this, confirm, cancel);
        AutomationProperties.SetAutomationId(confirm, "file-delete.confirm");
        confirm.Click += (_, _) => Confirmed?.Invoke(SelectedKind);
        cancel.Click += (_, _) => Close();
        var selection = new TextBlock { Text = request.Sources.Length == 1 ? request.Sources[0].Name : $"{request.Sources.Length} items", TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(selection, string.Join("\n", request.Sources.Select(entry => entry.FullPath)));
        Content = new StackPanel
        {
            Margin = new Thickness(14),
            Spacing = 10,
            Children =
            {
                question, selection,
                new StackPanel { Orientation = Orientation.Horizontal, Children = { Recycle, Permanently } },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { confirm, cancel } }
            }
        };
        Opened += (_, _) => confirm.Focus();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback.IsPickingIn(this) || feedback.ActiveComment != null) return;
            if (e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.R or Key.P)
            {
                var option = e.Key == Key.R ? Recycle : Permanently;
                option.IsChecked = true; option.Focus(); e.Handled = true;
            }
            else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && FocusManager?.GetFocusedElement() is RadioButton) { Confirmed?.Invoke(SelectedKind); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }

    private void UpdateQuestion() => question.Text = SelectedKind == FileOperationKind.Delete ? "Delete permanently?" : "Move to Recycle Bin?";
}

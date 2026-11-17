// @implements DeleteEntries, ResolveConflicts
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
    public CheckBox ApplyToAll { get; } = new CheckBox() { Content = new AccessText { Text = "_Apply to all", ShowAccessKey = true }.WithFeedbackId("FileOperationDialogs.applyLabel"), Name = "ConflictApplyToAll" }.WithFeedbackId("FileOperationDialogs.ApplyToAll");
    public TextBox NewName { get; } = new TextBox() { Name = "ConflictNewName" }.WithFeedbackId("FileOperationDialogs.NewName");
    public TextBlock Error { get; } = UiLayout.Error();
    public event Action<ConflictDecision>? Decided;

    public FileConflictDialog(FileConflict conflict, FeedbackController feedback)
    {
        Title = "Name conflict"; Width = 620; MinWidth = 440; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var replaceAllowed = !conflict.Source.IsDirectory && !conflict.Destination.IsDirectory && !conflict.Destination.IsLink;
        NewName.Text = FileOperationNames.CopyName(conflict.Source);
        AutomationProperties.SetAutomationId(NewName, "file-conflict.new-name");
        ToolTip.SetTip(ApplyToAll, "Apply Skip or Replace to matching conflicts in this operation.");
        var comparison = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") }.WithFeedbackId("FileOperationDialogs.comparison");
        comparison.Children.Add(Describe("Source", conflict.Source));
        var target = Describe("Destination", conflict.Destination); target.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(target, 1); comparison.Children.Add(target);
        var nameRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") }.WithFeedbackId("FileOperationDialogs.nameRow");
        nameRow.Children.Add(new TextBlock { Text = "New name", Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("FileOperationDialogs.content"));
        Grid.SetColumn(NewName, 1); nameRow.Children.Add(NewName);
        var skip = Choice("_Skip", "ConflictSkip", ConflictAction.Skip);
        var replace = Choice("_Replace", "ConflictReplace", ConflictAction.Replace); replace.IsEnabled = replaceAllowed;
        var rename = Choice("Rena_me", "ConflictRename", ConflictAction.Rename);
        var cancel = Choice("Ca_ncel operation", "ConflictCancel", ConflictAction.Cancel);
        ApplyToAll.IsCheckedChanged += (_, _) =>
        {
            ((AccessText)skip.Content!).Text = ApplyToAll.IsChecked == true ? "_Skip all" : "_Skip";
            ((AccessText)replace.Content!).Text = ApplyToAll.IsChecked == true ? "_Replace all" : "_Replace";
        };
        Content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = conflict.Source.Name + " already exists", FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }.WithFeedbackId("FileOperationDialogs.content.2"),
                comparison, ApplyToAll, nameRow, Error,
                UiLayout.Actions(skip, rename, replace, cancel)
            }
        }.WithFeedbackId("FileOperationDialogs.content.3");
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
            else if (e.KeyModifiers == KeyModifiers.Alt) e.Handled = HandleAccessKey(e.Key, replaceAllowed);
        }, RoutingStrategies.Tunnel);

        Button Choice(string label, string name, ConflictAction action)
        {
            var button = new Button { Content = new AccessText { Text = label, ShowAccessKey = true }.WithFeedbackId("FileOperationDialogs.choiceLabel"), Name = name, Margin = new Thickness(0, 0, 8, 4) }.WithFeedbackId("FileOperationDialogs.button");
            AutomationProperties.SetAutomationId(button, "file-conflict." + action.ToString().ToLowerInvariant());
            button.Click += (_, _) => Decide(action);
            return button;
        }
    }

    private bool HandleAccessKey(Key key, bool replaceAllowed)
    {
        if (key == Key.A) { ApplyToAll.IsChecked = ApplyToAll.IsChecked != true; ApplyToAll.Focus(); return true; }
        ConflictAction? action = key switch
        {
            Key.R when replaceAllowed => ConflictAction.Replace,
            Key.S => ConflictAction.Skip,
            Key.M => ConflictAction.Rename,
            Key.N => ConflictAction.Cancel,
            _ => null
        };
        if (action is not { } choice) return false;
        Decide(choice); return true;
    }

    private static StackPanel Describe(string label, FileEntry entry)
    {
        var path = new TextBlock { Text = Path.GetDirectoryName(entry.FullPath), TextTrimming = TextTrimming.LeadingCharacterEllipsis }.WithFeedbackId("FileOperationDialogs.path");
        ToolTip.SetTip(path, entry.FullPath);
        return new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = label, Foreground = UiPalette.Secondary }.WithFeedbackId("FileOperationDialogs.Describe"), path,
                new TextBlock { Text = entry.IsDirectory ? "Folder" : $"{entry.Size:N0} bytes" }.WithFeedbackId("FileOperationDialogs.Describe.2"),
                new TextBlock { Text = $"{entry.ModifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}" }.WithFeedbackId("FileOperationDialogs.Describe.3")
            }
        }.WithFeedbackId("FileOperationDialogs.Describe.4");
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
    public RadioButton Recycle { get; } = new RadioButton() { Content = new AccessText { Text = "_Recycle Bin", ShowAccessKey = true }.WithFeedbackId("FileOperationDialogs.Recycle"), Name = "DeleteToRecycleBin", GroupName = "DeleteMode" }.WithFeedbackId("FileOperationDialogs.Recycle.2");
    public RadioButton Permanently { get; } = new RadioButton() { Content = new AccessText { Text = "_Permanently", ShowAccessKey = true }.WithFeedbackId("FileOperationDialogs.Permanently"), Name = "DeletePermanently", GroupName = "DeleteMode", Margin = new Thickness(16, 0, 0, 0) }.WithFeedbackId("FileOperationDialogs.Permanently.2");
    private readonly TextBlock question = new TextBlock() { FontWeight = FontWeight.SemiBold }.WithFeedbackId("FileOperationDialogs.question");
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
            Content = new AccessText { Text = "_Yes", ShowAccessKey = true }.WithFeedbackId("FileOperationDialogs.confirmLabel"),
            Name = "ConfirmFileDelete",
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0)
        }.WithFeedbackId("FileOperationDialogs.confirm");
        var cancel = new Button { Content = new AccessText { Text = "Ca_ncel", ShowAccessKey = true }.WithFeedbackId("FileOperationDialogs.cancelLabel"), Name = "CancelFileDelete" }.WithFeedbackId("FileOperationDialogs.cancel");
        DialogActions.FollowFocus(this, confirm, cancel);
        AutomationProperties.SetAutomationId(confirm, "file-delete.confirm");
        confirm.Click += (_, _) => Confirmed?.Invoke(SelectedKind);
        cancel.Click += (_, _) => Close();
        var selection = new TextBlock { Text = request.Sources.Length == 1 ? request.Sources[0].Name : $"{request.Sources.Length} items", TextTrimming = TextTrimming.CharacterEllipsis }.WithFeedbackId("FileOperationDialogs.selection");
        ToolTip.SetTip(selection, string.Join("\n", request.Sources.Select(entry => entry.FullPath)));
        Content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 10,
            Children =
            {
                question, selection,
                new StackPanel { Orientation = Orientation.Horizontal, Children = { Recycle, Permanently } }.WithFeedbackId("FileOperationDialogs.content.4"),
                UiLayout.Actions(confirm, cancel)
            }
        }.WithFeedbackId("FileOperationDialogs.content.5");
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
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Y) { Confirmed?.Invoke(SelectedKind); e.Handled = true; }
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.N) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && FocusManager?.GetFocusedElement() is RadioButton) { Confirmed?.Invoke(SelectedKind); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }

    private void UpdateQuestion() => question.Text = SelectedKind == FileOperationKind.Delete ? "Delete permanently?" : "Move to Recycle Bin?";
}

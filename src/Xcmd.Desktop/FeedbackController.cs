using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FeedbackController
{
    private readonly CommanderWindow owner;
    private readonly Dictionary<Window, FeedbackSurface> surfaces = [];
    private readonly List<CommentDialog> comments = [];
    public ObservableCollection<FeedbackNote> Notes { get; } = [];
    public bool IsPicking => surfaces.Values.Any(surface => surface.IsPicking);
    public CommentDialog? ActiveComment => comments.LastOrDefault();
    public Window? ActiveNotes { get; private set; }
    public string ReleaseId { get; set; } = "development";
    public string? ListPath { get; set; }
    public bool IsActive => IsPicking || ActiveComment != null || ActiveNotes != null;
    public event Func<Task>? Changed;

    public FeedbackController(CommanderWindow owner, Grid content)
    {
        this.owner = owner;
        surfaces[owner] = new FeedbackSurface(this, owner, content);
    }

    public bool IsPickingIn(Window window) => surfaces.TryGetValue(window, out var surface) && surface.IsPicking;
    internal void SetHint(string text) => owner.SetUpdateMessage(text);
    internal void StopNameSearch() => owner.StopNameSearch();

    public void Attach(Window window)
    {
        if (surfaces.ContainsKey(window)) return;
        window.Icon ??= owner.Icon;
        var original = window.Content as Control;
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var button = new Button { Content = "Feedback  F12", Name = "DialogFeedback", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(6), Padding = new Thickness(8, 3) };
        AutomationProperties.SetAutomationId(button, "feedback.start");
        button.Click += (_, _) => Begin(window);
        layout.Children.Add(button);
        window.Content = null;
        if (original != null) { Grid.SetRow(original, 1); layout.Children.Add(original); }
        var root = new Grid(); root.Children.Add(layout); window.Content = root;
        if (double.IsFinite(window.Height)) window.Height += 40;
        surfaces[window] = new FeedbackSurface(this, window, root);
        window.Closed += (_, _) => surfaces.Remove(window);
    }

    public void Begin(Window? window = null) => surfaces[window ?? owner].Begin();
    public void Cancel() { foreach (var surface in surfaces.Values) surface.Cancel(); }

    public void Restore(IEnumerable<FeedbackNote> notes)
    {
        Notes.Clear();
        foreach (var note in notes) Notes.Add(note);
    }

    internal static Control? Target(Control? source)
    {
        var ancestors = source?.GetSelfAndVisualAncestors().OfType<Control>().ToArray() ?? [];
        return ancestors.FirstOrDefault(control => control is Button or TextBox or TableViewRow or TableViewColumnHeader) ??
            ancestors.FirstOrDefault(control => control is TextBlock { Text.Length: > 0 } || !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(control))) ?? source;
    }

    internal void OpenComment(Window source, Control control, FeedbackSurface surface)
    {
        var previousFocus = source.FocusManager?.GetFocusedElement() as Control;
        var panel = control.GetSelfAndVisualAncestors().OfType<PanelView>().FirstOrDefault() ?? owner.ActivePanel;
        var id = AutomationProperties.GetAutomationId(control) ?? control.Name ?? control.GetType().Name;
        var name = control switch
        {
            TableViewRow { DataContext: FileRow row } => "File row: " + row.Name,
            Button { Content: AccessText text } => text.Text?.Replace("_", "", StringComparison.Ordinal) ?? id,
            Button button => button.Content?.ToString() ?? id,
            TextBlock text => text.Text ?? id,
            _ => AutomationProperties.GetName(control) ?? id
        };
        if (source != owner) name = source.Title + ": " + name;
        if (control is TableViewRow) id = panel.Id + ".files.row";
        var dialog = new CommentDialog(name, this) { Background = owner.Background };
        comments.Add(dialog);
        Attach(dialog);
        dialog.Submitted += text => owner.Execute(async () =>
        {
            Notes.Add(new(Guid.NewGuid().ToString("N"), ReleaseId, id, name, panel.Session.DirectoryPath, text.Trim(), DateTimeOffset.UtcNow));
            dialog.Close();
            owner.SetUpdateMessage("Comment added. Open Notes to view the refinement list.");
            if (Changed != null) await Changed();
        });
        dialog.Closed += (_, _) =>
        {
            comments.Remove(dialog); surface.Cancel(); owner.RecordActivity();
            if (source == owner)
            { if (owner.CommandLine.Editor == previousFocus) previousFocus.Focus(); else owner.ActivePanel.FocusCurrent(); }
            else if (source.IsVisible) previousFocus?.Focus();
        };
        _ = dialog.ShowDialog(source);
        dialog.Editor.Focus();
    }

    public void ShowNotes()
    {
        if (IsActive || owner.HasOpenDialog || owner.IsBusy) return;
        var list = new ListBox { ItemsSource = Notes, Name = "FeedbackNotes" };
        AutomationProperties.SetAutomationId(list, "feedback.notes");
        list.ItemTemplate = new FuncDataTemplate<FeedbackNote>((note, _) => note == null ? new Border() : new StackPanel
        {
            Margin = new Thickness(4, 4),
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = note!.ElementName, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = note.Text, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = note.ResolvedIn == null ? "Open · " + note.Release : "Resolved in " + note.ResolvedIn, FontSize = 11, Foreground = Brushes.LightGray },
                new TextBlock { Text = note.Resolution ?? note.PanelPath, FontSize = 11, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap }
            }
        });
        var remove = new Button { Content = "Remove selected", Name = "RemoveNote" };
        remove.Click += (_, _) => owner.Execute(async () =>
        {
            if (list.SelectedItem is FeedbackNote note) { Notes.Remove(note); if (Changed != null) await Changed(); }
        });
        var dialog = new Window
        {
            Title = "Refinement list",
            Width = 820,
            Height = Math.Clamp(160 + Math.Max(1, Notes.Count) * 110, 280, 620),
            MinWidth = 500,
            MinHeight = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.Background
        };
        var close = new Button { Content = "Close" };
        close.Click += (_, _) => dialog.Close();
        var layout = new Grid { Margin = new Thickness(12), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 8 };
        layout.Children.Add(new TextBlock { Text = "Select an element with Feedback or F12 to add a comment.", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(list, 1); layout.Children.Add(list);
        var location = new TextBlock
        {
            Text = ListPath == null ? "Comments are kept with the session." : "Saved list: " + ListPath,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(location, 2); layout.Children.Add(location);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { remove, close } };
        Grid.SetRow(buttons, 3); layout.Children.Add(buttons);
        dialog.Content = layout;
        dialog.AddHandler(InputElement.KeyDownEvent, (_, e) => { if (IsPickingIn(dialog)) return; if (e.Key == Key.Escape) { dialog.Close(); e.Handled = true; } }, RoutingStrategies.Tunnel);
        Attach(dialog);
        ActiveNotes = dialog;
        dialog.Closed += (_, _) => { ActiveNotes = null; owner.RecordActivity(); owner.ActivePanel.FocusCurrent(); };
        _ = dialog.ShowDialog(owner);
        list.Focus();
    }
}

public sealed class CommentDialog : Window
{
    public TextBox Editor { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 180, PlaceholderText = "What should change?" };
    public event Action<string>? Submitted;

    public CommentDialog(string elementName, FeedbackController? feedback = null)
    {
        Title = "Add a comment"; Width = 560; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(Editor, "feedback.comment");
        var add = new Button { Content = "Add comment", Name = "AddComment", IsEnabled = false };
        add.Classes.Add("primary");
        Editor.TextChanged += (_, _) => add.IsEnabled = !string.IsNullOrWhiteSpace(Editor.Text);
        void Submit() { if (!string.IsNullOrWhiteSpace(Editor.Text)) Submitted?.Invoke(Editor.Text); }
        add.Click += (_, _) => Submit();
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = new Thickness(14),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Comment on: " + elementName, FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis },
                Editor, new TextBlock { Text = "Ctrl+Enter adds the comment · Esc cancels", FontSize = 12 },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, add } }
            }
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback?.IsPickingIn(this) == true) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Submit(); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }
}

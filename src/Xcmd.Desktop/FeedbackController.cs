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
    public bool HasPendingSelection => surfaces.Values.Any(surface => surface.HasPendingSelection);
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
    internal void SetHint(Window source, string text)
    {
        if (source is FileViewerWindow viewer) viewer.SetFeedbackHint(text);
        else owner.SetUpdateMessage(text);
    }
    internal void StopNameSearch() => owner.StopNameSearch();

    public void Attach(Window window, bool showButton = true)
    {
        if (surfaces.ContainsKey(window)) return;
        window.Icon ??= owner.Icon;
        var original = window.Content as Control;
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        var button = new Button
        {
            Content = "Feedback  F12",
            Name = "DialogFeedback",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 12, 8),
            Padding = new Thickness(6, 0),
            FontSize = UiPalette.MetaSize,
            MinHeight = 20,
            Height = 24
        };
        button.Classes.Add("quiet");
        AutomationProperties.SetAutomationId(button, "feedback.start");
        button.Click += (_, _) => Begin(window);
        if (showButton) { Grid.SetRow(button, 1); layout.Children.Add(button); }
        window.Content = null;
        if (original != null) layout.Children.Add(original);
        var root = new Grid(); root.Children.Add(layout); window.Content = root;
        if (showButton && double.IsFinite(window.Height)) window.Height += 32;
        surfaces[window] = new FeedbackSurface(this, window, root);
        window.Closed += (_, _) => surfaces.Remove(window);
    }

    public void Begin(Window? window = null)
    {
        foreach (var surface in surfaces.Values) surface.Cancel();
        surfaces[window ?? owner].Begin();
    }
    public void Cancel() { foreach (var surface in surfaces.Values) surface.Cancel(); }

    public void Restore(IEnumerable<FeedbackNote> notes)
    {
        Notes.Clear();
        foreach (var note in notes) Notes.Add(note);
    }

    internal static Control? Target(Control? source)
    {
        var ancestors = source?.GetSelfAndVisualAncestors().OfType<Control>().ToArray() ?? [];
        return ancestors.FirstOrDefault(control => control is MenuItem or Button or TextBox or TableViewRow or TableViewColumnHeader) ??
            ancestors.FirstOrDefault(control => control is TextBlock { Text.Length: > 0 } || !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(control))) ?? source;
    }

    internal static FeedbackInteraction DescribeInteraction(Control control, string action)
    {
        var id = AutomationProperties.GetAutomationId(control) ?? control.Name ?? control.GetType().Name;
        if (control is TableViewRow && control.DataContext is not FileRow && control.GetVisualAncestors().OfType<TableView>().FirstOrDefault() is { } table)
            id = (AutomationProperties.GetAutomationId(table) ?? table.Name ?? "table") + ".row";
        var name = control switch
        {
            MenuItem menu => menu.Header?.ToString()?.Replace("_", "", StringComparison.Ordinal) ?? id,
            Button { Content: AccessText text } => text.Text?.Replace("_", "", StringComparison.Ordinal) ?? id,
            Button button => button.Content?.ToString() ?? id,
            TextBlock text => text.Text ?? id,
            TableViewRow { DataContext: FileRow row } => "File row: " + row.Name,
            TableViewRow { DataContext: FileSearchResult result } => "Search result: " + result.RelativePath,
            TableViewRow { DataContext: DeferredSearchLocation location } => location.Reason + ": " + ArchivePath.Display(location.Path),
            TableViewRow { DataContext: SearchFailure failure } => failure.Reason + ": " + ArchivePath.Display(failure.Path),
            _ => AutomationProperties.GetName(control) ?? id
        };
        return new(action, id, name);
    }

    internal void OpenComment(Window source, Control control, FeedbackSurface surface, FeedbackInteraction[] interactions)
    {
        var previousFocus = source.FocusManager?.GetFocusedElement() as Control;
        var panel = control.GetSelfAndVisualAncestors().OfType<PanelView>().FirstOrDefault() ?? owner.ActivePanel;
        var directory = source is FileViewerWindow viewer ? Path.GetDirectoryName(viewer.CurrentPath)!
            : source is FileSearchWindow search ? search.SearchDirectory : panel.Session.DirectoryPath;
        var target = DescribeInteraction(control, "Comment");
        var id = target.ElementId;
        var name = target.ElementName;
        if (source != owner) name = source.Title + ": " + name;
        if (control is TableViewRow { DataContext: FileRow }) id = panel.Id + ".files.row";
        var actionPath = interactions.Length == 0 ? null : string.Join(" > ", interactions.Select(step => step.ElementName).Append(target.ElementName));
        var dialog = new CommentDialog(name, this, actionPath) { Background = owner.Background };
        comments.Add(dialog);
        Attach(dialog);
        dialog.Submitted += text => owner.Execute(async () =>
        {
            Notes.Add(new(Guid.NewGuid().ToString("N"), ReleaseId, id, name, directory, text.Trim(), DateTimeOffset.UtcNow,
                Interactions: interactions.Append(target with { ElementId = id, ElementName = name }).ToArray()));
            dialog.Close();
            owner.SetUpdateMessage("Comment added. Open Notes to view the refinement list.");
            if (Changed != null) await Changed();
        });
        dialog.Closed += (_, _) =>
        {
            comments.Remove(dialog); surface.Cancel(); owner.RecordActivity();
            if (source == owner)
            { if (owner.CommandLine.Editor == previousFocus) previousFocus.Focus(); else owner.ActivePanel.FocusCurrent(); }
            else if (source.IsVisible && previousFocus?.Focus() != true && source is FileViewerWindow sourceViewer) sourceViewer.TextContent.Focus();
        };
        _ = dialog.ShowDialog(source);
        dialog.Editor.Focus();
    }

    public void ShowNotes()
    {
        if (IsActive || owner.HasOpenDialog || owner.IsBusy) return;
        var list = new ListBox { Name = "FeedbackNotes", Focusable = true };
        AutomationProperties.SetAutomationId(list, "feedback.notes");
        list.ItemTemplate = new FuncDataTemplate<FeedbackNote>((note, _) => note == null ? new Border() : new StackPanel
        {
            Margin = new Thickness(4, 4),
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = note!.ElementName, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = note.Text, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = note.Interactions is { Length: > 1 } steps ? "Actions: " + string.Join(" > ", steps.Select(step => step.ElementName)) : null,
                    IsVisible = note.Interactions is { Length: > 1 }, FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = note.ResolvedIn == null ? "Open · " + note.Release : "Resolved in " + note.ResolvedIn, FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary },
                new TextBlock { Text = note.Resolution ?? note.PanelPath, FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary, TextWrapping = TextWrapping.Wrap }
            }
        });
        var resolved = new ListBox { Name = "ResolvedFeedbackNotes", ItemTemplate = list.ItemTemplate, Focusable = true };
        AutomationProperties.SetAutomationId(resolved, "feedback.resolved-notes");
        var openTab = new TabItem { Content = list };
        var resolvedTab = new TabItem { Content = resolved };
        var tabs = new TabControl { Name = "FeedbackTabs", ItemsSource = new[] { openTab, resolvedTab }, SelectedIndex = 0 };
        void RefreshNotes()
        {
            var selectedOpen = list.SelectedItem; var selectedResolved = resolved.SelectedItem;
            var openNotes = Notes.Where(note => note.ResolvedIn == null).OrderByDescending(note => note.CreatedAt).ToArray();
            var resolvedNotes = Notes.Where(note => note.ResolvedIn != null).OrderByDescending(note => note.CreatedAt).ToArray();
            list.ItemsSource = openNotes; resolved.ItemsSource = resolvedNotes;
            list.SelectedItem = selectedOpen; resolved.SelectedItem = selectedResolved;
            openTab.Header = $"Open ({openNotes.Length})"; resolvedTab.Header = $"Resolved ({resolvedNotes.Length})";
        }
        System.Collections.Specialized.NotifyCollectionChangedEventHandler notesChanged = (_, _) => RefreshNotes();
        Notes.CollectionChanged += notesChanged;
        RefreshNotes();
        var remove = new Button { Content = "Remove selected", Name = "RemoveNote" };
        remove.Click += (_, _) => owner.Execute(async () =>
        {
            if ((tabs.SelectedIndex == 0 ? list : resolved).SelectedItem is FeedbackNote note) { Notes.Remove(note); if (Changed != null) await Changed(); }
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
        var layout = new Grid { Margin = UiPalette.FormMargin, RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 8 };
        layout.Children.Add(new TextBlock { Text = "Select an element with Feedback or F12 to add a comment.", FontSize = UiPalette.MetaSize, TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(tabs, 1); layout.Children.Add(tabs);
        var location = new TextBlock
        {
            Text = ListPath == null ? "Comments are kept with the session." : "Saved list: " + ListPath,
            FontSize = UiPalette.MetaSize,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(location, 2); layout.Children.Add(location);
        var buttons = UiLayout.Actions(remove, close);
        Grid.SetRow(buttons, 3); layout.Children.Add(buttons);
        dialog.Content = layout;
        dialog.AddHandler(InputElement.KeyDownEvent, (_, e) => { if (IsPickingIn(dialog)) return; if (e.Key == Key.Escape) { dialog.Close(); e.Handled = true; } }, RoutingStrategies.Tunnel);
        Attach(dialog);
        ActiveNotes = dialog;
        dialog.Closed += (_, _) => { Notes.CollectionChanged -= notesChanged; ActiveNotes = null; owner.RecordActivity(); owner.ActivePanel.FocusCurrent(); };
        _ = dialog.ShowDialog(owner);
        list.Focus();
    }
}

public sealed class CommentDialog : Window
{
    public TextBox Editor { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 180, PlaceholderText = "What should change?" };
    public event Action<string>? Submitted;

    public CommentDialog(string elementName, FeedbackController? feedback = null, string? actionPath = null)
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
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Comment on: " + elementName, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = actionPath == null ? null : "Actions: " + actionPath, Name = "FeedbackActionPath", IsVisible = actionPath != null, FontSize = UiPalette.MetaSize, TextWrapping = TextWrapping.Wrap },
                Editor, new TextBlock { Text = "Ctrl+Enter adds the comment · Esc cancels", FontSize = UiPalette.MetaSize },
                UiLayout.Actions(add, cancel)
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

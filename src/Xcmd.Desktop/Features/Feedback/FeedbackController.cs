// @implements ReportControl, KeepFeedback, ReportHighlighting, FeedbackIdentity
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
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Features.Viewer;

namespace Xcmd.Desktop.Features.Feedback;

public sealed partial class FeedbackController
{
    private readonly Window owner;
    private readonly FeedbackActions actions;
    private readonly Dictionary<Window, FeedbackSurface> surfaces = [];
    private readonly List<CommentDialog> comments = [];
    private int writes;
    public bool IsSaving => writes != 0;
    public ObservableCollection<FeedbackIssue> Notes { get; } = [];
    public IssueStore? Store { get; set; }
    public bool IsPicking => surfaces.Values.Any(surface => surface.IsPicking);
    public CommentDialog? ActiveComment => comments.LastOrDefault();
    public Window? ActiveNotes { get; private set; }
    public string ReleaseId { get; set; } = "development";
    public string? ListPath { get; set; }
    public bool IsActive => IsPicking || ActiveComment != null || ActiveNotes != null || IsSaving;

    internal FeedbackController(Window owner, Grid content, FeedbackActions actions)
    {
        this.owner = owner;
        this.actions = actions;
        surfaces[owner] = new FeedbackSurface(this, owner, content, actions.SetHint, actions.RestoreFocus);
    }

    public bool IsPickingIn(Window window) => surfaces.TryGetValue(window, out var surface) && surface.IsPicking;
    internal void StopNameSearch() => actions.StopNameSearch();

    public void Attach(Window window, bool showButton = true, Action<string>? setHint = null, Action? focusFallback = null)
    {
        if (surfaces.ContainsKey(window)) return;
        window.Icon ??= owner.Icon;
        var original = window.Content as Control;
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto") }.WithFeedbackId("FeedbackController.layout");
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
        }.WithFeedbackId("FeedbackController.button");
        button.Classes.Add("quiet");
        ToolTip.SetTip(button, "Start feedback. While picking, use Ctrl+click or Alt+click to open a control.");
        AutomationProperties.SetAutomationId(button, "feedback.start");
        button.Click += (_, _) => Begin(window);
        if (showButton) { Grid.SetRow(button, 1); layout.Children.Add(button); }
        window.Content = null;
        if (original != null) layout.Children.Add(original);
        var root = new Grid().WithFeedbackId("FeedbackController.root"); root.Children.Add(layout); window.Content = root;
        if (showButton && double.IsFinite(window.Height)) window.Height += 32;
        surfaces[window] = new FeedbackSurface(this, window, root, setHint ?? actions.SetHint, previousFocus =>
        {
            if (window.IsVisible && previousFocus?.Focus() != true) focusFallback?.Invoke();
        });
        window.Closed += (_, _) => surfaces.Remove(window);
    }

    public void Begin(Window? window = null)
    {
        foreach (var surface in surfaces.Values) surface.Cancel();
        surfaces[window ?? owner].Begin();
    }
    public void Cancel() { foreach (var surface in surfaces.Values) surface.Cancel(); }

    public void Restore(IEnumerable<FeedbackIssue> notes)
    {
        if (Notes.SequenceEqual(notes)) return;
        Notes.Clear();
        foreach (var note in notes) Notes.Add(note);
    }

    public async Task RefreshAsync()
    {
        if (Store != null) Restore(await Store.ReadAsync());
    }

    internal static Control? Target(Control? source)
    {
        var ancestors = source?.GetSelfAndVisualAncestors().OfType<Control>().ToArray() ?? [];
        var inPopup = ancestors.Any(control => control is OverlayPopupHost);
        return ancestors.FirstOrDefault(control => control is MenuItem or Button or TextBox or ComboBox || inPopup && control is ListBoxItem) ??
            ancestors.FirstOrDefault(control => control.GetValue(FeedbackTarget.IdProperty) != null || control is TableViewRow or TableViewColumnHeader) ??
            ancestors.FirstOrDefault(control => control is TextBlock { Text.Length: > 0 } || !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(control))) ?? source;
    }

    internal static FeedbackInteraction DescribeInteraction(Control control, string action)
    {
        var id = FeedbackTarget.Id(control);
        var name = control switch
        {
            MenuItem menu => menu.Header?.ToString()?.Replace("_", "", StringComparison.Ordinal) ?? id,
            ListBoxItem { Content: string text } => text,
            Button { Content: AccessText text } => text.Text?.Replace("_", "", StringComparison.Ordinal) ?? id,
            Button { Content: Control } button => AutomationProperties.GetName(button) ?? id,
            Button button => button.Content?.ToString() ?? id,
            TextBlock text => text.Text ?? id,
            TableViewRow { DataContext: IFeedbackRow row } => "File row: " + row.Name,
            TableViewRow { DataContext: FileSearchResult result } => "Search result: " + result.RelativePath,
            TableViewRow { DataContext: DeferredSearchLocation location } => location.Reason + ": " + ArchivePath.Display(location.Path),
            TableViewRow { DataContext: SearchFailure failure } => failure.Reason + ": " + ArchivePath.Display(failure.Path),
            _ => AutomationProperties.GetName(control) ?? id
        };
        return new(action, id, name);
    }

    public CommentDialog? Report(Window source, Control control, string context, Action? onSaved = null)
    {
        if (IsActive || !surfaces.TryGetValue(source, out var surface)) return null;
        return OpenComment(source, control, surface, [], context, onSaved);
    }

    internal CommentDialog OpenComment(Window source, Control control, FeedbackSurface surface, FeedbackInteraction[] interactions, string? context = null, Action? onSaved = null)
    {
        var previousFocus = source.FocusManager?.GetFocusedElement() as Control;
        var target = DescribeInteraction(control, "Comment");
        var id = target.ElementId;
        var name = target.ElementName;
        if (control.DataContext is IFeedbackRow row && !name.Contains(row.Name, StringComparison.Ordinal)) name = row.Name + ": " + name;
        if (source != owner) name = source.Title + ": " + name;
        if (FeedbackTarget.Scope(control) is { } scope) name = scope + ": " + name;
        var actionPath = interactions.Length == 0 ? null : string.Join(" > ", interactions.Select(step => step.ElementName).Append(target.ElementName));
        surface.ClosePopups();
        var dialog = new CommentDialog(name, this, actionPath, context) { Background = owner.Background }.WithFeedbackId("FeedbackController.dialog");
        comments.Add(dialog);
        Attach(dialog);
        var submitting = false;
        dialog.Submitted += text =>
        {
            if (submitting) return;
            submitting = true;
            writes++;
            actions.Execute(async () =>
            {
                try { await SaveCommentAsync(dialog, id, name, text, context, onSaved); }
                finally { submitting = false; writes--; }
            });
        };
        dialog.Closed += (_, _) =>
        {
            comments.Remove(dialog); surface.Cancel(); actions.RecordActivity();
            surface.RestoreFocus(previousFocus);
        };
        _ = dialog.ShowDialog(source);
        dialog.Editor.Focus();
        return dialog;
    }

    private async Task SaveCommentAsync(CommentDialog dialog, string id, string name, string text, string? context, Action? onSaved)
    {
        try
        {
            if (Store == null) throw new InvalidOperationException("The issue store has not been configured.");
            var report = context == null ? text.Trim() : text.Trim() + "\n\n" + context;
            await Store.AddAsync(new(Guid.NewGuid().ToString("N"), FeedbackIssue.New, report, new(id, name), ReleaseId));
            dialog.Close();
            await RefreshAsync();
            actions.SetHint("Comment saved. Open Notes to view issues.");
            onSaved?.Invoke();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        { dialog.SaveError.Text = "Could not save comment. " + error.Message; actions.Trace(error.ToString()); }
    }

    public void ShowNotes()
    {
        if (IsActive || !actions.CanShowNotes()) return;
        var list = new ListBox { Name = "FeedbackNotes", Focusable = true }.WithFeedbackId("FeedbackController.list");
        AutomationProperties.SetAutomationId(list, "feedback.notes");
        list.ItemTemplate = new FuncDataTemplate<FeedbackIssue>((note, _) => note == null ? new Border().WithFeedbackId("FeedbackController.empty") : new StackPanel
        {
            Margin = new Thickness(4, 4),
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = note!.Target.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FeedbackController.item.2"),
                new TextBlock { Text = note.Text, TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FeedbackController.item.3"),
                new TextBlock { Text = note.Status + " · " + note.Release, FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary }.WithFeedbackId("FeedbackController.item.4"),
                new TextBlock { Text = note.Target.Id, FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary, TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FeedbackController.item.5")
            }
        }.WithFeedbackId("FeedbackController.item.6"));
        var resolved = new ListBox { Name = "ResolvedFeedbackNotes", ItemTemplate = list.ItemTemplate, Focusable = true }.WithFeedbackId("FeedbackController.resolved");
        AutomationProperties.SetAutomationId(resolved, "feedback.resolved-notes");
        var openTab = new TabItem { Content = list }.WithFeedbackId("FeedbackController.openTab");
        var resolvedTab = new TabItem { Content = resolved }.WithFeedbackId("FeedbackController.resolvedTab");
        var tabs = new TabControl { Name = "FeedbackTabs", ItemsSource = new[] { openTab, resolvedTab }, SelectedIndex = 0 }.WithFeedbackId("FeedbackController.tabs");
        void RefreshNotes()
        {
            var selectedOpen = list.SelectedItem; var selectedResolved = resolved.SelectedItem;
            var openNotes = Notes.Where(note => note.Status != FeedbackIssue.Closed).Reverse().ToArray();
            var resolvedNotes = Notes.Where(note => note.Status == FeedbackIssue.Closed).Reverse().ToArray();
            list.ItemsSource = openNotes; resolved.ItemsSource = resolvedNotes;
            list.SelectedItem = selectedOpen; resolved.SelectedItem = selectedResolved;
            openTab.Header = $"Open ({openNotes.Length})"; resolvedTab.Header = $"Closed ({resolvedNotes.Length})";
        }
        System.Collections.Specialized.NotifyCollectionChangedEventHandler notesChanged = (_, _) => RefreshNotes();
        Notes.CollectionChanged += notesChanged;
        RefreshNotes();
        var notesError = UiLayout.Error().WithFeedbackId("FeedbackController.NotesError");
        var remove = new Button { Content = "Remove selected", Name = "RemoveNote" }.WithFeedbackId("FeedbackController.remove");
        remove.Click += (_, _) => actions.Execute(async () =>
        {
            writes++;
            try
            {
                notesError.Text = null;
                if ((tabs.SelectedIndex == 0 ? list : resolved).SelectedItem is FeedbackIssue note && Store != null)
                { await Store.RemoveAsync(note.Id); await RefreshAsync(); }
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            { notesError.Text = error.Message; actions.Trace(error.ToString()); }
            finally { writes--; }
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
        }.WithFeedbackId("FeedbackController.dialog.2");
        var close = new Button { Content = "Close" }.WithFeedbackId("FeedbackController.close");
        close.Click += (_, _) => dialog.Close();
        var layout = new Grid { Margin = UiPalette.FormMargin, RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto"), RowSpacing = 8 }.WithFeedbackId("FeedbackController.layout.2");
        layout.Children.Add(new TextBlock { Text = "Use Feedback or F12 to add a comment. Run an agent to fix and commit one group of related issues.", FontSize = UiPalette.MetaSize, TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FeedbackController.ShowNotes"));
        Grid.SetRow(tabs, 1); layout.Children.Add(tabs);
        var location = new TextBlock
        {
            Text = ListPath == null ? "Issues are stored in the repository." : "Issue store: " + ListPath,
            FontSize = UiPalette.MetaSize,
            TextWrapping = TextWrapping.Wrap
        }.WithFeedbackId("FeedbackController.location");
        Grid.SetRow(location, 2); layout.Children.Add(location);
        Grid.SetRow(notesError, 3); layout.Children.Add(notesError);
        var buttons = UiLayout.Actions(BuildAgentButton(notesError), remove, close);
        Grid.SetRow(buttons, 4); layout.Children.Add(buttons);
        dialog.Content = layout;
        dialog.AddHandler(InputElement.KeyDownEvent, (_, e) => { if (IsPickingIn(dialog)) return; if (e.Key == Key.Escape) { dialog.Close(); e.Handled = true; } }, RoutingStrategies.Tunnel);
        Attach(dialog);
        ActiveNotes = dialog;
        dialog.Closed += (_, _) => { Notes.CollectionChanged -= notesChanged; ActiveNotes = null; actions.RecordActivity(); actions.RestoreFocus(null); };
        _ = dialog.ShowDialog(owner);
        list.Focus();
    }
}

internal sealed record FeedbackActions(
    Action<Func<Task>> Execute,
    Action<string> SetHint,
    Action StopNameSearch,
    Action RecordActivity,
    Func<bool> CanShowNotes,
    Action<Control?> RestoreFocus,
    Func<ExternalLaunch, Task> LaunchExternal,
    Action<string> Trace);

public sealed class CommentDialog : Window
{
    public TextBlock SaveError { get; } = UiLayout.Error().WithFeedbackId("CommentDialog.SaveError");
    public TextBox Editor { get; } = new TextBox() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 180, PlaceholderText = "What should change?" }.WithFeedbackId("FeedbackController.Editor");
    public event Action<string>? Submitted;

    public CommentDialog(string elementName, FeedbackController? feedback = null, string? actionPath = null, string? context = null)
    {
        Title = "Add a comment"; Width = 560; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(Editor, "feedback.comment");
        var add = new Button { Content = "Add comment", Name = "AddComment", IsEnabled = false }.WithFeedbackId("FeedbackController.add");
        add.Classes.Add("primary");
        Editor.TextChanged += (_, _) => add.IsEnabled = !string.IsNullOrWhiteSpace(Editor.Text);
        void Submit() { if (!string.IsNullOrWhiteSpace(Editor.Text)) Submitted?.Invoke(Editor.Text); }
        add.Click += (_, _) => Submit();
        var cancel = new Button { Content = "Cancel" }.WithFeedbackId("FeedbackController.cancel");
        cancel.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Comment on: " + elementName, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }.WithFeedbackId("FeedbackController.content"),
                new TextBlock { Text = actionPath == null ? null : "Actions: " + actionPath, Name = "FeedbackActionPath", IsVisible = actionPath != null, FontSize = UiPalette.MetaSize, TextWrapping = TextWrapping.Wrap }.WithFeedbackId("FeedbackController.content.2"),
                Editor,
                new TextBlock { Text = "Included with this report:", IsVisible = context != null, FontSize = UiPalette.MetaSize }.WithFeedbackId("FeedbackController.report-label"),
                new TextBox { Text = context, Name = "FeedbackReportContext", IsVisible = context != null, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 160 }.WithFeedbackId("FeedbackController.report-context"),
                SaveError, new TextBlock { Text = "Ctrl+Enter adds the comment · Esc cancels", FontSize = UiPalette.MetaSize }.WithFeedbackId("FeedbackController.content.3"),
                UiLayout.Actions(add, cancel)
            }
        }.WithFeedbackId("FeedbackController.content.4");
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback?.IsPickingIn(this) == true) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Submit(); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }
}

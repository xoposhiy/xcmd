using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Desktop;

public enum DirectoryList { History, Frequent, Favorites }

public sealed class DirectoryNavigationDialog : Window
{
    private readonly FavoriteDirectories favorites;
    private readonly string currentDirectory;
    private readonly TextBlock message = new() { Name = "NavigationMessage", FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly Button open = new() { Content = "Open", Name = "NavigationOpen", MinWidth = 80 };
    private readonly Button add = new() { Content = "Add current directory", Name = "FavoriteAdd" };
    private readonly Button rename = new() { Content = "Save name", Name = "FavoriteRename" };
    private readonly Button remove = new() { Content = "Remove", Name = "FavoriteRemove" };
    private readonly Button cancel = new() { Content = "Cancel", Name = "NavigationCancel", MinWidth = 80 };
    private readonly Grid editorRow;
    private readonly Dictionary<ListBox, int> cursors = [];
    private ListBox activeList;
    public ListBox Directories { get; } = new() { Name = "HistoryDirectories", Focusable = true };
    public ListBox Suggestions { get; } = new() { Name = "SuggestedDirectories", Focusable = true };
    public ListBox Favorites { get; } = new() { Name = "FavoriteDirectories", Focusable = true };
    public TextBox NameEditor { get; } = new() { Name = "FavoriteName", PlaceholderText = "Favorite name", MaxLength = 100 };
    public DirectoryList ActiveList => activeList == Directories ? DirectoryList.History : activeList == Suggestions ? DirectoryList.Frequent : DirectoryList.Favorites;
    public bool IsOpening { get; private set; }
    public string? Error { get; private set; }
    public event Action<string>? OpenRequested;
    public event Action? Changed;

    public DirectoryNavigationDialog(IReadOnlyList<string> history, IReadOnlyList<DirectorySuggestion> suggestions,
        FavoriteDirectories favorites, string currentDirectory, DirectoryList initial, FeedbackController comments)
    {
        this.favorites = favorites; this.currentDirectory = currentDirectory;
        activeList = initial == DirectoryList.Favorites ? Favorites : initial == DirectoryList.Frequent ? Suggestions : Directories;
        Title = "Go to directory"; Width = 1140; MinWidth = 840; MinHeight = 300;
        Height = Math.Clamp(200 + Math.Max(Math.Max(history.Count, suggestions.Count), favorites.Items.Count) * 30, 320, 600);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(this, "navigation.dialog");
        AutomationProperties.SetAutomationId(Directories, "history.directories");
        AutomationProperties.SetAutomationId(Suggestions, "history.suggestions");
        AutomationProperties.SetAutomationId(Favorites, "favorites.directories");
        AutomationProperties.SetAutomationId(NameEditor, "favorites.name");
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 12 };
        var lists = new[] { Directories, Suggestions, Favorites };
        var headings = new[] { "History", "Frequent", "Favorites" };
        for (var i = 0; i < lists.Length; i++)
        {
            var list = lists[i]; cursors[list] = 0;
            list.IsTextSearchEnabled = false;
            KeyboardNavigation.SetTabNavigation(list, KeyboardNavigationMode.Once);
            list.Styles.Add(new Style(selector => selector.OfType<ListBoxItem>())
            { Setters = { new Setter(PaddingProperty, new Thickness(6, 3)), new Setter(MinHeightProperty, 30d) } });
            list.GotFocus += (_, _) => ActivateList(list);
            list.PointerPressed += (_, _) => ActivateList(list);
            list.SelectionChanged += (_, _) =>
            {
                if (list == Favorites && list.SelectedItem is FavoriteDirectory favorite) NameEditor.Text = favorite.Name;
                UpdateActions();
            };
            list.DoubleTapped += (_, e) =>
            {
                if (e.Source is Control source && source.GetSelfAndVisualAncestors().OfType<ListBoxItem>().Any())
                { ActivateList(list); OpenSelection(); }
            };
            var column = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
            var heading = new TextBlock { Text = headings[i], FontWeight = FontWeight.SemiBold };
            if (list == Suggestions) ToolTip.SetTip(heading, "Locations used for at least ten active seconds. Roots and quick pass-through visits are excluded. Weight halves every three days.");
            column.Children.Add(heading); Grid.SetRow(list, 1); column.Children.Add(list);
            Grid.SetColumn(column, i); columns.Children.Add(column);
        }
        foreach (var list in new[] { Directories, Suggestions })
            list.ItemTemplate = new FuncDataTemplate<string>((path, _) => PathLabel(path));
        Favorites.ItemTemplate = new FuncDataTemplate<FavoriteDirectory>((favorite, _) =>
        {
            if (favorite == null) return new Border();
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,2*"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = favorite.Name, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            var path = new TextBlock { Text = favorite.Path, Foreground = Brushes.LightGray, TextTrimming = TextTrimming.PrefixCharacterEllipsis };
            Grid.SetColumn(path, 1); row.Children.Add(path); ToolTip.SetTip(row, favorite.Name + "\n" + favorite.Path);
            return row;
        });
        Directories.ItemsSource = history;
        Suggestions.ItemsSource = suggestions.Select(item => item.Path).ToArray();
        Favorites.ItemsSource = favorites.Items.ToArray();
        NameEditor.TextChanged += (_, _) => UpdateActions();
        ToolTip.SetTip(add, currentDirectory);
        add.Click += (_, _) => AddCurrent(); rename.Click += (_, _) => RenameSelection(); remove.Click += (_, _) => RemoveSelection();
        editorRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = 8 };
        editorRow.Children.Add(new TextBlock { Text = "Favorite name", VerticalAlignment = VerticalAlignment.Center });
        var editorControls = new Control[] { NameEditor, rename, add, remove };
        for (var i = 0; i < editorControls.Length; i++) { Grid.SetColumn(editorControls[i], i + 1); editorRow.Children.Add(editorControls[i]); }
        open.Click += (_, _) => OpenSelection();
        cancel.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, open } };
        var layout = new Grid { Margin = new Thickness(12), RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto"), RowSpacing = 8 };
        layout.Children.Add(columns); Grid.SetRow(editorRow, 1); layout.Children.Add(editorRow);
        Grid.SetRow(message, 2); layout.Children.Add(message); Grid.SetRow(buttons, 3); layout.Children.Add(buttons);
        Content = layout; ActivateList(activeList);
        AddHandler(GotFocusEvent, (_, _) => UpdateActions(), RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, (_, e) => HandleKey(e, comments, lists), RoutingStrategies.Tunnel);
    }

    private void HandleKey(KeyEventArgs e, FeedbackController comments, ListBox[] lists)
    {
        if (comments.IsPickingIn(this)) return;
        var inList = lists.Any(list => list.IsKeyboardFocusWithin);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        if (HandleNavigationShortcut(e)) { e.Handled = true; return; }
        if (e.Key == Key.Enter)
        {
            if (ConfirmSelection(inList)) e.Handled = true;
            return;
        }
        if (IsOpening) return;
        if (inList && e.Key is Key.Left or Key.Right)
        {
            ActivateList(lists[Math.Clamp(Array.IndexOf(lists, activeList) + (e.Key == Key.Left ? -1 : 1), 0, 2)]);
            FocusSelection();
            e.Handled = true;
            return;
        }
        if (Favorites.IsKeyboardFocusWithin) HandleFavoriteKey(e);
    }

    private bool HandleNavigationShortcut(KeyEventArgs e)
    {
        if (IsOpening) return false;
        if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control)) SelectList(DirectoryList.Favorites);
        else if (e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt)) SelectList(DirectoryList.History);
        else return false;
        return true;
    }

    private bool ConfirmSelection(bool inList)
    {
        if (!IsOpening && NameEditor.IsKeyboardFocusWithin) RenameSelection();
        else if (inList) OpenSelection();
        else return false;
        return true;
    }

    private void HandleFavoriteKey(KeyEventArgs e)
    {
        if (e.Key == Key.F2) { NameEditor.Focus(); NameEditor.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Insert) { AddCurrent(); e.Handled = true; }
        else if (e.Key == Key.Delete) { RemoveSelection(); e.Handled = true; }
    }

    private static Control PathLabel(string? path)
    {
        var label = new TextBlock { Text = path, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.PrefixCharacterEllipsis, MinHeight = 22 };
        ToolTip.SetTip(label, path); return label;
    }

    public void SelectList(DirectoryList list) { ActivateList(list == DirectoryList.History ? Directories : list == DirectoryList.Frequent ? Suggestions : Favorites); FocusSelection(); }
    private void ActivateList(ListBox list)
    {
        if (activeList != list) { cursors[activeList] = Math.Max(0, activeList.SelectedIndex); activeList.SelectedIndex = -1; activeList = list; }
        if (activeList.SelectedIndex < 0 && activeList.ItemCount > 0) activeList.SelectedIndex = Math.Min(cursors[activeList], activeList.ItemCount - 1);
        if (editorRow != null) editorRow.IsVisible = activeList == Favorites;
        UpdateActions();
    }

    public void FocusSelection()
    {
        activeList.UpdateLayout();
        if (activeList.ContainerFromIndex(activeList.SelectedIndex) is Control row) row.Focus(); else activeList.Focus();
    }
    public void SetOpening(bool value)
    {
        IsOpening = value; Directories.IsEnabled = Suggestions.IsEnabled = Favorites.IsEnabled = add.IsEnabled = !value; UpdateActions();
        if (value) ShowMessage("Opening directory…");
    }
    public void ShowError(string text) { ShowMessage("Could not open directory: " + text, true); FocusSelection(); }
    private void Change(Action action)
    {
        if (IsOpening) return;
        try { action(); Changed?.Invoke(); FocusSelection(); } catch (ArgumentException error) { ShowMessage(error.Message, true); }
    }
    private void AddCurrent() => Change(() => RefreshFavorites(favorites.Add(currentDirectory).Id));
    private void RenameSelection() => Change(() =>
    { if (Favorites.SelectedItem is FavoriteDirectory favorite) { favorites.Rename(favorite.Id, NameEditor.Text ?? ""); RefreshFavorites(favorite.Id); } });
    private void RemoveSelection() => Change(() =>
    { if (Favorites.SelectedItem is FavoriteDirectory favorite) { var index = Favorites.SelectedIndex; favorites.Remove(favorite.Id); RefreshFavorites(index: index); } });
    private void RefreshFavorites(string? id = null, int index = 0)
    {
        Favorites.ItemsSource = favorites.Items.ToArray();
        Favorites.SelectedIndex = id == null ? Math.Min(index, favorites.Items.Count - 1) : favorites.Items.ToList().FindIndex(item => item.Id == id);
        if (Favorites.SelectedItem == null) NameEditor.Text = "";
        ShowMessage(""); UpdateActions();
    }
    private void UpdateActions()
    {
        open.IsEnabled = !IsOpening && activeList.SelectedItem != null;
        var selected = Favorites.SelectedItem as FavoriteDirectory;
        remove.IsEnabled = NameEditor.IsEnabled = !IsOpening && selected != null;
        rename.IsEnabled = !IsOpening && selected != null && NameEditor.Text != selected.Name;
        var actions = new[] { open, rename, add, remove, cancel };
        var focused = FocusManager?.GetFocusedElement();
        var primary = focused is Button button ? actions.Contains(button) ? button : null
            : NameEditor.IsKeyboardFocusWithin ? rename : open;
        foreach (var action in actions) action.IsDefault = action == primary && action.IsEnabled;
    }
    private void ShowMessage(string text, bool error = false) { Error = error ? text : null; message.Text = text; message.IsVisible = text.Length > 0; message.Foreground = error ? Brushes.Salmon : Brushes.LightGray; }
    private void OpenSelection()
    {
        var path = activeList.SelectedItem is FavoriteDirectory favorite ? favorite.Path : activeList.SelectedItem as string;
        if (!IsOpening && path != null) OpenRequested?.Invoke(path);
    }
}

// @implements RecentLocations, FavoriteLocations, FindSavedLocation
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

public enum DirectoryList { History, Favorites }

public sealed partial class DirectoryNavigationDialog : Window
{
    private readonly FavoriteDirectories favorites;
    private readonly string[] suggestions;
    private readonly string currentDirectory;
    private readonly TextBlock message = new TextBlock() { Name = "NavigationMessage", FontSize = UiPalette.MetaSize, TextWrapping = TextWrapping.Wrap, IsVisible = false }.WithFeedbackId("DirectoryNavigationDialog.message");
    private readonly Button open = new Button() { Content = "Open", Name = "NavigationOpen", MinWidth = 80 }.WithFeedbackId("DirectoryNavigationDialog.open");
    private readonly Button add = new Button() { Content = "Add current directory", Name = "FavoriteAdd" }.WithFeedbackId("DirectoryNavigationDialog.add");
    private readonly Button rename = new Button() { Content = "Save name", Name = "FavoriteRename" }.WithFeedbackId("DirectoryNavigationDialog.rename");
    private readonly Button remove = new Button() { Content = "Remove", Name = "FavoriteRemove" }.WithFeedbackId("DirectoryNavigationDialog.remove");
    private readonly Button cancel = new Button() { Content = "Cancel", Name = "NavigationCancel", MinWidth = 80 }.WithFeedbackId("DirectoryNavigationDialog.cancel");
    private readonly Grid editorRow;
    private readonly Dictionary<ListBox, int> cursors = [];
    private ListBox activeList;
    public ListBox Directories { get; } = new ListBox() { Name = "HistoryDirectories", Focusable = true }.WithFeedbackId("DirectoryNavigationDialog.Directories");
    public ListBox Favorites { get; } = new ListBox() { Name = "FavoriteDirectories", Focusable = true }.WithFeedbackId("DirectoryNavigationDialog.Favorites");
    public TextBox NameEditor { get; } = new TextBox() { Name = "FavoriteName", PlaceholderText = "Favorite name", MaxLength = 100 }.WithFeedbackId("DirectoryNavigationDialog.NameEditor");
    public DirectoryList ActiveList => activeList == Directories ? DirectoryList.History : DirectoryList.Favorites;
    public bool IsOpening { get; private set; }
    public string? Error { get; private set; }
    public event Action<string>? OpenRequested;
    public event Action? Changed;

    public DirectoryNavigationDialog(IReadOnlyList<string> history, IReadOnlyList<DirectorySuggestion> suggestions,
        FavoriteDirectories favorites, string currentDirectory, DirectoryList initial, FeedbackController comments)
    {
        this.favorites = favorites; this.currentDirectory = currentDirectory;
        this.suggestions = suggestions.Select(item => item.Path).ToArray();
        activeList = initial == DirectoryList.Favorites ? Favorites : Directories;
        Title = "Go to directory"; Width = 960; MinWidth = 840; MinHeight = 300;
        Height = Math.Clamp(200 + Math.Max(history.Count, favorites.Items.Count + suggestions.Count) * 30, 320, 600);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(this, "navigation.dialog");
        AutomationProperties.SetAutomationId(Directories, "history.directories");
        AutomationProperties.SetAutomationId(Favorites, "favorites.directories");
        AutomationProperties.SetAutomationId(NameEditor, "favorites.name");
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 }.WithFeedbackId("DirectoryNavigationDialog.columns");
        var lists = new[] { Directories, Favorites };
        var headings = new[] { "History", "Favorites" };
        for (var i = 0; i < lists.Length; i++)
        {
            var list = lists[i]; cursors[list] = 0;
            list.IsTextSearchEnabled = false;
            KeyboardNavigation.SetTabNavigation(list, KeyboardNavigationMode.Once);
            list.Styles.Add(new Style(selector => selector.OfType<ListBoxItem>())
            { Setters = { new Setter(PaddingProperty, UiPalette.CellPadding), new Setter(MinHeightProperty, UiPalette.RowHeight) } });
            list.GotFocus += (_, _) => ActivateList(list);
            list.PointerPressed += (_, _) => { ResetNameSearch(); ActivateList(list); };
            list.SelectionChanged += (_, _) =>
            {
                if (list == Favorites) NameEditor.Text = (list.SelectedItem as FavoriteDirectory)?.Name ?? "";
                UpdateActions();
            };
            list.DoubleTapped += (_, e) =>
            {
                if (e.Source is Control source && source.GetSelfAndVisualAncestors().OfType<ListBoxItem>().Any())
                { ActivateList(list); OpenSelection(); }
            };
            var column = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 }.WithFeedbackId("DirectoryNavigationDialog.column");
            var heading = new TextBlock { Text = headings[i], FontWeight = FontWeight.SemiBold }.WithFeedbackId("DirectoryNavigationDialog.heading");
            if (list == Favorites) ToolTip.SetTip(heading, "Starred favorites stay at the top. Below them are frequent directories, ranked by visits of at least ten active seconds. Directories opened only briefly follow without a score. Roots, archives and Git history are excluded. Weight halves every three days. Type to find a name or path.");
            column.Children.Add(heading); Grid.SetRow(list, 1); column.Children.Add(list);
            Grid.SetColumn(column, i); columns.Children.Add(column);
        }
        Directories.ItemTemplate = new FuncDataTemplate<string>((path, _) => PathLabel(path));
        Favorites.ItemTemplate = new FuncDataTemplate<object>((item, _) =>
        {
            if (item is not FavoriteDirectory favorite) return PathLabel(item as string, reserveStar: true);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*,2*"), ColumnSpacing = 8 }.WithFeedbackId("DirectoryNavigationDialog.row");
            row.Children.Add(new TextBlock { Text = "★", Foreground = UiPalette.Mark, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("DirectoryNavigationDialog.empty"));
            var name = new TextBlock { Text = favorite.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("DirectoryNavigationDialog.name");
            Grid.SetColumn(name, 1); row.Children.Add(name);
            var path = new TextBlock { Text = PathText.Display(favorite.Path), Foreground = UiPalette.Secondary, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.PrefixCharacterEllipsis }.WithFeedbackId("DirectoryNavigationDialog.path");
            TrackLocation(path, favorite.Path);
            Grid.SetColumn(path, 2); row.Children.Add(path); ToolTip.SetTip(row, favorite.Name + "\n" + favorite.Path);
            return row;
        });
        Directories.ItemsSource = history;
        Favorites.ItemsSource = FavoriteLocations();
        NameEditor.TextChanged += (_, _) => UpdateActions();
        ToolTip.SetTip(add, currentDirectory);
        add.Click += (_, _) => AddCurrent(); rename.Click += (_, _) => RenameSelection(); remove.Click += (_, _) => RemoveSelection();
        editorRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = 8 }.WithFeedbackId("DirectoryNavigationDialog.content");
        editorRow.Children.Add(new TextBlock { Text = "Favorite name", VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("DirectoryNavigationDialog.content.2"));
        var editorControls = new Control[] { NameEditor, rename, add, remove };
        for (var i = 0; i < editorControls.Length; i++) { Grid.SetColumn(editorControls[i], i + 1); editorRow.Children.Add(editorControls[i]); }
        open.Click += (_, _) => OpenSelection();
        cancel.Click += (_, _) => Close();
        var buttons = UiLayout.Actions(open, cancel);
        var layout = new Grid { Margin = UiPalette.FormMargin, RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto,Auto"), RowSpacing = 8 }.WithFeedbackId("DirectoryNavigationDialog.layout");
        layout.Children.Add(columns); Grid.SetRow(editorRow, 1); layout.Children.Add(editorRow);
        Grid.SetRow(searchHint, 2); layout.Children.Add(searchHint);
        Grid.SetRow(message, 3); layout.Children.Add(message); Grid.SetRow(buttons, 4); layout.Children.Add(buttons);
        Content = layout; ActivateList(activeList);
        AddHandler(GotFocusEvent, (_, _) => UpdateActions(), RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, (_, e) => HandleKey(e, comments, lists), RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, (_, e) => HandleSearchText(e, comments), RoutingStrategies.Tunnel);
    }

    private void HandleKey(KeyEventArgs e, FeedbackController comments, ListBox[] lists)
    {
        if (comments.IsPickingIn(this)) return;
        var inList = lists.Any(list => list.IsKeyboardFocusWithin);
        if (e.Key == Key.Escape)
        {
            if (NameSearchPrefix.Length > 0) ResetNameSearch(); else Close();
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
            ResetNameSearch();
            ActivateList(lists[Math.Clamp(Array.IndexOf(lists, activeList) + (e.Key == Key.Left ? -1 : 1), 0, lists.Length - 1)]);
            FocusSelection();
            e.Handled = true;
            return;
        }
        if (inList && HandleListMovement(e)) { e.Handled = true; return; }
        if (inList && HandleSearchKey(e)) { e.Handled = true; return; }
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
        if (e.Key == Key.F2 && Favorites.SelectedItem is FavoriteDirectory) { NameEditor.Focus(); NameEditor.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Insert) { AddCurrent(); e.Handled = true; }
        else if (e.Key == Key.Delete) { RemoveSelection(); e.Handled = true; }
    }

    private Control PathLabel(string? path, bool reserveStar = false)
    {
        var label = new TextBlock { Text = path == null ? "" : PathText.Display(path), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.PrefixCharacterEllipsis, MinHeight = 22, Margin = new Thickness(reserveStar ? 28 : 0, 0, 0, 0) }.WithFeedbackId("DirectoryNavigationDialog.label");
        if (path != null) TrackLocation(label, path);
        return label;
    }

    public void SelectList(DirectoryList list) { ResetNameSearch(); ActivateList(list == DirectoryList.History ? Directories : Favorites); FocusSelection(); }
    private void ActivateList(ListBox list)
    {
        if (activeList != list) { ResetNameSearch(); cursors[activeList] = Math.Max(0, activeList.SelectedIndex); activeList.SelectedIndex = -1; activeList = list; }
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
        IsOpening = value; Directories.IsEnabled = Favorites.IsEnabled = add.IsEnabled = !value; UpdateActions();
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
        ResetNameSearch();
        var locations = FavoriteLocations();
        Favorites.ItemsSource = locations;
        Favorites.SelectedIndex = id == null ? Math.Min(index, locations.Length - 1) : Array.FindIndex(locations, item => item is FavoriteDirectory favorite && favorite.Id == id);
        if (Favorites.SelectedItem is not FavoriteDirectory) NameEditor.Text = "";
        ShowMessage(""); UpdateActions();
    }
    private object[] FavoriteLocations()
    {
        var pinned = favorites.Items.Select(item => Path.TrimEndingDirectorySeparator(item.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return favorites.Items.Cast<object>().Concat(suggestions.Where(path => !pinned.Contains(Path.TrimEndingDirectorySeparator(path)))).ToArray();
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
    private void ShowMessage(string text, bool error = false) { Error = error ? text : null; message.Text = text; message.IsVisible = text.Length > 0; message.Foreground = error ? UiPalette.Error : UiPalette.Secondary; }
    private void OpenSelection()
    {
        var path = activeList.SelectedItem is FavoriteDirectory favorite ? favorite.Path : activeList.SelectedItem as string;
        if (!IsOpening && path != null) OpenRequested?.Invoke(path);
    }
}

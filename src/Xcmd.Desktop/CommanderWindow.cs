using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow : Window
{
    private readonly TextBlock status = new() { Text = "Ready", FontSize = 12, Foreground = Brushes.LightGray };
    private bool altInput;
    private int pendingActions;
    private readonly TextBlock updateMessage = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420, TextWrapping = TextWrapping.Wrap };
    private long lastInput = Stopwatch.GetTimestamp();
    private bool restarting;
    private bool pointerHeld;
    private Menu? mainMenu;
    public bool IsMenuOpen => mainMenu?.IsOpen == true;
    public PanelView Left { get; }
    public PanelView Right { get; }
    public PanelView ActivePanel { get; private set; }
    public PanelView OppositePanel => ActivePanel == Left ? Right : Left;
    public NameDialog? ActiveDialog { get; private set; }
    public DirectoryNavigationDialog? ActiveNavigationDialog { get; private set; }
    public bool HasOpenDialog => ActiveDialog != null || ActiveNavigationDialog != null;
    public FavoriteDirectories Favorites { get; } = new();
    public event Func<Task>? FavoritesChanged;
    public bool IsBusy => pendingActions > 0 || Left.Session.IsLoading || Right.Session.IsLoading;
    public string? LastError { get; private set; }
    public string StatusMessage => status.Text ?? "";
    public List<string> Trace { get; } = [];
    public FeedbackController Feedback { get; }
    public DirectoryUsageBook DirectoryUsage { get; } = new();
    private bool restoringVisits;
    public RestartReadiness Readiness => new(IsBusy, nameJump.Active || pointerHeld || CommandLine.HasKeyboardFocus || Left.EditingRow != null || Right.EditingRow != null,
        HasOpenDialog || IsMenuOpen, Feedback.IsActive, Stopwatch.GetElapsedTime(lastInput));

    public void SetUpdateMessage(string message) => updateMessage.Text = message;
    public void RecordActivity() => lastInput = Stopwatch.GetTimestamp();
    public void SetRestarting(bool value) { restarting = value; IsEnabled = !value; }

    public CommanderWindow(IFileSystem? fileSystem = null, IExternalLauncher? externalLauncher = null)
    {
        this.externalLauncher = externalLauncher ?? new WindowsExternalLauncher();
        fileSystem ??= new LocalFileSystem();
        Title = "Xcmd";
        Icon = ApplicationIcon.Load();
        Width = 1280; Height = 760; MinWidth = 840; MinHeight = 400;
        Background = new SolidColorBrush(Color.Parse("#0E1420"));
        var fileTypes = FileTypeSettings.Bundled();
        Left = new PanelView("left", fileSystem, fileTypes);
        Right = new PanelView("right", fileSystem, fileTypes);
        ActivePanel = Left;
        foreach (var panel in new[] { Left, Right })
        {
            panel.Activated += Activate;
            panel.OpenRequested += source => Execute(() => OpenCurrentAsync(source));
            panel.HistoryRequested += panel => OpenNavigation(panel, DirectoryList.History);
            panel.LocationVisited += path => { if (!restoringVisits) DirectoryUsage.Seed([path]); if (panel == ActivePanel) CommandLine.SetDirectory(path); };
            panel.LoadingRequested += source => { if (source == sizePanel) CancelDirectorySizes(); };
        }
        var layout = new Grid { RowDefinitions = new RowDefinitions("32,*,Auto,Auto,20"), Margin = new Thickness(12, 0, 12, 6) };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
        header.Children.Add(mainMenu = BuildMenu());
        Grid.SetColumn(updateMessage, 1); header.Children.Add(updateMessage);
        var feedback = new Button { Content = "Feedback", Name = "StartFeedback", Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var notes = new Button { Content = "Notes (0)", Name = "ShowNotes", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(feedback, "feedback.start");
        AutomationProperties.SetAutomationId(notes, "feedback.show-notes");
        Grid.SetColumn(feedback, 2); header.Children.Add(feedback);
        Grid.SetColumn(notes, 3); header.Children.Add(notes);
        layout.Children.Add(header);
        var panels = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
        panels.Children.Add(Left);
        Grid.SetColumn(Right, 2); panels.Children.Add(Right);
        Grid.SetRow(panels, 1); layout.Children.Add(panels);
        var help = BuildKeyboardHelp();
        Grid.SetRow(CommandLine, 2); layout.Children.Add(CommandLine);
        CommandLine.Focused += () => { Left.SetActive(false); Right.SetActive(false); };
        Grid.SetRow(help, 3); layout.Children.Add(help);
        Grid.SetRow(status, 4); layout.Children.Add(status);
        var root = new Grid();
        root.Children.Add(layout);
        Content = root;
        Feedback = new FeedbackController(this, root);
        feedback.Click += (_, _) => Feedback.Begin();
        notes.Click += (_, _) => Feedback.ShowNotes();
        Feedback.Notes.CollectionChanged += (_, _) => notes.Content = $"Notes ({Feedback.Notes.Count(note => note.ResolvedIn == null)})";
        AddHandler(PointerMovedEvent, (_, _) => { lastInput = Stopwatch.GetTimestamp(); ResetQuickJump(); }, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => { lastInput = Stopwatch.GetTimestamp(); ResetQuickJump(); pointerHeld = true; }, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => { lastInput = Stopwatch.GetTimestamp(); pointerHeld = false; }, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, _) => lastInput = Stopwatch.GetTimestamp(), RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, (_, _) => { RecordActivity(); ResetQuickJump(); }, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => { pointerHeld = false; launchKeysHeld.Clear(); ResetQuickJump(); };
        Closing += (_, _) => CancelDirectorySizes();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => { launchKeysHeld.Remove(e.Key); if (e.Key is Key.LeftAlt or Key.RightAlt) { altInput = false; suppressQuickText = false; e.Handled = true; } }, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        Left.SetActive(true);
    }

    private static WrapPanel BuildKeyboardHelp()
    {
        var help = new WrapPanel { Name = "KeyboardHelp" };
        string[] hints = ["Tab  Panels", "Enter  Open / Run", "Ctrl+L  Command line", "Esc  Clear", "↑ ↓  Files", "Alt+↑ ↓  Commands", "Ctrl+Enter  Name", "Ctrl+Shift+Enter  Path"];
        foreach (var hint in hints)
            help.Children.Add(new Border
            {
                BorderThickness = new Thickness(help.Children.Count == 0 ? 0 : 1, 0, 0, 0),
                BorderBrush = new SolidColorBrush(Color.Parse("#526078")),
                Padding = new Thickness(help.Children.Count == 0 ? 0 : 8, 0, 8, 0),
                Margin = new Thickness(0, 3),
                Child = new TextBlock { Text = hint, FontSize = 12, Foreground = Brushes.LightGray }
            });
        return help;
    }

    public async Task InitializeAsync(string left, string right)
    {
        await Task.WhenAll(Left.LoadAccessibleAsync(left), Right.LoadAccessibleAsync(right));
        Activate(Left);
        Left.FocusCurrent();
    }

    public void Activate(PanelView panel)
    {
        if (ActivePanel != panel) { ActivePanel.CancelRename(); ResetQuickJump(); }
        ActivePanel = panel;
        Left.SetActive(panel == Left);
        Right.SetActive(panel == Right);
        CommandLine.SetDirectory(panel.Session.DirectoryPath);
    }

    public async void Execute(Func<Task> action)
    {
        await RunActionAsync(action);
    }

    public async Task RunActionAsync(Func<Task> action)
    {
        pendingActions++;
        LastError = null;
        try { await action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { SetError(error.Message); }
        finally { pendingActions--; }
    }

    private void SetError(string? error)
    {
        LastError = error;
        status.Text = error ?? "Ready";
        if (error != null) Trace.Add("error: " + error);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (restarting) { e.Handled = true; return; }
        if (Feedback.IsPicking) { if (e.Key == Key.Escape) Feedback.Cancel(); e.Handled = true; return; }
        if (Feedback.ActiveComment != null || Feedback.ActiveNotes != null) return;
        if (IsMenuOpen) return;
        if (HasOpenDialog) return;
        if (CommandLine.HasKeyboardFocus) { HandleCommandLineKey(e); return; }
        altInput = e.KeyModifiers.HasFlag(KeyModifiers.Alt) || e.Key is Key.LeftAlt or Key.RightAlt;
        if (ActivePanel.EditingRow != null) { HandleRenameKey(e); return; }
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (HandleCommandLineShortcut(e, ctrl, shift)) return;
        if (HandleNameSearchKey(e, ctrl)) return;
        if (!HandlePanelCommand(e, ctrl, shift) && !HandlePanelNavigation(e, ctrl, shift)) return;
        Trace.Add($"key: {e.KeyModifiers}+{e.Key}; panel: {ActivePanel.Id}");
        e.Handled = true;
    }

    private async Task OpenCurrentAsync(PanelView panel)
    {
        if (panel.Current is not { } row) return;
        if (!row.Entry.IsDirectory)
        { await LaunchExternalAsync(LaunchPlans.OpenFile(row.Entry.FullPath, panel.Session.DirectoryPath)); return; }
        await panel.LoadAsync(panel.Current.Entry.FullPath, selectName: panel.Current.Entry.IsParent ? null : "..");
        panel.FocusCurrent();
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (restarting || Feedback.IsActive) return;
        if (ActivePanel.EditingRow != null || HasOpenDialog || IsMenuOpen || CommandLine.HasKeyboardFocus || string.IsNullOrEmpty(e.Text)) return;
        if (!nameJump.Active)
        { if (ActivePanel.Table.IsKeyboardFocusWithin) { CommandLine.InsertText(e.Text); e.Handled = true; } return; }
        if (suppressQuickText) { suppressQuickText = false; e.Handled = true; return; }
        JumpText(e.Text);
        e.Handled = true;
    }

    private void OpenCreateDirectoryDialog()
    {
        var panel = ActivePanel;
        var dialog = new NameDialog(Feedback) { Background = Background };
        Feedback.Attach(dialog);
        ActiveDialog = dialog;
        dialog.Submitted += name => Execute(async () =>
        {
            var error = PanelRules.ValidateName(name);
            if (error != null) { dialog.Error.Text = error; return; }
            try
            {
                await panel.Session.FileSystem.CreateDirectoryAsync(panel.Session.DirectoryPath, name);
                dialog.Close();
                await panel.LoadAsync(panel.Session.DirectoryPath, selectName: name);
                panel.FocusCurrent();
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            { dialog.Error.Text = failure.Message; }
        });
        dialog.Closed += (_, _) => { ActiveDialog = null; RecordActivity(); panel.FocusCurrent(); };
        _ = dialog.ShowDialog(this);
        dialog.Editor.Focus();
    }

    private void OpenNavigation(PanelView panel, DirectoryList initial)
    {
        if (HasOpenDialog) return;
        panel.CancelRename();
        ResetQuickJump();
        altInput = false;
        var suggestions = DirectoryUsage.Suggest(panel.Session.DirectoryPath, DateTimeOffset.UtcNow);
        var dialog = new DirectoryNavigationDialog(panel.Session.PreviousDirectories, suggestions, Favorites, panel.Session.DirectoryPath, initial, Feedback) { Background = Background };
        Feedback.Attach(dialog);
        ActiveNavigationDialog = dialog;
        dialog.Changed += () => Execute(async () => { if (FavoritesChanged != null) await FavoritesChanged(); });
        dialog.OpenRequested += directory => Execute(async () =>
        {
            if (dialog.IsOpening) return;
            dialog.SetOpening(true);
            var loaded = await panel.LoadAsync(directory);
            dialog.SetOpening(false);
            if (!dialog.IsVisible) return;
            if (loaded) { SetError(null); dialog.Close(); }
            else dialog.ShowError(panel.Session.Error ?? "The directory could not be opened.");
        });
        dialog.Closed += (_, _) =>
        {
            if (dialog.IsOpening) panel.Session.CancelLoad();
            ActiveNavigationDialog = null;
            RecordActivity();
            panel.FocusCurrent();
        };
        _ = dialog.ShowDialog(this);
        dialog.FocusSelection();
    }
}

public sealed class NameDialog : Window
{
    public TextBox Editor { get; } = new() { Name = "DirectoryName", PlaceholderText = "Folder name" };
    public TextBlock Error { get; } = new() { Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap };
    public event Action<string>? Submitted;
    public NameDialog(FeedbackController? feedback = null)
    {
        Title = "New folder"; Width = 440; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(Editor, "new-folder.name");
        var create = new Button { Content = "Create", Name = "CreateFolder", HorizontalAlignment = HorizontalAlignment.Right };
        create.Click += (_, _) => Submitted?.Invoke(Editor.Text ?? "");
        Content = new StackPanel
        {
            Margin = new Thickness(14),
            Spacing = 8,
            Children = { new TextBlock { Text = "Folder name" }, Editor, Error, create }
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback?.IsPickingIn(this) == true) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter) { Submitted?.Invoke(Editor.Text ?? ""); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }
}

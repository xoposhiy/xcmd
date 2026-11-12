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
    private readonly TextBlock status = new() { Text = "Ready", FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary };
    private bool altInput;
    private int pendingActions;
    private readonly TextBlock updateMessage = new() { FontSize = UiPalette.MetaSize, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420, TextWrapping = TextWrapping.Wrap };
    private long lastInput = Stopwatch.GetTimestamp();
    private bool restarting;
    private bool pointerHeld;
    private Menu? mainMenu;
    private readonly CommanderFrame frame;
    public Grid TitleBar => frame.TitleBar;
    private readonly FileViewingController viewing;
    public FileSearchController FileSearch { get; }
    private readonly ArchiveController archives;
    public FileViewerWindow? Viewer => viewing.Viewer;
    public IReadOnlyList<FileViewerWindow> Viewers => viewing.Viewers;
    public string ViewerThemeId => viewing.ThemeId;
    public event Func<Task>? ViewerThemeChanged;
    internal async Task NotifyViewerThemeChangedAsync() { if (ViewerThemeChanged != null) await ViewerThemeChanged(); }
    public bool IsMenuOpen => mainMenu?.IsOpen == true;
    public PanelView Left { get; }
    public PanelView Right { get; }
    public PanelView ActivePanel { get; private set; }
    public PanelView OppositePanel => ActivePanel == Left ? Right : Left;
    public NameDialog? ActiveDialog { get; private set; }
    public DirectoryNavigationDialog? ActiveNavigationDialog { get; private set; }
    public bool HasOpenDialog => ActiveDialog != null || ActiveNavigationDialog != null || OperationDialog != null;
    public FavoriteDirectories Favorites { get; } = new();
    public event Func<Task>? FavoritesChanged;
    public bool IsBusy => pendingActions > 0 || Left.Session.IsLoading || Right.Session.IsLoading || Operations.HasPendingWork || viewing.IsBusy;
    public string? LastError { get; private set; }
    public string StatusMessage => status.Text ?? "";
    public List<string> Trace { get; } = [];
    public FeedbackController Feedback { get; }
    public DirectoryUsageBook DirectoryUsage { get; } = new();
    private bool restoringVisits;
    public RestartReadiness Readiness => new(IsBusy, nameJump.Active || pointerHeld || CommandLine.HasKeyboardFocus || Left.EditingRow != null || Right.EditingRow != null,
        HasOpenDialog || FileSearch.IsOpen || IsMenuOpen || viewing.HasViewers, Feedback.IsActive, Stopwatch.GetElapsedTime(lastInput));

    public void SetUpdateMessage(string message) => updateMessage.Text = message;
    public void RecordActivity() => lastInput = Stopwatch.GetTimestamp();
    public void SetRestarting(bool value) { restarting = value; IsEnabled = !value; }

    public CommanderWindow(IFileSystem? fileSystem = null, IExternalLauncher? externalLauncher = null, string? editorPath = null, string? archiveCacheRoot = null, IArchiveContents? archiveContents = null)
    {
        archives = new ArchiveController(this, archiveCacheRoot, archiveContents);
        this.externalLauncher = externalLauncher ?? new WindowsExternalLauncher();
        fileSystem ??= new LocalFileSystem();
        fileSystem = archives.Wrap(fileSystem);
        viewing = new FileViewingController(this, editorPath, archives.Files);
        FileSearch = new FileSearchController(this, viewing, archiveCacheRoot);
        operationStore = fileSystem as IFileOperationStore ?? new LocalFileOperations(fileSystem);
        var decisions = new FileOperationDecisions(this);
        Operations = new FileOperationQueue(new FileOperationRunner(operationStore, decisions.ResolveConflictAsync, decisions.ResolveErrorAsync));
        OperationsView = new FileOperationsView(Operations);
        Operations.Finished += job =>
        {
            Execute(RefreshOperationPanelsAsync);
            var failure = job.Outcomes.FirstOrDefault(result => result.Kind == FileOutcomeKind.Failed);
            if (failure != null) SetError(Path.GetFileName(failure.Source) + ": " + failure.Message);
            else status.Text = $"{job.Request.Kind}: {job.ProcessedFiles:N0}/{job.TotalFiles:N0} files · {job.SkippedFiles:N0} skipped · {job.State}";
        };
        Title = "Xcmd";
        Icon = ApplicationIcon.Load();
        Width = 1280; Height = 760; MinWidth = 840; MinHeight = 400;
        Background = UiPalette.Canvas;
        var fileTypes = FileTypeSettings.Bundled();
        Left = new PanelView("left", fileSystem, fileTypes);
        Right = new PanelView("right", fileSystem, fileTypes);
        ActivePanel = Left;
        OperationsView.FilesRequested += () => ActivePanel.FocusCurrent();
        foreach (var panel in new[] { Left, Right })
        {
            panel.Activated += Activate;
            panel.OpenRequested += source => Execute(() => OpenCurrentAsync(source));
            panel.HistoryRequested += panel => OpenNavigation(panel, DirectoryList.History);
            panel.LocationVisited += path => { if (!restoringVisits && !ArchivePath.IsEntry(path)) DirectoryUsage.Seed([path]); if (panel == ActivePanel) CommandLine.SetDirectory(ArchivePath.WorkingDirectory(path)); };
            panel.LoadingRequested += source => { if (source == sizePanel) CancelDirectorySizes(); };
        }
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto,20"), Margin = new Thickness(12, 0, 12, 6) };
        frame = new CommanderFrame(this, mainMenu = BuildMenu(), status, updateMessage);
        var panels = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
        panels.Children.Add(Left);
        Grid.SetColumn(Right, 2); panels.Children.Add(Right);
        layout.Children.Add(panels);
        var help = CommanderFrame.BuildKeyboardHelp();
        Grid.SetRow(OperationsView, 1); layout.Children.Add(OperationsView);
        Grid.SetRow(CommandLine, 2); layout.Children.Add(CommandLine);
        CommandLine.Focused += () => { Left.SetActive(false); Right.SetActive(false); };
        Grid.SetRow(help, 3); layout.Children.Add(help);
        Grid.SetRow(frame.StatusBar, 4); layout.Children.Add(frame.StatusBar);
        var applicationLayout = new Grid { RowDefinitions = new RowDefinitions("32,*") };
        applicationLayout.Children.Add(frame.TitleBar);
        Grid.SetRow(layout, 1); applicationLayout.Children.Add(layout);
        var root = new Grid();
        root.Children.Add(applicationLayout);
        Content = root;
        Feedback = new FeedbackController(this, root);
        frame.AttachFeedback(Feedback);
        AddHandler(PointerMovedEvent, (_, _) => { lastInput = Stopwatch.GetTimestamp(); ResetQuickJump(); }, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => { lastInput = Stopwatch.GetTimestamp(); ResetQuickJump(); pointerHeld = true; }, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => { lastInput = Stopwatch.GetTimestamp(); pointerHeld = false; }, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, _) => lastInput = Stopwatch.GetTimestamp(), RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, (_, _) => { RecordActivity(); ResetQuickJump(); }, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => { pointerHeld = false; launchKeysHeld.Clear(); ResetQuickJump(); };
        Closing += (_, _) => { CancelDirectorySizes(); archives.CancelOpening(); Operations.CancelAll(); OperationDialog?.Close(); FileSearch.Close(); viewing.CloseAll(); };
        Closed += async (_, _) => { await Operations.Completion; await Task.Run(archives.Dispose); };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => { launchKeysHeld.Remove(e.Key); if (e.Key is Key.LeftAlt or Key.RightAlt) { altInput = false; suppressQuickText = false; e.Handled = true; } }, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        Left.SetActive(true);
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
        CommandLine.SetDirectory(ArchivePath.WorkingDirectory(panel.Session.DirectoryPath));
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

    internal void SetError(string? error)
    {
        LastError = error;
        status.Text = error ?? "Ready";
        if (error != null) Trace.Add("error: " + error);
    }

    internal void SetStatusMessage(string message) => status.Text = message;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (restarting) { e.Handled = true; return; }
        if (Feedback.IsPicking) { if (e.Key == Key.Escape) Feedback.Cancel(); e.Handled = true; return; }
        if (Feedback.ActiveComment != null || Feedback.ActiveNotes != null) return;
        if (IsMenuOpen) return;
        if (HasOpenDialog) return;
        if (e.Key == Key.F7 && e.KeyModifiers == KeyModifiers.Alt)
        { ResetQuickJump(); FileSearch.Open(); e.Handled = true; return; }
        if (OperationsView.IsKeyboardFocusWithin)
        {
            if (e.Key == Key.Escape) { ActivePanel.FocusCurrent(); e.Handled = true; }
            return;
        }
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
        if (!row.Entry.IsDirectory && ArchivePath.OpensOnEnter(row.Entry.FullPath))
        { await OpenArchiveAsync(panel); return; }
        if (!row.Entry.IsDirectory)
        { await OpenFileExternallyAsync(row.Entry.FullPath, panel.OperationDirectory); return; }
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
        if (panel.IsSearchResults) { SetError("Open a folder before creating a directory (Escape leaves search results)."); return; }
        if (ArchivePath.IsEntry(panel.Session.DirectoryPath)) { SetError("Archives are read-only. Choose a regular folder."); return; }
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
    public TextBlock Error { get; } = UiLayout.Error();
    public event Action<string>? Submitted;
    public NameDialog(FeedbackController? feedback = null)
    {
        Title = "New folder"; Width = 440; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(Editor, "new-folder.name");
        var create = new Button { Content = "Create", Name = "CreateFolder", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Name = "CancelCreateFolder" };
        cancel.Click += (_, _) => Close();
        create.Click += (_, _) => Submitted?.Invoke(Editor.Text ?? "");
        DialogActions.FollowFocus(this, create, cancel);
        Content = new StackPanel
        {
            Margin = UiPalette.FormMargin,
            Spacing = 8,
            Children = { new TextBlock { Text = "Folder name" }, Editor, Error, UiLayout.Actions(create, cancel) }
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (feedback?.IsPickingIn(this) == true) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && Editor.IsKeyboardFocusWithin) { Submitted?.Invoke(Editor.Text ?? ""); e.Handled = true; }
        }, RoutingStrategies.Tunnel);
    }
}

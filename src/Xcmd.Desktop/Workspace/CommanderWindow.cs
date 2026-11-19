// @implements NavigatePanels, JumpToName, RecentLocations, FavoriteLocations, RecognizeFileTypes, CreateFolder, CreateFile, QueueOperations, FindFiles, QuickViewFile, BrowseArchives, OpenExternalTool, RunCommand, TryLocalRelease
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
using Xcmd.Desktop.Features.CommandLine;
using Xcmd.Desktop.Features.Feedback;
using Xcmd.Desktop.Features.FileOperations;
using Xcmd.Desktop.Features.Git;
using Xcmd.Desktop.Features.Navigation;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Desktop.Features.Search;
using Xcmd.Desktop.Features.Viewer;
using Xcmd.Desktop.Shared.Platform;

namespace Xcmd.Desktop.Workspace;

public sealed partial class CommanderWindow : Window
{
    private readonly TextBlock status = new TextBlock() { Text = "Ready", FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary }.WithFeedbackId("CommanderWindow.status");
    private bool altInput;
    private int pendingActions;
    private readonly TextBlock updateMessage = new TextBlock() { FontSize = UiPalette.MetaSize, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420, TextWrapping = TextWrapping.Wrap }.WithFeedbackId("CommanderWindow.updateMessage");
    private long lastInput = Stopwatch.GetTimestamp();
    private readonly Stopwatch runningTime = Stopwatch.StartNew();
    private bool freshRelease;
    private bool restarting;
    private bool pointerHeld;
    private Menu? mainMenu;
    private readonly CommanderFrame frame;
    private readonly CommandLineController commandLineController;
    public CommandLineView CommandLine { get; } = new CommandLineView().WithFeedbackId("CommanderWindow.Launching.CommandLine");
    public Grid TitleBar => frame.TitleBar;
    private readonly FileViewingController viewing;
    public FileSearchController FileSearch { get; }
    public GitHistoryController GitHistory { get; }
    private readonly ArchiveController archives;
    public FileViewerWindow? Viewer => viewing.Viewer;
    public IReadOnlyList<FileViewerWindow> Viewers => viewing.Viewers;
    public string ViewerThemeId => viewing.ThemeId;
    public event Func<Task>? ViewerThemeChanged;
    internal async Task NotifyViewerThemeChangedAsync() { if (ViewerThemeChanged != null) await ViewerThemeChanged(); }
    private readonly ShellInteractionController shell;
    private readonly PanelClipboardController panelClipboard;
    public bool IsMenuOpen => shell.IsMenuOpen || mainMenu?.IsOpen == true || viewing.IsQuickViewMenuOpen || Left.Git.IsMenuOpen || Right.Git.IsMenuOpen;
    public PanelView Left { get; }
    public PanelView Right { get; }
    public PanelView ActivePanel { get; private set; }
    public PanelView OppositePanel => ActivePanel == Left ? Right : Left;
    public NameDialog? ActiveDialog { get; private set; }
    public DirectoryNavigationDialog? ActiveNavigationDialog { get; private set; }
    public bool HasOpenDialog => ActiveDialog != null || ActiveNavigationDialog != null || OperationDialog != null;
    public FavoriteDirectories Favorites { get; } = new();
    public event Func<Task>? FavoritesChanged;
    public bool IsBusy => pendingActions > 0 || Left.Session.IsLoading || Right.Session.IsLoading || Left.IsCommitMessageLoading || Right.IsCommitMessageLoading || Operations.HasPendingWork || viewing.IsBusy || panelClipboard?.IsBusy == true || shell.IsDragBusy;
    public string? LastError { get; private set; }
    public string StatusMessage => status.Text ?? "";
    public List<string> Trace { get; } = [];
    public FeedbackController Feedback { get; }
    public DirectoryUsageBook DirectoryUsage { get; } = new();
    private bool restoringVisits;
    public RestartReadiness Readiness => new(IsBusy, nameJump.Active || pointerHeld || IsActive && CommandLine.HasFocusedDraft || Left.EditingRow != null || Right.EditingRow != null,
        HasOpenDialog || FileSearch.IsOpen || IsMenuOpen, Feedback.IsActive, Stopwatch.GetElapsedTime(lastInput),
        MainWindowActive: IsActive, ViewerActive: viewing.HasActiveViewer);

    private string updateText = "";
    private bool updateReady;
    public string UpdateMessage => updateMessage.Text ?? "";
    public void SetUpdateMessage(string message) { freshRelease = false; updateText = message; RefreshUpdateMessage(); }
    internal void SetUpdateReady(bool ready) { updateReady = ready; RefreshUpdateMessage(); }
    private void RefreshUpdateMessage() => updateMessage.Text = !updateReady ? updateText
        : updateText.Length == 0 ? "Update ready · Restarts when idle" : "Update ready · " + updateText;
    public void ShowUpdatedMessage() { SetUpdateMessage("Just updated · new version"); freshRelease = true; }
    public void RecordActivity()
    {
        lastInput = Stopwatch.GetTimestamp();
        if (freshRelease) SetUpdateMessage("");
    }
    internal void EndPointerDrag() { pointerHeld = false; RecordActivity(); }
    public void RefreshUptime() => UpdateUptime(runningTime.Elapsed);
    public string UptimeText { get; private set; } = "Uptime: 0 min.";
    public string ReleaseCaption => Feedback?.ReleaseId is { } release && release != "development" ? "Xcmd · " + release : "Xcmd";
    public void UpdateUptime(TimeSpan elapsed)
    {
        UptimeText = $"Uptime: {Math.Max(0, (long)elapsed.TotalMinutes)} min.";
        var title = ReleaseCaption + " · " + UptimeText;
        if (Title != title) Title = title;
    }
    public void SetRestarting(bool value)
    {
        restarting = value;
        IsEnabled = !value;
        foreach (var viewer in Viewers) viewer.IsEnabled = !value;
    }

    public CommanderWindow(IFileSystem? fileSystem = null, IExternalLauncher? externalLauncher = null, string? editorPath = null, string? archiveCacheRoot = null, IArchiveContents? archiveContents = null, ShellInteractionController? shellInteraction = null, string? claudeExecutable = null, string? comparisonExecutable = null)
    {
        var ui = new UiActions(Execute, SetStatusMessage, SetError, Trace.Add);
        shell = shellInteraction ?? new ShellInteractionController();
        archives = new ArchiveController(archiveCacheRoot, archiveContents, ui, LaunchExternalAsync,
            panel => ActivePanel == panel && !CommandLine.HasKeyboardFocus && !HasOpenDialog);
        commandLineController = new CommandLineController(CommandLine, archives.Files, ui, GetActivePanel, FocusAfterCommandEditor,
            panel => ActivePanel == panel && !HasOpenDialog && !IsMenuOpen, StopNameSearch, LaunchExternalAsync,
            () => launchKeysHeld.Add(Key.Enter), EscapeCommandLine);
        this.externalLauncher = externalLauncher ?? new WindowsExternalLauncher();
        fileSystem ??= new LocalFileSystem();
        fileSystem = archives.Wrap(fileSystem);
        var root = new Grid().WithFeedbackId("CommanderWindow.root");
        Feedback = new FeedbackController(this, root, new FeedbackActions(Execute, SetUpdateMessage, StopNameSearch,
            RecordActivity, () => !HasOpenDialog && !IsBusy, RestoreFeedbackFocus, LaunchExternalAsync, Trace.Add));
        viewing = new FileViewingController(this, Feedback, archives.Files, editorPath, comparisonExecutable, ui,
            LaunchExternalAsync, OpenFileExternallyAsync);
        viewing.ViewerClosed += OnViewerClosed;
        viewing.ThemeChanged += () => Execute(NotifyViewerThemeChangedAsync);
        viewing.MissingAfterRestore += SetUpdateMessage;
        GitHistory = new GitHistoryController(this, GetActivePanel, () => OppositePanel, Activate, ui,
            () => LastError != null, CancelPanelLoads, viewing.ViewDocument);
        FileSearch = new FileSearchController(this, Feedback, Trace.Add, archiveCacheRoot);
        FileSearch.OpenRequested += ShowFileSearch;
        operationStore = fileSystem as IFileOperationStore ?? new LocalFileOperations(fileSystem);
        fileSystem = GitHistory.Wrap(fileSystem);
        var operationDialogs = new OperationDialogHost(this, Feedback, TrackOperationDialog, RecordActivity);
        decisions = new FileOperationDecisions(operationDialogs, operationStore, ui, CanShowOperationDecision, FocusAfterOperationDialog);
        Operations = new FileOperationQueue(new FileOperationRunner(operationStore, decisions.ResolveConflictAsync, decisions.ResolveErrorAsync));
        decisions.Confirmed += request => Operations.Enqueue(request);
        OperationsView = new FileOperationsView(Operations).WithFeedbackId("CommanderWindow.content");
        AttachOperationEvents();
        Title = "Xcmd";
        Icon = ApplicationIcon.Load();
        Width = 1280; Height = 760; MinWidth = 840; MinHeight = 400;
        Background = UiPalette.Canvas;
        var fileTypes = FileTypeSettings.Bundled();
        Left = new PanelView("left", fileSystem, fileTypes).WithFeedbackId("CommanderWindow.content.2");
        Right = new PanelView("right", fileSystem, fileTypes).WithFeedbackId("CommanderWindow.content.3");
        ActivePanel = Left;
        panelClipboard = new PanelClipboardController(this, operationStore, Operations, [Left, Right], ui);
        AttachPanelEvents();
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto,20"), Margin = new Thickness(12, 0, 12, 6) }.WithFeedbackId("CommanderWindow.layout");
        frame = new CommanderFrame(this, mainMenu = BuildMenu(), status, updateMessage);
        var panels = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") }.WithFeedbackId("CommanderWindow.panels");
        panels.Children.Add(Left);
        Grid.SetColumn(Right, 2); panels.Children.Add(Right);
        panels.Children.Add(viewing.QuickViewControl);
        layout.Children.Add(panels);
        var help = CommanderFrame.BuildKeyboardHelp(this);
        Grid.SetRow(OperationsView, 1); layout.Children.Add(OperationsView);
        Grid.SetRow(CommandLine, 2); layout.Children.Add(CommandLine);
        CommandLine.Focused += () => { Left.SetActive(false); Right.SetActive(false); };
        Grid.SetRow(help, 3); layout.Children.Add(help);
        Grid.SetRow(frame.StatusBar, 4); layout.Children.Add(frame.StatusBar);
        var applicationLayout = new Grid { RowDefinitions = new RowDefinitions("32,*") }.WithFeedbackId("CommanderWindow.applicationLayout");
        applicationLayout.Children.Add(frame.TitleBar);
        Grid.SetRow(layout, 1); applicationLayout.Children.Add(layout);
        root.Children.Add(applicationLayout);
        Content = root;
        GitCommitController.Attach([Left, Right], Activate, RestoreFocusAfterGitMenu,
            () => GitHistory.Open(selectedItem: false), ui, LaunchExternalAsync, claudeExecutable);
        var shellContext = new ShellInteractionContext(this, [Left, Right], Activate, ui, RecordActivity);
        var packing = new ZipPackingController(operationDialogs, operationStore, Operations, Execute, () => HasOpenDialog);
        shell.Attach(shellContext, packing, () => !Feedback.IsActive && !HasOpenDialog,
            () => !Feedback.IsActive && !HasOpenDialog && !IsMenuOpen, EndPointerDrag);
        frame.AttachFeedback(Feedback);
        AttachWindowEvents();
        Left.SetActive(true);
        RefreshUptime();
    }

    private void AttachPanelEvents()
    {
        OperationsView.FilesRequested += () => ActivePanel.FocusCurrent();
        foreach (var panel in new[] { Left, Right })
        {
            panel.Activated += Activate;
            panel.OpenRequested += source => Execute(() => OpenCurrentAsync(source));
            panel.HistoryRequested += panel => OpenNavigation(panel, DirectoryList.History);
            panel.LocationVisited += path => { if (!restoringVisits && !ArchivePath.IsEntry(path)) DirectoryUsage.Seed([path]); if (panel == ActivePanel) CommandLine.SetDirectory(ArchivePath.WorkingDirectory(path)); GitHistory.UpdateCommands(); };
            panel.LoadingRequested += source => { if (source == sizePanel) CancelDirectorySizes(); };
        }
    }

    private void AttachWindowEvents()
    {
        AddHandler(PointerMovedEvent, (_, _) => { RecordActivity(); ResetQuickJump(); }, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => { RecordActivity(); ResetQuickJump(); pointerHeld = true; }, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => { RecordActivity(); pointerHeld = false; }, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, _) => RecordActivity(), RoutingStrategies.Tunnel, handledEventsToo: true);
        Activated += (_, _) => { lastInput = Stopwatch.GetTimestamp(); viewing.RefreshQuickViewAfterActivation(); };
        AddHandler(PointerWheelChangedEvent, (_, _) => { RecordActivity(); ResetQuickJump(); }, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => { pointerHeld = false; launchKeysHeld.Clear(); favoritesKeyHeld = false; viewing.ResetQuickViewKeys(); ResetQuickJump(); };
        Closing += (_, _) => { CancelDirectorySizes(); archives.CancelOpening(); Operations.CancelAll(); OperationDialog?.Close(); FileSearch.Close(); viewing.CloseAll(); };
        Closed += async (_, _) => { await Operations.Completion; await Task.Run(archives.Dispose); };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
    }

    public async Task InitializeAsync(string left, string right)
    {
        await Task.WhenAll(Left.LoadAccessibleAsync(left), Right.LoadAccessibleAsync(right));
        Activate(Left);
        Left.FocusCurrent();
    }

    public void Activate(PanelView panel)
    {
        if (ActivePanel != panel) { viewing.CloseQuickView(); ActivePanel.CancelRename(); ResetQuickJump(); }
        ActivePanel = panel;
        Left.SetActive(panel == Left);
        Right.SetActive(panel == Right);
        CommandLine.SetDirectory(ArchivePath.WorkingDirectory(panel.Session.DirectoryPath));
        GitHistory.UpdateCommands();
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
        if (IsFavoritesKey(e)) favoritesKeyHeld = true;
        if (restarting) { e.Handled = true; return; }
        if (Feedback.IsPicking) { if (e.Key == Key.Escape) Feedback.Cancel(); e.Handled = true; return; }
        if (Feedback.ActiveComment != null || Feedback.ActiveNotes != null) return;
        if (IsMenuOpen) return;
        if (HasOpenDialog) return;
        if (RouteQuickViewInput(e)) return;
        if (HandlePanelSwitch(e)) return;
        if (HandleFavoritesShortcut(e)) { e.Handled = true; return; }
        if (HasGitActionsFocus) return;
        if (GitHistory.CancelPending(e)) { e.Handled = true; return; }
        if (e.Key == Key.F7 && e.KeyModifiers == KeyModifiers.Alt)
        { ResetQuickJump(); FileSearch.Open(); e.Handled = true; return; }
        if (HandleFocusedInput(e)) return;
        if (GitHistory.HandleKey(e)) { ResetQuickJump(); e.Handled = true; return; }
        altInput = e.KeyModifiers.HasFlag(KeyModifiers.Alt) || e.Key is Key.LeftAlt or Key.RightAlt;
        if (ActivePanel.EditingRow != null) { HandleRenameKey(e); return; }
        if (HandleQuickViewKey(e)) { e.Handled = true; return; }
        if (HandlePanelShortcut(e)) { e.Handled = true; return; }
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (HandleNameSearchKey(e, ctrl)) return;
        if (!HandlePanelCommand(e, ctrl, shift) && !HandlePanelNavigation(e, ctrl, shift)) return;
        Trace.Add($"key: {e.KeyModifiers}+{e.Key}; panel: {ActivePanel.Id}");
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        launchKeysHeld.Remove(e.Key);
        if (ReleaseFavoritesKey(e)) { e.Handled = true; return; }
        if (e.Key is not (Key.LeftAlt or Key.RightAlt)) return;
        altInput = false;
        suppressQuickText = false;
        if (!viewing.IsQuickViewFocused) e.Handled = true;
    }

    private bool HandleFocusedInput(KeyEventArgs e)
    {
        if (OperationsView.IsKeyboardFocusWithin)
        {
            if (e.Key == Key.Escape) { ActivePanel.FocusCurrent(); e.Handled = true; }
            return true;
        }
        if (CommandLine.HasKeyboardFocus)
        {
            if (HandleDirectoryDestinationKey(e) || commandLineController.HandleEditorKey(e)) e.Handled = true;
            return true;
        }
        if (ActivePanel.HasCommitMessageFocus) { ActivePanel.HandleCommitMessageKey(e); return true; }
        return false;
    }

    private async Task OpenCurrentAsync(PanelView panel)
    {
        if (panel.HasHistory) { await GitHistory.OpenCurrentAsync(panel); return; }
        if (panel.Current is not { } row) return;
        if (row.Entry.IsParent) { await OpenParentAsync(panel); return; }
        if (!row.Entry.IsDirectory && ArchivePath.OpensOnEnter(row.Entry.FullPath))
        { await OpenArchiveAsync(panel); return; }
        if (!row.Entry.IsDirectory)
        { await OpenFileExternallyAsync(row.Entry.FullPath, panel.OperationDirectory); return; }
        await panel.LoadAsync(row.Entry.FullPath, selectName: "..");
        panel.FocusCurrent();
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (restarting || Feedback.IsActive) return;
        if (ActivePanel.EditingRow != null || HasOpenDialog || IsMenuOpen || viewing.IsQuickViewFocused || CommandLine.HasKeyboardFocus || string.IsNullOrEmpty(e.Text)) return;
        if (!nameJump.Active)
        { CommandLine.InsertText(e.Text); e.Handled = true; return; }
        if (suppressQuickText) { suppressQuickText = false; e.Handled = true; return; }
        JumpText(e.Text);
        e.Handled = true;
    }

    private void OpenCreateDirectoryDialog()
    {
        var panel = ActivePanel;
        if (panel.IsSearchResults) { SetError("Open a folder before creating a directory (Escape leaves search results)."); return; }
        if (ArchivePath.IsEntry(panel.Session.DirectoryPath)) { SetError(ArchivePath.ReadOnlyMessage(panel.Session.DirectoryPath, "Archives are read-only. Choose a regular folder.")); return; }
        var dialog = new NameDialog(Feedback) { Background = Background }.WithFeedbackId("CommanderWindow.dialog");
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
        var dialog = new DirectoryNavigationDialog(panel.Session.PreviousDirectories, suggestions, Favorites, panel.Session.DirectoryPath, initial, Feedback) { Background = Background }.WithFeedbackId("CommanderWindow.dialog.2");
        Feedback.Attach(dialog);
        ActiveNavigationDialog = dialog;
        dialog.Changed += () => Execute(async () => { if (FavoritesChanged != null) await FavoritesChanged(); });
        dialog.OpenRequested += directory => Execute(async () =>
        {
            if (dialog.IsOpening) return;
            dialog.SetOpening(true);
            var loaded = await panel.LoadAsync(directory, nearestExistingParent: true);
            dialog.SetOpening(false);
            if (!dialog.IsVisible) return;
            if (loaded)
            {
                SetError(null); dialog.Close();
                SetStatusMessage(DirectoryNavigationDialog.OpenedMessage(directory, panel.Session.DirectoryPath));
            }
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
        Execute(dialog.RefreshAvailabilityAsync);
    }
}

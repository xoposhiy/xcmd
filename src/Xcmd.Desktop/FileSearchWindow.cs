using System.Collections.Concurrent;
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
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class FileSearchWindow : Window
{
    private string root;
    private bool rootIsFile;
    private readonly TextBlock rootLabel = new() { Name = "SearchRoot", TextTrimming = TextTrimming.PrefixCharacterEllipsis };
    private readonly string? cacheRoot;
    private readonly FeedbackController feedback;
    private ConcurrentQueue<FileSearchResult> incoming = new();
    private readonly ObservableCollection<FileSearchResult> results = [];
    private readonly TextBlock status = new() { Name = "SearchStatus", Text = "Enter a name or text, then press Enter.", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock details = new() { Name = "SearchDetails", Foreground = UiPalette.Error, TextWrapping = TextWrapping.Wrap, MaxHeight = 100 };
    private readonly CheckBox matchCase = new() { Content = "Match text case", Name = "SearchMatchCase" };
    private readonly CheckBox regex = new() { Content = "Regex", Name = "SearchRegex" };
    private readonly CheckBox literalName = new() { Content = "Treat * and ? as text", Name = "SearchLiteralName" };
    private readonly ComboBox nameMode = new() { Name = "SearchNameMode", ItemsSource = new[] { "Contains", "Whole name" }, MinWidth = 135 };
    private readonly QueryHistoryPopup nameHistory;
    private readonly QueryHistoryPopup textHistory;
    private readonly TextBlock pendingQuery = new() { Name = "SearchPendingQuery", Foreground = Brushes.LightSkyBlue, IsVisible = false, TextWrapping = TextWrapping.Wrap };
    private readonly Button start = new() { Content = "Search", Name = "SearchStart" };
    private readonly Button stop = new() { Content = "Stop", Name = "SearchStop", IsEnabled = false };
    private readonly Button expand = new() { Content = new AccessText { Text = "Search skipped f_iles", ShowAccessKey = true }, Name = "SearchExpand", IsVisible = false };
    private readonly Button view = new() { Content = "View · F3", Name = "SearchView", IsEnabled = false };
    private readonly Button go = new() { Content = "Go to file", Name = "SearchGo", IsEnabled = false };
    private readonly Button toPanel = new() { Content = new AccessText { Text = "To _panel", ShowAccessKey = true }, Name = "SearchToPanel", IsEnabled = false };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private CancellationTokenSource? cancellation;
    private FileSearchSource? source;
    private FileSearchSession? search;
    private SearchFailure? fatalFailure;
    private bool closed;
    private bool expanded;
    private FileSearchQuery? activeQuery;
    private FileSearchQuery? invalidQuery;
    private readonly TextBlock queryError = new() { Name = "SearchQueryError", Foreground = UiPalette.Error, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    public TextBox NameEditor { get; } = new() { Name = "SearchName", PlaceholderText = "Part of a name, *.cs, or src/*.cs", MaxLength = 1024 };
    public TextBox TextEditor { get; } = new() { Name = "SearchText", PlaceholderText = "Text inside files (optional)", MaxLength = 1024 };
    public TableView Results { get; } = new() { Name = "SearchResults" };
    public Task Completion { get; private set; } = Task.CompletedTask;
    public bool IsSearching => !Completion.IsCompleted;
    public FileSearchSession? Session => search;
    public string SearchDirectory => rootIsFile ? ArchivePath.Parent(root)! : root;
    public string StatusMessage => status.Text ?? "";
    public event Action<FileSearchResult>? GoRequested;
    public event Action<string, IReadOnlyList<FileSearchResult>, string?>? PanelRequested;
    public event Action<IReadOnlyList<FileSearchResult>, int, string>? ViewRequested;

    public FileSearchWindow(string root, FeedbackController feedback, FileSearchState saved, string? cacheRoot = null)
    {
        this.root = root; this.feedback = feedback; this.cacheRoot = cacheRoot;
        Title = "Find files"; Width = 1040; Height = 690; MinWidth = 760; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AutomationProperties.SetAutomationId(this, "search.window");
        NameEditor.Text = saved.Name; TextEditor.Text = saved.Text;
        matchCase.IsChecked = saved.MatchCase; regex.IsChecked = saved.Regex; literalName.IsChecked = saved.LiteralName;
        nameMode.SelectedIndex = saved.WholeName ? 1 : 0;
        rootLabel.Text = "Search in  " + ArchivePath.Display(root);
        nameHistory = new(NameEditor, saved.NameHistory); textHistory = new(TextEditor, saved.TextHistory);
        foreach (var control in new Control[] { NameEditor, TextEditor, Results, start, stop, expand, view, go, status, details, matchCase, regex, literalName, nameMode })
            AutomationProperties.SetAutomationId(control, "search." + control.Name);
        var fields = new Grid { ColumnDefinitions = new("Auto,*,Auto"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 10, RowSpacing = 8 };
        fields.Children.Add(new AccessText { Text = "_File name", ShowAccessKey = true, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(NameEditor, 1); fields.Children.Add(NameEditor);
        Grid.SetColumn(nameMode, 2); fields.Children.Add(nameMode);
        var textLabel = new AccessText { Text = "Containing _text", ShowAccessKey = true, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(textLabel, 1); fields.Children.Add(textLabel);
        Grid.SetRow(TextEditor, 1); Grid.SetColumn(TextEditor, 1); Grid.SetColumnSpan(TextEditor, 2); fields.Children.Add(TextEditor);
        var options = new WrapPanel { Orientation = Orientation.Horizontal, Children = { matchCase, regex, literalName } };
        foreach (var option in options.Children) option.Margin = new Thickness(0, 0, 16, 0);
        ToolTip.SetTip(matchCase, "Text search ignores case by default, like grep -i. Enable to match case.");
        ToolTip.SetTip(regex, "Use a regular expression, like grep -E. The search uses .NET regular expressions, one line at a time.");
        ToolTip.SetTip(literalName, "Search for the characters * and ? instead of using them as wildcards. Whole name controls whether the entire name must match.");
        ToolTip.SetTip(nameMode, "Contains: match any part of the name. Whole name: match the complete name. Wildcard masks such as *.cs always match the complete name.");
        ToolTip.SetTip(expand, "Include Git-ignored files, dependency folders, environments, caches and version-control data. Keep current results. Alt+I.");
        Results.ItemsSource = results;
        ConfigureResults();
        Results.SelectionChanged += (_, _) => { view.IsEnabled = Results.SelectedItem is FileSearchResult { Entry.IsDirectory: false }; go.IsEnabled = Results.SelectedItem != null; UpdatePrimaryAction(); };
        Results.DoubleTapped += (_, e) => { if (e.Source is Control control && control.GetSelfAndVisualAncestors().OfType<TableViewRow>().Any()) Go(); };
        start.Click += (_, _) => StartSearch(); stop.Click += (_, _) => Stop();
        expand.Click += (_, _) => Expand(); view.Click += (_, _) => View(); go.Click += (_, _) => Go();
        AutomationProperties.SetAutomationId(toPanel, "search.to-panel");
        ToolTip.SetTip(toPanel, "Send the results found so far to the file panel. Escape in the panel returns to its previous folder.");
        toPanel.Click += (_, _) => SendToPanel();
        var close = new Button { Content = "Close", Name = "SearchClose" }; close.Click += (_, _) => Close();
        var actions = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(expand, Dock.Left); actions.Children.Add(expand);
        var feedbackButton = new Button { Content = "Feedback", Name = "SearchFeedback" };
        AutomationProperties.SetAutomationId(feedbackButton, "feedback.start"); ToolTip.SetTip(feedbackButton, "Comment on an element · F12");
        feedbackButton.Click += (_, _) => feedback.Begin(this);
        feedbackButton.Classes.Add("quiet");
        var right = UiLayout.Actions(toPanel, view, go, feedbackButton, close);
        DockPanel.SetDock(right, Dock.Right); actions.Children.Add(right);
        var layout = new Grid { Margin = UiPalette.FormMargin, RowDefinitions = new("Auto,Auto,Auto,Auto,*,Auto,Auto,Auto"), RowSpacing = 6 };
        var queryActions = new Grid { ColumnDefinitions = new("*,Auto,Auto"), ColumnSpacing = 8 };
        queryActions.Children.Add(options); Grid.SetColumn(start, 1); queryActions.Children.Add(start); Grid.SetColumn(stop, 2); queryActions.Children.Add(stop);
        AutomationProperties.SetAutomationId(queryError, "search.query-error");
        Control[] rows = [rootLabel, fields, queryActions,
            new StackPanel { Spacing = 2, Children = { pendingQuery, queryError } }, Results, status, BuildCoverageDetails(), actions];
        for (var index = 0; index < rows.Length; index++) { Grid.SetRow(rows[index], index); layout.Children.Add(rows[index]); }
        Content = layout;
        SearchTables.Restore(Results, saved.ResultColumns); RestoreSkippedColumns(saved.SkippedColumns); SearchTables.Restore(failureList, saved.FailureColumns);
        layout.Children.Add(nameHistory.Popup); layout.Children.Add(textHistory.Popup);
        NameEditor.TextChanged += (_, _) => QueryChanged(); TextEditor.TextChanged += (_, _) => QueryChanged();
        foreach (var option in new[] { matchCase, regex, literalName }) option.IsCheckedChanged += (_, _) => QueryChanged();
        nameMode.SelectionChanged += (_, _) => QueryChanged();
        timer.Tick += (_, _) => Flush(); timer.Start();
        Closed += (_, _) => { closed = true; timer.Stop(); nameHistory.Close(); textHistory.Close(); _ = RetireSearchAsync(); };
        AddHandler(KeyDownEvent, HandleKey, RoutingStrategies.Tunnel);
        AddHandler(GotFocusEvent, (_, _) => UpdatePrimaryAction());
        AddHandler(LostFocusEvent, (_, _) => UpdatePrimaryAction());
    }

    private void UpdatePrimaryAction()
    {
        start.Classes.Set("primary", QueryControlHasFocus || start.IsKeyboardFocusWithin);
        go.Classes.Set("primary", go.IsEnabled && (Results.IsKeyboardFocusWithin || go.IsKeyboardFocusWithin));
    }

    public void FocusQuery() { var editor = TextEditor.Text?.Length > 0 ? TextEditor : NameEditor; editor.Focus(); editor.SelectAll(); }
    public FileSearchState CaptureState() => new(NameEditor.Text ?? "", TextEditor.Text ?? "", nameHistory.Capture(), textHistory.Capture(),
        matchCase.IsChecked == true, regex.IsChecked == true, literalName.IsChecked == true, nameMode.SelectedIndex == 1,
        SearchTables.Capture(Results), SearchTables.Capture(deferredList), SearchTables.Capture(failureList));

    public void StartSearch()
    {
        var query = CaptureQuery();
        try { query.Validate(); }
        catch (ArgumentException error) { invalidQuery = query; queryError.Text = error.Message; queryError.IsVisible = true; return; }
        invalidQuery = null; queryError.IsVisible = false;
        if (IsSearching && query == activeQuery) return;
        nameHistory.Close(); textHistory.Close(); nameHistory.Add(query.Name); textHistory.Add(query.Text);
        activeQuery = query;
        pendingQuery.IsVisible = false;
        var previous = RetireSearchAsync();
        var request = new CancellationTokenSource(); cancellation = request;
        search = null; fatalFailure = null; results.Clear(); incoming = new(); details.Text = ""; expanded = false; expand.IsVisible = false;
        deferredShown = failuresShown = -1; deferredDetails.IsVisible = failureDetails.IsVisible = false;
        stop.IsEnabled = true; status.Text = "Searching project files…";
        Completion = RunSearchAsync(previous, query, request);
        Results.Focus();
    }

    private async Task RunSearchAsync(Task previous, FileSearchQuery query, CancellationTokenSource request)
    {
        await previous;
        try
        {
            if (request.IsCancellationRequested) return;
            source = new FileSearchSource(query.Root, cacheRoot);
            var queue = incoming;
            var session = new FileSearchSession(query, source, result => { if (!request.IsCancellationRequested) queue.Enqueue(result); });
            if (cancellation == request) search = session;
            await Task.Run(() => session.RunAsync(false, request.Token));
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { if (!closed && cancellation == request) fatalFailure = new(query.Root, error.Message, "Read", !query.RootIsFile); }
        finally
        {
            if (!closed && cancellation == request)
            {
                Flush(); stop.IsEnabled = false;
                if (request.IsCancellationRequested) status.Text = $"Stopped · {results.Count:N0} matches · Search is incomplete";
            }
        }
    }

    private void QueryChanged()
    {
        if (invalidQuery != null && CaptureQuery() != invalidQuery) { invalidQuery = null; queryError.IsVisible = false; }
        pendingQuery.IsVisible = activeQuery != null && CaptureQuery() != activeQuery;
        pendingQuery.Text = "Settings changed. Search to apply.";
        ToolTip.SetTip(pendingQuery, "Current results and work are kept. Press Enter in the query controls or Search to apply the changed settings.");
        if (activeQuery == null) status.Text = "Enter a name or text, then press Enter.";
    }

    private FileSearchQuery CaptureQuery() => new(root, NameEditor.Text ?? "", TextEditor.Text ?? "", matchCase.IsChecked == true, regex.IsChecked == true, literalName.IsChecked == true, nameMode.SelectedIndex == 1, rootIsFile);

    public void SearchLocation(string path, bool file)
    {
        root = path; rootIsFile = file;
        rootLabel.Text = "Search in  " + ArchivePath.Display(path);
        StartSearch();
    }

    public void Stop()
    {
        cancellation?.Cancel(); Flush(); stop.IsEnabled = false;
        status.Text = $"Stopped · {results.Count:N0} matches · Search is incomplete";
    }

    public void Expand()
    {
        if (expanded || cancellation == null || search == null) return;
        expanded = true; expand.IsEnabled = false;
        status.Text = "Skipped files will be searched after the current files…";
        Completion = ExpandAsync(Completion, search, cancellation);
    }

    private async Task ExpandAsync(Task previous, FileSearchSession session, CancellationTokenSource request)
    {
        await previous;
        if (!request.IsCancellationRequested) await Task.Run(() => session.RunAsync(true, request.Token));
        if (!closed && cancellation == request) Flush();
    }

    private async Task RetireSearchAsync()
    {
        var previous = Completion;
        var oldSource = source; source = null;
        var request = cancellation; cancellation = null;
        request?.Cancel();
        await previous;
        if (oldSource != null) await Task.Run(oldSource.Dispose);
        request?.Dispose();
    }

    private void Flush()
    {
        if (closed) return;
        for (var index = 0; index < 300 && incoming.TryDequeue(out var result); index++) results.Add(result);
        if (Results.SelectedIndex < 0 && results.Count > 0) Results.SelectedIndex = 0;
        toPanel.IsEnabled = results.Count > 0;
        if (search == null) return;
        IReadOnlyList<SearchFailure> failures = fatalFailure == null ? search.Failures : search.Failures.Append(fatalFailure).ToArray();
        UpdateCoverageDetails(failures);
        var state = SearchStatus(failures.Count);
        status.Text = ProgressText(state, failures.Count);
        expand.IsVisible = search.DeferredCount > 0;
        UpdateDeferredSummary();
        expand.IsEnabled = !expanded && cancellation?.IsCancellationRequested == false;
        stop.IsEnabled = search.IsRunning;
    }

    private string ProgressText(string state, int failures)
    {
        var message = $"{state} · {search!.ResultCount:N0} matches · {search.Visited:N0} checked";
        if (search.DeferredCount > 0) message += $" · {search.DeferredCount:N0} skipped";
        if (failures > 0) message += $" · {failures:N0} locations could not be searched";
        return message;
    }

    private string SearchStatus(int failures)
    {
        if (cancellation?.IsCancellationRequested == true || search!.Stopped) return "Stopped · Search is incomplete";
        if (search.Limited) return "Search limit reached · Narrow the query";
        if (search.IsRunning) return expanded ? "Searching all files…" : "Searching project files…";
        return failures > 0 ? "Search finished with incomplete coverage" : "Complete";
    }

    private void HandleKey(object? sender, KeyEventArgs e)
    {
        if (feedback.IsPickingIn(this)) return;
        if (nameHistory.HandleKey(e) || textHistory.HandleKey(e)) { e.Handled = true; return; }
        if (HandleQueryFocusKey(e)) { e.Handled = true; return; }
        if (e.Key == Key.Escape) { if (IsSearching) Stop(); else Close(); }
        else if (e.Key == Key.Enter && QueryControlHasFocus) StartSearch();
        else if (e.Key == Key.Enter && Results.IsKeyboardFocusWithin) Go();
        else if (e.Key == Key.P && e.KeyModifiers == KeyModifiers.Alt) SendToPanel();
        else if (e.Key == Key.F3) View();
        else if (e.Key == Key.I && e.KeyModifiers == KeyModifiers.Alt && expand.IsEnabled && expand.IsVisible) Expand();
        else return;
        e.Handled = true;
    }

    private bool QueryControlHasFocus => NameEditor.IsKeyboardFocusWithin || TextEditor.IsKeyboardFocusWithin || matchCase.IsKeyboardFocusWithin ||
        regex.IsKeyboardFocusWithin || literalName.IsKeyboardFocusWithin || nameMode.IsKeyboardFocusWithin && !nameMode.IsDropDownOpen;

    private bool HandleQueryFocusKey(KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Alt) { NameEditor.Focus(); NameEditor.SelectAll(); }
        else if (e.Key == Key.T && e.KeyModifiers == KeyModifiers.Alt || e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control) { TextEditor.Focus(); TextEditor.SelectAll(); }
        else if (e.Key == Key.Up && e.KeyModifiers == KeyModifiers.None && Results.IsKeyboardFocusWithin && Results.SelectedIndex <= 0) TextEditor.Focus();
        else if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.None && TextEditor.IsKeyboardFocusWithin) Results.Focus();
        else if (e.Key is Key.Up or Key.Down && e.KeyModifiers == KeyModifiers.None && (NameEditor.IsKeyboardFocusWithin || TextEditor.IsKeyboardFocusWithin))
            (NameEditor.IsKeyboardFocusWithin ? TextEditor : NameEditor).Focus();
        else return false;
        return true;
    }

    private void Go() { if (Results.SelectedItem is FileSearchResult result) GoRequested?.Invoke(result); }
    private void SendToPanel()
    {
        while (incoming.TryDequeue(out var result)) results.Add(result);
        Flush();
        if (results.Count > 0) PanelRequested?.Invoke(SearchDirectory, results.ToArray(), (Results.SelectedItem as FileSearchResult)?.Entry.FullPath);
    }
    private void View()
    {
        if (Results.SelectedItem is not FileSearchResult { Entry.IsDirectory: false } result) return;
        var files = results.Where(item => !item.Entry.IsDirectory).ToArray();
        ViewRequested?.Invoke(files, Array.IndexOf(files, result), activeQuery?.Text ?? "");
    }
}

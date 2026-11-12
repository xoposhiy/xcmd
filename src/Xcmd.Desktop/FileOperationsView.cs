using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class FileOperationsView : Border
{
    private readonly FileOperationQueue queue;
    private readonly TextBlock summary = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock currentPath = new() { Name = "CurrentOperationPath", FontSize = UiPalette.MetaSize, TextTrimming = TextTrimming.LeadingCharacterEllipsis, Foreground = UiPalette.Secondary };
    private readonly ListBox jobs = new() { Name = "FileOperationJobs", MinWidth = 260, Focusable = true };
    private readonly OperationDetailsView report = new();
    private readonly Grid details = new() { ColumnDefinitions = new ColumnDefinitions("2*,3*"), Height = 168, IsVisible = false };
    private readonly Button cancelCurrent = new() { Content = "Cancel current", Name = "CancelCurrentOperation" };
    private readonly Button cancelSelected = new() { Content = "Cancel selected", Name = "CancelSelectedOperation", Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button toggle = new() { Content = "Details", Name = "ToggleOperationDetails", Margin = new Thickness(8, 0, 0, 0) };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private long runningSince;
    private long finishedAt;
    private bool wasPending;
    private bool automaticallyShown;
    private FileOperationJob? timedJob;
    private long measuredAt;
    private bool timingRunning;
    private TimeSpan runningTime;
    public ProgressBar Progress { get; } = new() { Name = "FileOperationProgress", Minimum = 0, Maximum = 100, Height = 5 };
    public string ProgressSummary => summary.Text ?? "";
    public bool DetailsVisible => details.IsVisible;
    public event Action? FilesRequested;

    public FileOperationsView(FileOperationQueue queue)
    {
        this.queue = queue;
        Name = "FileOperations"; IsVisible = false; Padding = new Thickness(8); Margin = new Thickness(0, 4, 0, 2);
        Background = UiPalette.Raised;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
        header.Children.Add(new StackPanel { Height = 40, Spacing = 1, Children = { summary, currentPath } });
        Grid.SetColumn(cancelCurrent, 1); header.Children.Add(cancelCurrent);
        Grid.SetColumn(cancelSelected, 2); header.Children.Add(cancelSelected);
        Grid.SetColumn(toggle, 3); header.Children.Add(toggle);
        details.Children.Add(jobs); Grid.SetColumn(report, 1); details.Children.Add(report);
        Child = new StackPanel { Spacing = 6, Children = { header, Progress, details } };
        AutomationProperties.SetAutomationId(this, "file-operations");
        AutomationProperties.SetAutomationId(Progress, "file-operations.progress");
        cancelCurrent.Click += (_, _) => { if (Current() is { } job) queue.Cancel(job); };
        cancelSelected.Click += (_, _) => { if (Selected() is { } job) queue.Cancel(job); };
        toggle.Click += (_, _) => ToggleDetails();
        jobs.SelectionChanged += (_, _) => ShowReport();
        queue.Changed += Refresh;
        timer.Tick += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) => timer.Start();
        DetachedFromVisualTree += (_, _) => timer.Stop();
    }

    public void ToggleDetails()
    {
        if (queue.Jobs.Count == 0) return;
        details.IsVisible = !details.IsVisible; toggle.Content = details.IsVisible ? "Hide details" : "Details"; Refresh();
        if (details.IsVisible) FocusJobs();
        else FilesRequested?.Invoke();
    }

    private void FocusJobs() => Dispatcher.UIThread.Post(() =>
    {
        if (!details.IsVisible) return;
        jobs.UpdateLayout();
        if (jobs.ContainerFromIndex(jobs.SelectedIndex) is Control row) row.Focus();
        else jobs.Focus();
    }, DispatcherPriority.Input);

    private FileOperationJob? Current() => queue.Jobs.FirstOrDefault(job => !job.IsFinished && job.State != FileOperationState.Queued);
    private FileOperationJob? Selected() => jobs.SelectedIndex >= 0 && jobs.SelectedIndex < queue.Jobs.Count ? queue.Jobs[jobs.SelectedIndex] : null;

    private static string Label(FileOperationJob job) => $"#{job.Id} {job.Request.Kind} · {State(job.State)} · {job.Request.Sources.Length} selected";
    private static string State(FileOperationState state) => state switch
    {
        FileOperationState.CompletedWithErrors => "Completed with errors",
        FileOperationState.CompletedWithSkippedItems => "Completed with skipped items",
        FileOperationState.Counting => "Counting files",
        FileOperationState.WaitingForConflict => "Waiting for a conflict decision",
        FileOperationState.WaitingForError => "Waiting for an error decision",
        _ => state.ToString()
    };

    private void Refresh()
    {
        var focusedJobs = jobs.IsKeyboardFocusWithin;
        var focusedCurrent = cancelCurrent.IsFocused;
        var focusedSelected = cancelSelected.IsFocused;
        UpdateVisibility();
        if (queue.Jobs.Count == 0) return;
        var current = Current() ?? queue.Jobs[^1];
        TrackRunningTime(current);
        if (!IsVisible) return;
        var waiting = queue.Jobs.Count(job => job.State == FileOperationState.Queued);
        var counts = current.State == FileOperationState.Counting ? $"Counting files · {current.TotalFiles:N0} found"
            : $"{current.ProcessedFiles:N0}/{current.TotalFiles:N0} files";
        var extra = current.SkippedFiles > 0 ? $" · {current.SkippedFiles:N0} skipped" : "";
        if (current.FailedEntries > 0) extra += $" · {current.FailedEntries:N0} failed";
        if (!current.TotalsComplete) extra += " · count incomplete";
        if (waiting > 0) extra += $" · {waiting} queued";
        var remaining = FileOperationProgress.Remaining(current, runningTime);
        var estimate = current.State == FileOperationState.Running ? remaining is { } eta ? " · " + Estimate(eta) : " · Estimating time" : "";
        var action = current.IsFinished || current.State == FileOperationState.WaitingForConflict ? $"{current.Request.Kind} · {State(current.State)}" : current.Request.Kind.ToString();
        var attention = current.State == FileOperationState.WaitingForConflict ? " · finish the current edit or dialog" : "";
        summary.Text = $"{action} · {counts}{extra}{estimate}{attention}";
        currentPath.Text = current.CurrentPath; ToolTip.SetTip(currentPath, current.CurrentPath);
        Progress.IsIndeterminate = current.State == FileOperationState.Counting;
        Progress.Value = FileOperationProgress.Fraction(current) * 100;
        ToolTip.SetTip(summary, summary.Text);
        cancelCurrent.IsEnabled = Current() is { State: not FileOperationState.Cancelling };
        cancelSelected.IsVisible = details.IsVisible;
        if (details.IsVisible) RefreshJobs(focusedJobs, focusedCurrent, focusedSelected);
    }

    private void RefreshJobs(bool focusedJobs, bool focusedCurrent, bool focusedSelected)
    {
        var selected = jobs.SelectedIndex;
        var labels = queue.Jobs.Select(Label).ToArray();
        var changed = jobs.ItemsSource is not string[] previous || !previous.SequenceEqual(labels);
        if (changed) jobs.ItemsSource = labels;
        jobs.SelectedIndex = selected >= 0 ? Math.Min(selected, queue.Jobs.Count - 1) : queue.Jobs.Count - 1;
        ShowReport();
        if (changed && focusedJobs || focusedCurrent && !cancelCurrent.IsEnabled || focusedSelected && !cancelSelected.IsEnabled) FocusJobs();
    }

    private void UpdateVisibility()
    {
        var pending = queue.HasPendingWork;
        var running = queue.Jobs.Any(job => job.State is FileOperationState.Counting or FileOperationState.Running or FileOperationState.WaitingForConflict or FileOperationState.Cancelling);
        if (running && runningSince == 0) runningSince = Stopwatch.GetTimestamp();
        if (!running) runningSince = 0;
        if (runningSince != 0 && Stopwatch.GetElapsedTime(runningSince).TotalSeconds >= 1) automaticallyShown = true;
        if (!pending && wasPending) finishedAt = Stopwatch.GetTimestamp();
        if (!pending && finishedAt != 0 && Stopwatch.GetElapsedTime(finishedAt).TotalSeconds >= 1.5) automaticallyShown = false;
        wasPending = pending;
        IsVisible = details.IsVisible || automaticallyShown;
    }

    private void TrackRunningTime(FileOperationJob job)
    {
        var now = Stopwatch.GetTimestamp();
        if (timedJob == job && timingRunning) runningTime += Stopwatch.GetElapsedTime(measuredAt, now);
        if (timedJob != job) runningTime = TimeSpan.Zero;
        timedJob = job; measuredAt = now; timingRunning = job.State == FileOperationState.Running;
    }

    private static string Estimate(TimeSpan time) => time.TotalDays >= 1 ? $"~{Math.Ceiling(time.TotalDays):N0} d left"
        : time.TotalHours >= 1 ? $"~{Math.Ceiling(time.TotalHours):N0} h left"
        : time.TotalMinutes >= 1 ? $"~{Math.Ceiling(time.TotalMinutes):N0} min left" : $"~{Math.Ceiling(time.TotalSeconds):N0} s left";

    private void ShowReport()
    {
        var job = Selected();
        cancelSelected.IsEnabled = job is { IsFinished: false, State: not FileOperationState.Cancelling };
        report.Show(job);
    }
}

// @implements RecentLocations, FavoriteLocations, ReaderThemes, RestoreWorkspace, KeepFeedback, TryLocalRelease, ApplicationLifecycle, WorkspaceStorage, ReleaseHandoff
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed class WorkspaceLifecycle : IDisposable
{
    private readonly CommanderWindow window;
    private readonly SessionStore store;
    private readonly ReleaseCatalog? releases;
    private readonly RestartProcess restart;
    private readonly DirectoryUpdates directoryUpdates;
    private readonly DirectoryAttention attention = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim saving = new(1);
    private bool checking;
    private bool closeRequested;
    private bool allowClose;
    private bool disposed;
    private string? failedRelease;
    private ViewerHandoffStore? restoredViewers;
    public Task PendingWork { get; private set; } = Task.CompletedTask;
    public string? UpdateError { get; private set; }
    public event Action? Initialized;

    public WorkspaceLifecycle(CommanderWindow window, SessionStore store, ReleaseCatalog? releases, RestartProcess? restart = null, IssueStore? issues = null)
    {
        this.window = window; this.store = store; this.releases = releases; this.restart = restart ?? new RestartProcess();
        directoryUpdates = new DirectoryUpdates(window);
        window.Feedback.ReleaseId = releases?.CurrentId ?? "development";
        window.RefreshUptime();
        window.Feedback.Store = issues ?? new IssueStore(Path.Combine(Path.GetDirectoryName(store.Path)!, "issues"));
        window.Feedback.ListPath = window.Feedback.Store.DatabasePath;
        window.FavoritesChanged += OnFavoritesChanged;
        window.ViewerThemeChanged += OnViewerThemeChanged;
        window.Feedback.AgentChanged += OnFeedbackAgentChanged;
        window.TrackPlacement();
        timer.Tick += OnTick;
    }

    public async Task InitializeAsync(string[] directories, bool requireSavedSession = false, string? handoffPath = null)
    {
        window.FileTypeSettingsPath = Path.Combine(Path.GetDirectoryName(store.Path)!, "file-types.json");
        await window.ReloadFileTypesAsync();
        WorkspaceState? state = null;
        try { state = await store.ReadAsync(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            if (requireSavedSession) throw;
            window.SetUpdateMessage("The saved session could not be restored. Starting a new session."); window.Trace.Add(error.ToString());
        }
        if (state == null && requireSavedSession) throw new InvalidDataException("The restart session is missing.");
        if (state != null && directories.Length == 0) await window.RestoreWorkspaceAsync(state);
        else
        {
            var left = directories.ElementAtOrDefault(0) ?? Environment.CurrentDirectory;
            await window.InitializeAsync(left, directories.ElementAtOrDefault(1) ?? left);
            if (state != null)
            {
                window.Favorites.Restore(state.Favorites ?? []);
                window.CommandLine.Restore(state.CommandLine);
                window.RestoreViewerTheme(state.ViewerThemeId);
                window.FileSearch.Restore(state.FileSearch);
                window.Feedback.RestoreAgent(state.FeedbackAgent);
            }
        }
        window.Closing += OnClosing;
        if (state?.Notes is { } legacy) await window.Feedback.Store!.ImportAsync(legacy, once: true);
        await window.Feedback.RefreshAsync();
        window.Closed += OnClosed;
        await SaveAsync();
        if (handoffPath != null && ViewerHandoffStore.ForRestart(handoffPath, store.Path) is { } snapshot)
            await window.PrepareViewersAsync(await snapshot.ReadAsync());
        if (requireSavedSession && window.UpdateMessage.Length == 0) window.ShowUpdatedMessage();
        if (!requireSavedSession) StartMonitoring();
        Initialized?.Invoke();
    }

    private void StartMonitoring()
    {
        timer.Start();
        directoryUpdates.Start();
    }

    internal void CompleteRestart(string handoffPath)
    {
        restoredViewers = ViewerHandoffStore.ForRestart(handoffPath, store.Path);
        window.ShowRestoredViewers();
        StartMonitoring();
    }

    public async Task SaveAsync(WorkspaceState? captured = null)
    {
        await saving.WaitAsync();
        try
        {
            var state = captured ?? window.CaptureWorkspace();
            await store.SaveAsync(state);
        }
        finally { saving.Release(); }
    }

    private Task OnFavoritesChanged() => PendingWork = SaveFavoritesAsync();
    private Task OnViewerThemeChanged() => PendingWork = SavePreferenceAsync("Could not save the viewer theme. Keep the application open and try again.");
    private Task OnFeedbackAgentChanged() => PendingWork = SavePreferenceAsync("Could not save the feedback agent. Keep the application open and try again.");

    private Task SaveFavoritesAsync() => SavePreferenceAsync("Could not save favorites. Keep the application open and try again.");


    private async Task SavePreferenceAsync(string message)
    {
        try { await SaveAsync(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { window.SetUpdateMessage(message); window.Trace.Add(error.ToString()); }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        PendingWork = PollAsync();
        await PendingWork;
    }

    public async Task PollAsync()
    {
        if (checking || disposed || allowClose) return;
        window.RefreshUptime();
        ObserveDirectoryAttention();
        checking = true;
        try
        {
            try { await window.Feedback.RefreshAsync(); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            { window.SetUpdateMessage("Could not read issues. Existing feedback has been kept."); window.Trace.Add(error.ToString()); }
            if (closeRequested)
            {
                if (!window.IsBusy && !window.Left.IsCommitting && !window.Right.IsCommitting) await SaveAndCloseAsync();
                return;
            }
            await CheckForUpdateAsync();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            UpdateError = error.Message;
            window.SetRestarting(false);
            window.SetUpdateMessage("Update failed. This version remains open.");
            window.Trace.Add(error.ToString());
        }
        finally { checking = false; }
    }

    private async Task CheckForUpdateAsync()
    {
        if (releases == null) return;
        var next = await releases.FindNextAsync();
        var canRestart = window.Readiness.CanRestart && saving.CurrentCount != 0;
        var pending = next != null && next.Id != failedRelease;
        window.SetUpdateReady(pending && !canRestart);
        if (pending && canRestart) await ReplaceProcessAsync(next!);
    }

    private async Task ReplaceProcessAsync(ReleaseInfo release)
    {
        var state = window.CaptureWorkspace();
        var viewers = window.CaptureViewers();
        window.SetRestarting(true);
        window.SetUpdateMessage("Updating and restoring your session…");
        var handoff = Path.Combine(Path.GetDirectoryName(store.Path)!, "handoff", Guid.NewGuid().ToString("N"));
        var snapshot = new ViewerHandoffStore(handoff);
        try
        {
            await SaveAsync(state);
            if (viewers.Length > 0) await snapshot.SaveAsync(viewers, lifetime.Token);
            await restart.PrepareAsync(release, store.Path, handoff, lifetime.Token);
        }
        catch
        {
            failedRelease = release.Id;
            snapshot.Delete();
            throw;
        }
        allowClose = true;
        window.Close();
    }

    private void ObserveDirectoryAttention()
    {
        var now = DateTimeOffset.UtcNow;
        var readiness = window.Readiness;
        var visit = attention.Observe(window.ActivePanel.Session.DirectoryPath, now,
            window.IsActive && !readiness.Busy && !readiness.DialogOpen && !readiness.FeedbackActive && readiness.IdleFor < TimeSpan.FromMinutes(1));
        if (visit != null) window.DirectoryUsage.Visit(visit, now);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        closeRequested = true;
        window.SetUpdateMessage("Saving session before closing…");
    }

    private async Task SaveAndCloseAsync()
    {
        try { await SaveAsync(); allowClose = true; window.Close(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { closeRequested = false; window.SetUpdateMessage("Could not save the session. The application remains open."); window.Trace.Add(error.ToString()); }
    }

    private void OnClosed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        directoryUpdates.Dispose();
        lifetime.Cancel();
        window.FavoritesChanged -= OnFavoritesChanged;
        window.ViewerThemeChanged -= OnViewerThemeChanged;
        window.Feedback.AgentChanged -= OnFeedbackAgentChanged;
        window.Closing -= OnClosing;
        window.Closed -= OnClosed;
        restoredViewers?.Delete();
    }
}

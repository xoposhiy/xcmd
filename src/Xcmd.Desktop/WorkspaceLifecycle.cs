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
    public Task PendingWork { get; private set; } = Task.CompletedTask;
    public string? UpdateError { get; private set; }
    public event Action? Initialized;

    public WorkspaceLifecycle(CommanderWindow window, SessionStore store, ReleaseCatalog? releases, RestartProcess? restart = null)
    {
        this.window = window; this.store = store; this.releases = releases; this.restart = restart ?? new RestartProcess();
        directoryUpdates = new DirectoryUpdates(window);
        window.Feedback.ReleaseId = releases?.CurrentId ?? "development";
        if (releases != null) window.Title = "Xcmd · " + releases.CurrentId;
        window.Feedback.ListPath = Path.Combine(Path.GetDirectoryName(store.Path)!, "refinements.md");
        window.Feedback.Changed += OnFeedbackChanged;
        window.FavoritesChanged += OnFavoritesChanged;
        window.ViewerThemeChanged += OnViewerThemeChanged;
        window.TrackPlacement();
        timer.Tick += OnTick;
    }

    public async Task InitializeAsync(string[] directories, bool requireSavedSession = false)
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
            if (state != null) { window.Feedback.Restore(state.Notes); window.Favorites.Restore(state.Favorites ?? []); window.CommandLine.Restore(state.CommandLine); window.RestoreViewerTheme(state.ViewerThemeId); window.FileSearch.Restore(state.FileSearch); }
        }
        window.Closing += OnClosing;
        if (releases != null)
        {
            var path = Path.Combine(releases.CurrentDirectory, "feedback-resolutions.json");
            window.Feedback.Restore(await FeedbackResolutions.ApplyAsync(window.Feedback.Notes, path, releases.CurrentId));
        }
        window.Closed += OnClosed;
        await SaveAsync();
        timer.Start();
        directoryUpdates.Start();
        Initialized?.Invoke();
    }

    public async Task SaveAsync()
    {
        await saving.WaitAsync();
        try
        {
            var state = window.CaptureWorkspace();
            await store.SaveAsync(state);
            await RefinementList.SaveAsync(window.Feedback.ListPath!, state.Notes);
        }
        finally { saving.Release(); }
    }

    private Task OnFeedbackChanged() => PendingWork = SaveFeedbackAsync();
    private Task OnFavoritesChanged() => PendingWork = SaveFavoritesAsync();
    private Task OnViewerThemeChanged() => PendingWork = SavePreferenceAsync("Could not save the viewer theme. Keep the application open and try again.");

    private Task SaveFavoritesAsync() => SavePreferenceAsync("Could not save favorites. Keep the application open and try again.");

    private Task SaveFeedbackAsync() => SavePreferenceAsync("Could not save comments. Keep the application open and try again.");

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
        ObserveDirectoryAttention();
        checking = true;
        try
        {
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
        if (next == null || next.Id == failedRelease) return;
        if (!window.Readiness.CanRestart || saving.CurrentCount == 0)
        {
            if (!window.Feedback.IsActive) window.SetUpdateMessage("Update ready. Waiting until the application is idle.");
            return;
        }
        await ReplaceProcessAsync(next);
    }

    private async Task ReplaceProcessAsync(ReleaseInfo release)
    {
        window.SetRestarting(true);
        window.SetUpdateMessage("Updating and restoring your session…");
        await SaveAsync();
        var handoff = Path.Combine(Path.GetDirectoryName(store.Path)!, "handoff", Guid.NewGuid().ToString("N"));
        try { await restart.PrepareAsync(release, store.Path, handoff, lifetime.Token); }
        catch { failedRelease = release.Id; throw; }
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
        window.Feedback.Changed -= OnFeedbackChanged;
        window.FavoritesChanged -= OnFavoritesChanged;
        window.ViewerThemeChanged -= OnViewerThemeChanged;
        window.Closing -= OnClosing;
        window.Closed -= OnClosed;
    }
}

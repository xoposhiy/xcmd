// @implements NavigatePanels, ViewRecentAgentSessions
using System.Diagnostics;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Panels;

internal sealed partial class AgentSessionView
{
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Stopwatch focusTime = new();
    private readonly Dictionary<SessionAgent, string> errors = [];
    private readonly HashSet<SessionAgent> historyLoaded = [];
    private CancellationTokenSource? loading;
    private RecentAgentSessions sessions = new();
    private IAgentSessions? source;
    private SessionAgent[] agents = [];
    private bool focused;
    private bool refreshQueued;

    internal void Cancel()
    {
        source = null;
        loading?.Cancel();
        loading = null;
        refreshQueued = false;
        focused = false;
        poll.Stop();
        updatedTimeTimer.Stop();
    }

    internal Task LoadAsync(IAgentSessions reader, SessionAgent? filter, bool active)
    {
        Cancel();
        source = reader;
        agents = filter.HasValue ? [filter.Value] : [SessionAgent.ClaudeCode, SessionAgent.Codex];
        Heading.Text = filter.HasValue ? AgentSession.AgentName(filter.Value) + " sessions" : "All Agent Sessions";
        sessions = new();
        Table.ItemsSource = sessions.Rows;
        errors.Clear();
        historyLoaded.Clear();
        userSelected = false;
        status.Text = "Loading sessions…";
        updatedTimeTimer.Start();
        WatchFocus(active);
        return ReadAsync(SessionListScope.History);
    }

    internal void SetRefreshFocus(bool active, bool windowReturned = false)
    {
        if (source == null) return;
        if (windowReturned) UpdateTimes?.Invoke();
        var gained = active && !focused;
        if (active != focused) WatchFocus(active);
        if (!active && !windowReturned) refreshQueued = false;
        if (!gained && !windowReturned) return;
        if (loading == null) _ = ReadAsync(SessionListScope.Recent);
        else refreshQueued = true;
    }

    private void WatchFocus(bool active)
    {
        focused = active;
        if (active) { focusTime.Restart(); poll.Start(); }
        else poll.Stop();
    }

    private async void PollRecent(object? sender, EventArgs e)
    {
        if (focusTime.Elapsed >= TimeSpan.FromMinutes(5)) { poll.Stop(); return; }
        if (focused && loading == null) await ReadAsync(SessionListScope.Recent);
    }

    private async Task ReadAsync(SessionListScope scope)
    {
        if (source is not { } reader) return;
        using var request = new CancellationTokenSource();
        loading = request;
        var pending = agents.Length;
        try { await Task.WhenAll(agents.Select(ReadAgentAsync)); }
        finally { if (loading == request) loading = null; }
        if (request.IsCancellationRequested || !refreshQueued) return;
        refreshQueued = false;
        await ReadAsync(SessionListScope.Recent);

        async Task ReadAgentAsync(SessionAgent agent)
        {
            try
            {
                bool needHistory = !historyLoaded.Contains(agent);
                if (scope == SessionListScope.Recent && !needHistory)
                {
                    var recent = new List<AgentSession>();
                    await reader.ListAsync(agent, scope, rows => { recent.AddRange(rows); return Task.CompletedTask; }, request.Token);
                    if (request.IsCancellationRequested) return;
                    needHistory = sessions.HasGapBefore(agent, recent);
                    UpdateRows(() =>
                    {
                        sessions.AddPage(recent);
                        sessions.Reconcile(agent, recent, SessionListScope.Recent);
                    }, preserveSelection: true);
                }
                if (needHistory)
                {
                    historyLoaded.Remove(agent);
                    var seen = new List<AgentSession>();
                    await reader.ListAsync(agent, SessionListScope.History, async rows =>
                    {
                        seen.AddRange(rows);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (request.IsCancellationRequested) return;
                            UpdateRows(() => sessions.AddPage(rows), preserveSelection: scope == SessionListScope.Recent);
                            UpdateStatus();
                        });
                    }, request.Token);
                    if (request.IsCancellationRequested) return;
                    UpdateRows(() => sessions.Reconcile(agent, seen, SessionListScope.History), preserveSelection: scope == SessionListScope.Recent);
                    historyLoaded.Add(agent);
                }
                errors.Remove(agent);
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { return; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (request.IsCancellationRequested) return;
                errors[agent] = AgentSession.AgentName(agent) + ": " + error.Message;
            }
            pending--;
            UpdateStatus();
        }

        void UpdateStatus()
        {
            var count = sessions.Rows.Count == 0 && pending == 0 && errors.Count == 0 ? "No sessions found" : $"{sessions.Rows.Count:N0} sessions";
            var text = scope == SessionListScope.History && pending > 0 ? count + " · Loading…" : count;
            status.Text = string.Join(" · ", errors.Values.Prepend(text));
        }
    }
}

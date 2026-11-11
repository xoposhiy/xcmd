using Avalonia.Threading;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed class DirectoryUpdates : IDisposable
{
    private readonly CommanderWindow window;
    private readonly Dictionary<PanelView, (DirectoryMonitor Monitor, long Applied)> monitors = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool checking;
    private bool disposed;
    public Task PendingWork { get; private set; } = Task.CompletedTask;

    public DirectoryUpdates(CommanderWindow window)
    {
        this.window = window;
        timer.Tick += async (_, _) => { PendingWork = PollAsync(); await PendingWork; };
    }

    public void Start() => timer.Start();
    private bool CanApply => !disposed && window.IsVisible && window.IsEnabled && !window.IsMenuOpen && !window.HasOpenDialog && !window.IsNameSearchActive &&
        !window.Feedback.IsActive && window.Left.EditingRow == null && window.Right.EditingRow == null;

    public async Task PollAsync()
    {
        if (checking || disposed) return;
        checking = true;
        try
        {
            foreach (var panel in new[] { window.Left, window.Right })
            {
                var path = panel.Session.DirectoryPath;
                if (ArchivePath.IsEntry(path))
                {
                    if (monitors.Remove(panel, out var old)) old.Monitor.Dispose();
                    continue;
                }
                if (path.Length == 0) continue;
                if (!monitors.TryGetValue(panel, out var state) || state.Monitor.Path != path)
                {
                    state.Monitor?.Dispose();
                    state = (new DirectoryMonitor(path), -1);
                    monitors[panel] = state;
                }
                await state.Monitor.ProbeAsync();
                if (disposed) return;
                if (window.IsBusy || !CanApply || !state.Monitor.IsQuiet || state.Applied == state.Monitor.Revision) continue;
                var revision = state.Monitor.Revision;
                var applied = false;
                await window.RunActionAsync(async () => { applied = await panel.RefreshAsync(() => CanApply, () => window.ActivePanel == panel); });
                if (applied) monitors[panel] = (state.Monitor, revision);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { window.Trace.Add("Directory monitoring: " + error); window.SetUpdateMessage("A directory could not be updated. Retrying automatically."); }
        finally { checking = false; }
    }

    public void Dispose()
    {
        disposed = true; timer.Stop();
        foreach (var state in monitors.Values) state.Monitor.Dispose();
    }
}

// @implements ReportControl, KeepFeedback, FeedbackIdentity
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Threading.Channels;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Feedback;

public sealed partial class FeedbackController
{
    private PointTakenAdapter? adapter;
    private KeyGesture shortcut = new(Key.F12, KeyModifiers.Control);
    private Window? origin;
    private Control? previousFocus;
    private Channel<object?>? selection;
    private bool repeatTargets;
    private bool hasTarget;
    public bool Connected => adapter != null;

    private void ConnectTracker()
    {
        try { shortcut = KeyGesture.Parse(PointTakenAdapter.Shortcut()); }
        catch (Exception error) { actions.Trace("Could not read the Point Taken shortcut: " + error.Message); }
        adapter = new PointTakenAdapter(
            async (hwnd, repeat, point, useControl) => await Dispatcher.UIThread.InvokeAsync(() => StartSelection(hwnd, repeat, point, useControl), DispatcherPriority.Background),
            async (hwnd, x, y) => await Dispatcher.UIThread.InvokeAsync(() => PreviewTarget(hwnd, x, y)),
            async () => await Dispatcher.UIThread.InvokeAsync(FinishSelection),
            () => ReleaseId, actions.Trace);
        var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
        {
            if (!surfaces.ContainsKey(window)) AttachTrackerWindow(window, null, null);
            if (origin != null && window != origin && !hasTarget) surfaces[origin].Cancel();
        }, RoutingStrategies.Bubble | RoutingStrategies.Direct, handledEventsToo: true);
        owner.Closed += (_, _) => { opened.Dispose(); adapter.Dispose(); };
    }

    private void AttachTrackerWindow(Window window, Action<string>? hint, Action? fallback)
    {
        if (surfaces.ContainsKey(window)) return;
        surfaces[window] = new FeedbackSurface(this, window, window.Content as Control ?? window, hint ?? actions.SetHint,
            previous => { if (window.IsActive && previous?.Focus() != true) fallback?.Invoke(); });
        window.Closed += (_, _) => surfaces.Remove(window);
    }
    internal bool ShortcutMatches(KeyEventArgs e) => e.Key == shortcut.Key && e.KeyModifiers == shortcut.KeyModifiers;
    internal void RequestPick(Window window)
    {
        if (IsActive || window.TryGetPlatformHandle() is not { } handle) return;
        actions.Execute(async () =>
        {
            try { await PointTakenAdapter.ActivateAsync(handle.Handle); }
            catch (Exception error) { actions.SetHint("Start Point Taken to report an issue."); actions.Trace(error.Message); }
        });
    }
    private (int X, int Y, int Width, int Height, string SourceId)? PreviewTarget(long hwnd, int x, int y)
    {
        var source = surfaces.Keys.FirstOrDefault(w => w.IsVisible && w.TryGetPlatformHandle()?.Handle == (nint)hwnd);
        if (source == null || surfaces[source].PreviewAt(new(x, y)) is not { } target) return null;
        var rect = target.Bounds;
        return (rect.X, rect.Y, rect.Width, rect.Height, target.SourceId);
    }

    private ChannelReader<object?>? StartSelection(long hwnd, bool repeat, (int X, int Y)? point, bool useControl)
    {
        if (IsActive) return null;
        var source = surfaces.Keys.FirstOrDefault(window => window.IsVisible && window.IsActive && window.IsEnabled &&
            window.TryGetPlatformHandle()?.Handle == (nint)hwnd);
        if (source == null) return null;
        origin = source;
        previousFocus = source.FocusManager?.GetFocusedElement() as Control;
        selection = Channel.CreateUnbounded<object?>();
        repeatTargets = repeat;
        hasTarget = false;
        surfaces[source].Begin();
        if (point is { } location)
        {
            if (useControl) surfaces[source].UseAtScreenPoint(new(location.X, location.Y));
            else surfaces[source].SelectAtScreenPoint(new(location.X, location.Y));
        }
        return selection.Reader;
    }
    internal void SelectTarget(Window window, Control control, IReadOnlyList<FeedbackInteraction> actionsTaken)
    {
        if (origin != window || selection == null) return;
        try
        {
            var target = DescribeInteraction(control, "Comment");
            var snapshot = new
            {
                projectId = "xcmd",
                build = ReleaseId,
                window = new { kind = window.GetType().Name, name = window == owner ? null : window.Title },
                target = new
                {
                    sourceId = target.ElementId,
                    name = repeatTargets && target.ElementName == target.ElementId ? null : target.ElementName,
                    kind = control.GetType().Name,
                    scope = FeedbackTarget.Scope(control),
                    enabled = control.IsEffectivelyEnabled ? (bool?)null : false,
                    instance = (control.DataContext as IFeedbackRow)?.Name
                },
                actionPath = actionsTaken.Count == 0 ? null : actionsTaken.Select(step => new
                { sourceId = step.ElementId, name = step.ElementName == step.ElementId ? null : step.ElementName }).ToArray(),
                context = FeedbackTarget.Context(control)
            };
            adapter?.AllowTrackerFocus();
            hasTarget = true;
            selection.Writer.TryWrite(snapshot);
            surfaces[window].ClosePopups();
            if (repeatTargets) surfaces[window].Begin();
        }
        catch (Exception error) { selection.Writer.TryComplete(error); }
    }
    internal void EndSelection(Window window)
    {
        if (origin != window || selection == null) return;
        if (!window.IsVisible) { selection.Writer.TryComplete(); return; }
        if (hasTarget) adapter?.AllowTrackerFocus();
        selection.Writer.TryWrite(null);
        if (hasTarget && repeatTargets) surfaces[window].Begin();
    }
    private void FinishSelection()
    {
        var source = origin;
        origin = null;
        selection?.Writer.TryComplete();
        selection = null;
        if (source != null && surfaces.TryGetValue(source, out var surface))
        {
            surface.Cancel();
            if (source.IsActive) surface.RestoreFocus(previousFocus);
        }
        previousFocus = null;
        actions.RecordActivity();
    }
}

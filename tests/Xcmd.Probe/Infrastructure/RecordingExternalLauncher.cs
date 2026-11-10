using Xcmd.Core;

namespace Xcmd.Probe.Infrastructure;

public sealed class RecordingExternalLauncher : IExternalLauncher
{
    public List<ExternalLaunch> Requests { get; } = [];
    public Exception? Failure { get; set; }
    public TaskCompletionSource? Pause { get; set; }
    public IExternalLauncher? ForwardTo { get; set; }
    public async Task LaunchAsync(ExternalLaunch request)
    {
        Requests.Add(request);
        if (Pause != null) await Pause.Task;
        if (Failure != null) throw Failure;
        if (ForwardTo != null) await ForwardTo.LaunchAsync(request);
    }
}

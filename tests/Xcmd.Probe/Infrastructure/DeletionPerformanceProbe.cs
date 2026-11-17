// @implements HeadlessVerification
// @tests DeleteEntries, QueueOperations
using System.Diagnostics;
using Avalonia.Headless;
using Xcmd.Core;

namespace Xcmd.Probe.Infrastructure;

public static class DeletionPerformanceProbe
{
    public static string CreateTree(ProbeContext context, int files = 1200)
    {
        var root = Path.Combine(context.LeftPath, "delete-tree");
        for (var index = 0; index < files; index++)
        {
            var folder = Path.Combine(root, "level-one", "level-two", "group-" + index / 40);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "file-" + index + ".txt"), "Deletion performance fixture");
        }
        return root;
    }

    public static async Task MeasureAsync(ProbeContext context, FileEntry entry)
    {
        var notifications = 0;
        context.Window.Operations.Changed += CountNotification;
        void CountNotification() => notifications++;
        var watch = Stopwatch.StartNew();
        var previousFrame = watch.Elapsed;
        var longestFrame = TimeSpan.Zero;
        try
        {
            var job = context.Window.Operations.Enqueue(new(FileOperationKind.Delete, [entry]));
            context.Window.OperationsView.ToggleDetails();
            while (!job.IsFinished)
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Deletion did not finish.");
                if (context.Window.OperationDialog != null) throw new InvalidOperationException("Deletion requested an unexpected error decision.");
                await Task.Delay(5);
                context.Window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var frame = watch.Elapsed;
                longestFrame = TimeSpan.FromMilliseconds(Math.Max(longestFrame.TotalMilliseconds, (frame - previousFrame).TotalMilliseconds));
                previousFrame = frame;
            }
            watch.Stop();
            context.Measurements["deletionElapsedMs"] = watch.Elapsed.TotalMilliseconds;
            context.Measurements["deletionFiles"] = job.CompletedFiles;
            context.Measurements["deletionNotifications"] = notifications;
            context.Measurements["deletionState"] = job.State.ToString();
            context.Measurements["deletionLongestFrameMs"] = longestFrame.TotalMilliseconds;
            ProbeContext.Equal(FileOperationState.Completed, job.State, "The complete tree is removed");
            ProbeContext.Check(longestFrame < TimeSpan.FromSeconds(1), "Deletion keeps the UI responsive");
        }
        finally { context.Window.Operations.Changed -= CountNotification; }
    }
}

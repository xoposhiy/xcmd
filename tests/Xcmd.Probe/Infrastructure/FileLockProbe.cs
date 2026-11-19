// @tests RecoverOperations
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.FileOperations;

namespace Xcmd.Probe.Infrastructure;

public static class FileLockProbe
{
    public static async Task MeasureReleaseAsync(ProbeContext context, LockingProcessFixture holder, Func<bool> completed)
    {
        var dialog = (FileErrorDialog)context.Window.OperationDialog!;
        var elapsed = Stopwatch.StartNew();
        double? retryMilliseconds = null;
        dialog.Decided += action => { if (action == FileErrorAction.Retry) retryMilliseconds = elapsed.Elapsed.TotalMilliseconds; };
        await holder.ReleaseAsync();
        await FileOperationProbe.WaitUntilAsync(completed);
        context.Measurements["releaseToRetryMs"] = retryMilliseconds ?? -1;
        context.Measurements["releaseToVisibleResultMs"] = elapsed.Elapsed.TotalMilliseconds;
        ProbeContext.Check(retryMilliseconds is >= 0 and < 333, "Automatic retry starts within one third of a second after requesting process exit");
        ProbeContext.Check(elapsed.Elapsed.TotalMilliseconds < 333, "The changed item appears within one third of a second after requesting process exit");
        await context.SettleAsync();
    }

    public static void CheckCompact(FileErrorDialog dialog, int groups)
    {
        var controls = dialog.GetVisualDescendants().ToArray();
        var processes = controls.OfType<TextBlock>().Where(text => text.Name == "FileUserProcesses").ToArray();
        ProbeContext.Equal(groups, processes.Length, "Each application has one plain process summary");
        ProbeContext.Check(processes.All(text => text.Text!.Contains("(PID ", StringComparison.Ordinal)), "Process summaries include executable names and parenthesized PIDs");
        ProbeContext.Check(!controls.OfType<Button>().Any(button => button.Name == "FileUserPid"), "PIDs do not open another window");
        ProbeContext.Check(controls.OfType<Button>().Where(button => button.Name == "FileUserEnd").All(button => Equals(button.Content, "Kill")), "The action is called Kill for both one and multiple processes");
        ProbeContext.Check(!controls.OfType<TextBlock>().Any(text => text.Text?.Contains("retries automatically", StringComparison.Ordinal) == true), "The automatic retry explanation does not occupy dialog space");
    }
}

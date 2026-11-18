// @tests CreateArchive, SharedControls
using System.IO.Compression;
using Avalonia.Input;
using Xcmd.Desktop;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public static class ZipPackingProbe
{
    public static async Task<ZipNameDialog> OpenAsync(ProbeContext context, string name, bool keyboard = false)
    {
        context.Shell.NextAction = ShellMenuAction.AddToZip;
        if (keyboard) await context.KeyAsync(Key.F10, RawInputModifiers.Shift);
        else await ShellInputProbe.RightClickAsync(context, name);
        return context.Window.OperationDialog as ZipNameDialog ?? throw new InvalidOperationException("The ZIP command opens its name dialog.");
    }

    public static Dictionary<string, string> Read(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    public static async Task CaptureDialogAsync(ProbeContext context, ZipNameDialog dialog)
    {
        foreach (var scale in new[] { 1d, 1.5d })
        {
            dialog.SetScaleForTest(scale); await context.SettleAsync();
            DesignSystemProbe.CheckActions(dialog);
            await context.ScreenshotAsync("create-zip-" + (scale == 1 ? "100" : "150"), dialog);
        }
        dialog.SetScaleForTest(1);
    }

    public sealed class CancelAfterProgress(CancellationTokenSource cancellation) : IProgress<long>
    {
        public void Report(long bytes) { if (bytes > 0) cancellation.Cancel(); }
    }
}

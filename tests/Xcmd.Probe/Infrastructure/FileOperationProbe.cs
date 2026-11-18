// @tests TransferEntries, DeleteEntries, ResolveConflicts, RecoverOperations, WindowsFileActions
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Infrastructure;

public static class FileOperationProbe
{
    public static FileOperationJob LastJob(ProbeContext c) => c.Window.Operations.Jobs[^1];

    public static async Task CopyAsync(ProbeContext c)
    {
        await c.KeyAsync(Key.F5); ConfirmTransfer(c); await c.SettleAsync();
    }

    public static async Task MoveAsync(ProbeContext c)
    {
        await c.KeyAsync(Key.F6); ConfirmTransfer(c); await c.SettleAsync();
    }

    public static void ConfirmTransfer(ProbeContext c)
    {
        var dialog = c.Window.OperationDialog as FileTransferDialog ?? throw new InvalidOperationException("Transfer confirmation is open.");
        c.PressKey(Key.Enter, target: dialog);
    }

    public static async Task WaitUntilAsync(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("File operation state did not become ready.");
            await Task.Delay(5);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    public static async Task PrepareSeparatedMarksAsync(ProbeContext context, string cursor, string other)
    {
        var panel = context.Window.Left;
        var state = panel.CaptureState() with { Cursor = cursor, Selected = [cursor, other], ScrollY = 520 };
        await panel.RestoreStateAsync(state);
        panel.FocusCurrent(); panel.RestoreScroll(state);
        await DrainRenderingAsync(context);
    }

    public static async Task DrainRenderingAsync(ProbeContext context)
    {
        for (var frame = 0; frame < 4; frame++)
        {
            await Task.Delay(5);
            context.Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    public static async Task CaptureProgressAsync(ProbeContext context, string name)
    {
        await DrainRenderingAsync(context);
        using var frame = context.Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The progress view did not render.");
        frame.Save(Path.Combine(context.OutputPath, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    public static async Task ChooseConflictAsync(ProbeContext c, string button)
    {
        await WaitUntilAsync(() => c.Window.OperationDialog is FileConflictDialog);
        var dialog = (FileConflictDialog)c.Window.OperationDialog!;
        await c.ClickAsync(dialog.GetVisualDescendants().OfType<Button>().Single(control => control.Name == button), target: dialog);
    }

    public static async Task SkipFileErrorAsync(ProbeContext c)
    {
        await WaitUntilAsync(() => c.Window.OperationDialog is FileErrorDialog);
        var dialog = c.Window.OperationDialog!;
        await c.ClickAsync(dialog.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "FileErrorSkip"), target: dialog);
        await WaitUntilAsync(() => LastJob(c).State >= FileOperationState.Completed);
    }

    public static Task ConfirmDeleteAsync(ProbeContext c)
    {
        var dialog = c.Window.OperationDialog as FileDeleteDialog ?? throw new InvalidOperationException("Delete confirmation is open.");
        return c.ClickAsync(dialog.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "ConfirmFileDelete"), target: dialog);
    }

    public static void VerifyAndRestoreRecycleFixture(string original, string contents)
        => VerifyAndRestore(original, payload => Equal(contents, File.ReadAllText(payload), "The real Recycle Bin retains the selected fixture contents"), false);

    public static void VerifyAndRestoreRecycleFolder(string original, string child, string contents)
        => VerifyAndRestore(original, payload => Equal(contents, File.ReadAllText(Path.Combine(payload, child)), "The real Recycle Bin retains the selected tree contents"), true);

    private static void VerifyAndRestore(string original, Action<string> verify, bool directoryItem)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Recycle Bin fixtures require Windows.");
        using var identity = WindowsIdentity.GetCurrent();
        var directory = Path.Combine(Path.GetPathRoot(original)!, "$Recycle.Bin", identity.User!.Value);
        foreach (var metadata in Directory.EnumerateFiles(directory, "$I*"))
        {
            var data = File.ReadAllBytes(metadata);
            if (data.Length < 28) continue;
            var version = BitConverter.ToInt64(data, 0);
            var start = version == 2 ? 28 : 24;
            var path = Encoding.Unicode.GetString(data, start, data.Length - start).TrimEnd('\0');
            if (!path.Equals(original, StringComparison.OrdinalIgnoreCase)) continue;
            var payload = Path.Combine(directory, "$R" + Path.GetFileName(metadata)[2..]);
            verify(payload);
            if (directoryItem) Directory.Move(payload, original);
            else File.Move(payload, original);
            File.Delete(metadata);
            return;
        }
        throw new InvalidOperationException("The selected fixture was not found in the real Recycle Bin.");
    }
}

public sealed class CancelCopyProgress(CancellationTokenSource cancellation) : IProgress<long>
{
    public void Report(long value) { if (value > 0) cancellation.Cancel(); }
}

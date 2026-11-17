using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class OperationRefinementScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("operations-review-shortcuts-and-action-verbs", "UC04; UC05; Concise action prompts", async c =>
        {
            var help = c.Window.GetVisualDescendants().OfType<WrapPanel>().Single(panel => panel.Name == "KeyboardHelp");
            var hints = help.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? "").ToArray();
            Check(!hints.Any(text => new[] { "Tab ", "Enter ", "Esc ", "↑ ↓" }.Any(value => text.StartsWith(value, StringComparison.Ordinal))), "Obvious navigation hints are removed.");
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F5);
            var dialog = (FileTransferDialog)c.Window.OperationDialog!;
            Check(dialog.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Copy alpha.txt to:"), "The copy action appears in the dialog body.");
            await c.ScreenshotAsync("copy-action", dialog); await c.KeyAsync(Key.Escape, target: dialog);
            await c.KeyAsync(Key.F6); dialog = (FileTransferDialog)c.Window.OperationDialog!;
            Check(dialog.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Move alpha.txt to:"), "The move action appears in the dialog body.");
            await c.KeyAsync(Key.Escape, target: dialog);
        }),
        new("operations-review-identical-files", "UC34; Skip only matching contents and metadata", async c =>
        {
            var source = Path.Combine(c.LeftPath, "alpha.txt"); var target = Path.Combine(c.RightPath, "alpha.txt");
            await c.SelectAsync("alpha.txt"); await CopyAsync(c); await CopyAsync(c);
            Check(c.Window.OperationDialog == null, "Identical files do not require a conflict decision.");
            Equal(1L, LastJob(c).SkippedFiles, "The identical file is reported as skipped");
            Equal(0L, LastJob(c).BytesTransferred, "An identical file is not copied again");
            File.WriteAllText(target, "xyz"); File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
            await CopyAsync(c);
            Check(c.Window.OperationDialog is FileConflictDialog, "Equal sizes and timestamps do not hide different contents.");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            File.WriteAllText(target, "abc"); File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source).AddSeconds(5));
            await CopyAsync(c);
            Check(c.Window.OperationDialog is FileConflictDialog, "Different modification times still require a decision.");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
            File.SetAttributes(target, File.GetAttributes(source) | FileAttributes.Hidden);
            await CopyAsync(c);
            Check(c.Window.OperationDialog is FileConflictDialog, "Different file attributes still require a decision.");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            File.SetAttributes(target, FileAttributes.Normal);
        }),
        new("operations-review-selective-keyboard-copy", "UC20; Mark departing rows and copy only the marks", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            Equal("file2.log", c.Window.Left.Current?.Name, "Shift+Down moves the cursor");
            Check(c.Window.Left.SelectedNames().SequenceEqual(new[] { "alpha.txt" }), "Only the departed row is marked.");
            await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            Check(c.Window.Left.SelectedNames().Order().SequenceEqual(new[] { "alpha.txt", "file10.bin" }.Order()), "Plain Down skips a file without clearing earlier marks.");
            Equal("zeta.md", c.Window.Left.Current?.Name, "The cursor remains separate from marked files");
            await c.ScreenshotAsync("keyboard-marks"); await CopyAsync(c);
            Check(File.Exists(Path.Combine(c.RightPath, "alpha.txt")) && File.Exists(Path.Combine(c.RightPath, "file10.bin")), "Marked files are copied.");
            Check(!File.Exists(Path.Combine(c.RightPath, "file2.log")) && !File.Exists(Path.Combine(c.RightPath, "zeta.md")), "Unmarked files and the cursor row are not copied.");
            await c.KeyAsync(Key.Up, RawInputModifiers.Shift);
            Equal("file10.bin", c.Window.Left.Current?.Name, "Shift+Up moves the cursor upward");
            Check(c.Window.Left.SelectedNames().Contains("zeta.md"), "Shift+Up marks the departed lower row.");
        }),
        new("operations-review-preserve-source-scroll", "UC04; Copy completion keeps the source viewport", async c =>
        {
            for (var index = 0; index < 220; index++) c.AddFile($"item-{index:D3}.txt", "viewport fixture");
            await c.Window.Left.LoadAsync(c.LeftPath);
            await PrepareSeparatedMarksAsync(c, "item-020.txt", "item-180.txt");
            Equal("item-020.txt", c.Window.Left.Current?.Name, "The cursor returns above the last marked file");
            var before = c.Window.Left.CaptureState();
            Check(before.ScrollY > 0 && before.Selected.Length == 2, "The fixture has a scrolled panel with separated marks.");
            await c.KeyAsync(Key.F5);
            c.Measurements["scrollAfterOpen"] = c.Window.Left.CaptureState().ScrollY;
            ConfirmTransfer(c); await c.SettleAsync(); await DrainRenderingAsync(c);
            var after = c.Window.Left.CaptureState();
            c.Measurements["scrollBefore"] = before.ScrollY; c.Measurements["scrollAfter"] = after.ScrollY;
            Equal(before.Cursor, after.Cursor, "Copy completion keeps the source cursor");
            Check(Math.Abs(before.ScrollY - after.ScrollY) < 1, "Copy completion and later layout passes keep the source scroll offset.");
            Check(before.Selected.Order().SequenceEqual(after.Selected.Order()), "Copy completion keeps the marks.");
        }),
        new("operations-review-count-before-copy-and-cancel", "UC33; Count files before changes; cancel counting", async c =>
        {
            var folder = Path.Combine(c.LeftPath, "folder1"); var nested = Path.Combine(folder, "nested"); Directory.CreateDirectory(nested);
            File.WriteAllBytes(Path.Combine(nested, "one.bin"), new byte[3]); File.WriteAllBytes(Path.Combine(nested, "two.bin"), new byte[7]);
            var gate = c.FileSystem.PauseNextRead(folder);
            await c.SelectAsync("folder1"); c.PressKey(Key.F5); ConfirmTransfer(c);
            await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            c.Window.OperationsView.ToggleDetails(); await DrainRenderingAsync(c);
            Equal(FileOperationState.Counting, LastJob(c).State, "Directory counting has an observable state");
            Check(c.Window.OperationsView.Progress.IsIndeterminate && !Directory.Exists(Path.Combine(c.RightPath, "folder1")), "Counting shows progress before creating destination files.");
            c.PressKey(Key.Escape); c.PressKey(Key.Tab);
            Equal("right", c.Window.ActivePanel.Id, "Escape returns from operation history so Tab can switch panels during counting");
            gate.Release.TrySetResult(); await c.SettleAsync();
            Equal(3L, LastJob(c).TotalFiles, "The total counts files rather than selected folders or result records");
            Equal(3L, LastJob(c).ProcessedFiles, "Each file is processed once");
            Equal(3L, LastJob(c).CompletedFiles, "Folder results do not inflate the completed-file count");
            var cancelled = c.FileSystem.PauseNextRead(folder);
            c.Window.Operations.Enqueue(new(FileOperationKind.Copy, [c.Window.Left.Rows.Single(row => row.Name == "folder1").Entry], c.RightPath, "cancelled-tree"));
            await WaitUntilAsync(() => cancelled.Entered.Task.IsCompleted);
            c.Window.Operations.Cancel(LastJob(c)); await c.SettleAsync();
            Check(cancelled.WasCancelled && !Directory.Exists(Path.Combine(c.RightPath, "cancelled-tree")), "Cancelling the count changes no destination files.");
        }),
        new("operations-review-progress-and-time-estimate", "UC33; Show file totals, byte progress and estimated time", async c =>
        {
            var sources = new[] { "alpha.txt", "file2.log", "file10.bin" }.Select(name => c.Window.Left.Rows.Single(row => row.Name == name).Entry).ToArray();
            var gate = c.FileSystem.PauseNextCopy(sources[1].FullPath);
            c.Window.Operations.Enqueue(new(FileOperationKind.Copy, sources, c.RightPath));
            await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            c.Window.OperationsView.ToggleDetails(); await DrainRenderingAsync(c);
            var job = LastJob(c);
            Equal(3L, job.TotalFiles, "The file total is known before the second copy");
            Equal(1L, job.ProcessedFiles, "Only the first file has completed");
            Check(Math.Abs(c.Window.OperationsView.Progress.Value - 100d * 3 / 33) < 0.01, "The progress bar accounts for file bytes.");
            Check(Math.Abs((FileOperationProgress.Remaining(job, TimeSpan.FromSeconds(2))!.Value - TimeSpan.FromSeconds(20)).TotalMilliseconds) < 1, "The time estimate uses completed work and elapsed running time.");
            await WaitUntilAsync(() => c.Window.OperationsView.ProgressSummary.Contains('~'));
            await CaptureProgressAsync(c, "file-progress");
            gate.Release.TrySetResult(); await c.SettleAsync();
            Equal(3L, job.ProcessedFiles, "All files finish");
            Equal(1d, FileOperationProgress.Fraction(job), "Completed work reaches the full progress value");
        }),
        new("operations-review-delete-keyboard-options", "UC35; Discover deletion shortcuts and the Enter action", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Delete);
            var dialog = (FileDeleteDialog)c.Window.OperationDialog!;
            Check(dialog.Recycle.Content is AccessText { AccessKey: "R", ShowAccessKey: true } && dialog.Permanently.Content is AccessText { AccessKey: "P", ShowAccessKey: true }, "Both deletion options show standard underlined access keys.");
            var confirm = dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ConfirmFileDelete");
            Check(confirm.IsDefault && confirm.Content is AccessText { Text: "_Yes", AccessKey: "Y", ShowAccessKey: true }, "The main button confirms and shows its standard underlined access key.");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, dialog); Equal(FileOperationKind.Delete, dialog.SelectedKind, "Alt+P selects permanent deletion");
            await c.KeyAsync(Key.R, RawInputModifiers.Alt, dialog); Equal(FileOperationKind.Recycle, dialog.SelectedKind, "Alt+R selects the Recycle Bin");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, dialog); await c.ScreenshotAsync("delete-shortcuts", dialog);
            await c.KeyAsync(Key.Enter, target: dialog);
            Check(!File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Enter confirms the selected mode while a radio option has focus.");
        })
    ];
}

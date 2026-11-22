// @tests MarkEntries, TransferEntries, DeleteEntries, ResolveConflicts, QueueOperations
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.FileOperations;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.DialogProbe;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.OperationInputProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class OperationUsabilityScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("review-enter-on-cancel-keeps-files", "UC04; UC05; UC35; Enter activates the focused Cancel action", async c =>
        {
            await c.SelectAsync("alpha.txt");
            foreach (var key in new[] { Key.F5, Key.F6 })
            {
                await c.KeyAsync(key); var dialog = c.Window.OperationDialog!;
                var cancel = dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CancelFileTransfer");
                await FocusByTabAsync(c, dialog, cancel); Primary(dialog, "CancelFileTransfer");
                await c.KeyAsync(Key.Enter, target: dialog);
                Check(c.Window.OperationDialog == null, "Enter on Cancel closes the transfer confirmation.");
            }
            foreach (var modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
            {
                await c.KeyAsync(Key.Delete, modifiers); var dialog = c.Window.OperationDialog!;
                var cancel = dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CancelFileDelete");
                await FocusByTabAsync(c, dialog, cancel); Primary(dialog, "CancelFileDelete");
                await c.KeyAsync(Key.Enter, target: dialog);
                Check(c.Window.OperationDialog == null, "Enter on Cancel closes the deletion confirmation.");
            }
            Equal(0, c.Window.Operations.Jobs.Count, "Cancel never submits an operation");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha.txt")), "Cancelled operations preserve the source");
            Check(!File.Exists(Path.Combine(c.RightPath, "alpha.txt")), "Cancelled transfers create no destination.");
        }),
        new("review-queue-keyboard-selection-and-cancel", "UC33; Cancel selected queued work while Tab keeps switching panels", async c =>
        {
            Check(!OperationHistoryCommand(c).IsVisible, "View omits operation history until the first job");
            await c.SelectAsync("alpha.txt"); var gate = c.FileSystem.PauseNextCopy(Path.Combine(c.LeftPath, "alpha.txt"));
            c.PressKey(Key.F5); ConfirmTransfer(c); await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            try
            {
                var queued = c.Window.Operations.Enqueue(new(FileOperationKind.Copy, [c.Window.Left.Rows.Single(row => row.Name == "file2.log").Entry], c.RightPath));
                Check(OperationHistoryCommand(c).IsVisible, "View offers operation history once a job exists");
                await OpenOperationsMenuAsync(c);
                Check(c.Window.OperationsView.DetailsVisible && c.Window.OperationsView.IsKeyboardFocusWithin, "Opening operation history focuses the queue.");
                c.PressKey(Key.Tab); await DrainRenderingAsync(c);
                Check(c.Window.Right.Table.IsKeyboardFocusWithin, "Tab from operation history switches to the other file panel");
                c.PressKey(Key.Tab, RawInputModifiers.Shift); await DrainRenderingAsync(c);
                var cancel = c.Window.OperationsView.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CancelSelectedOperation");
                await ClickActionAsync(c, cancel);
                await WaitUntilAsync(() => queued.State == FileOperationState.Cancelled);
                await DrainRenderingAsync(c);
                Check(c.Window.OperationsView.IsKeyboardFocusWithin, "Cancelling a queued request keeps focus in its history.");
                gate.Release.TrySetResult();
                await WaitUntilAsync(() => c.Window.Operations.Jobs[0].IsFinished); await DrainRenderingAsync(c);
                Check(c.Window.OperationsView.IsKeyboardFocusWithin, "A changed operation label keeps focus in its history.");
                var hide = c.Window.OperationsView.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ToggleOperationDetails");
                await ClickActionAsync(c, hide);
                Check(!c.Window.OperationsView.DetailsVisible && c.Window.Left.Table.IsKeyboardFocusWithin, "Hiding operation history returns focus to files.");
            }
            finally { gate.Release.TrySetResult(); }
            await c.SettleAsync();
            Check(!File.Exists(Path.Combine(c.RightPath, "file2.log")), "The cancelled queued request never starts.");
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Cancelling queued work keeps the current operation running");
        }),
        new("review-completion-keeps-empty-marks", "UC20; Move and delete never mark a replacement file", async c =>
        {
            foreach (var kind in new[] { FileOperationKind.Move, FileOperationKind.Delete })
            {
                var prefix = $"marked-{kind}";
                foreach (var suffix in new[] { "a", "b", "c" }) c.AddFile($"{prefix}-{suffix}.txt", "mark fixture");
                await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync($"{prefix}-a.txt");
                await c.KeyAsync(Key.Down, RawInputModifiers.Shift); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
                if (kind == FileOperationKind.Move) await MoveAsync(c);
                else
                {
                    await c.KeyAsync(Key.Up); await c.KeyAsync(Key.Delete, RawInputModifiers.Shift); await ConfirmDeleteAsync(c);
                }
                await WaitUntilAsync(() => !c.Window.Left.Rows.Any(row => row.Name == $"{prefix}-a.txt"));
                Equal(0, c.Window.Left.SelectedNames().Length, "Removing every marked file leaves no marks");
                Check(c.Window.Left.Current is { IsMarked: false }, "A replacement cursor stays unmarked.");
                Check(File.Exists(Path.Combine(c.LeftPath, $"{prefix}-c.txt")), "The unmarked file remains in place.");
                await c.KeyAsync(Key.Down);
                Equal(0, c.Window.Left.SelectedNames().Length, "Navigation after completion keeps the empty mark set");
            }
        }),
        new("review-deferred-conflict-explains-wait", "UC33; Explain a conflict waiting for the current edit", async c =>
        {
            File.WriteAllText(Path.Combine(c.RightPath, "alpha.txt"), "existing version");
            await c.SelectAsync("alpha.txt"); c.Window.CommandLine.FocusCommand(); await c.TextAsync("unfinished command");
            c.Window.Operations.Enqueue(new(FileOperationKind.Copy, [c.Window.Left.Rows.Single(row => row.Name == "alpha.txt").Entry], c.RightPath));
            await WaitUntilAsync(() => LastJob(c).State == FileOperationState.WaitingForConflict && c.Window.OperationsView.IsVisible);
            Check(c.Window.OperationsView.ProgressSummary.Contains("Waiting for a conflict decision", StringComparison.Ordinal), "The compact strip explains the waiting state.");
            Check(c.Window.CommandLine.HasKeyboardFocus && c.Window.OperationDialog == null, "The operation preserves the current editor.");
            Equal("unfinished command", c.Window.CommandLine.Editor.Text, "Waiting does not replace the command draft");
            await CaptureProgressAsync(c, "deferred-conflict");
            await c.KeyAsync(Key.Escape); await WaitUntilAsync(() => c.Window.OperationDialog is FileConflictDialog);
            await ChooseConflictAsync(c, "ConflictSkip");
            Equal("existing version", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Skipping the conflict preserves the destination");
        })
    ];
}

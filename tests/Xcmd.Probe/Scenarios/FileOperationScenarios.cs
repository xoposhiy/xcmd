// @tests RefreshEntries, TransferEntries, DuplicateEntry, DeleteEntries, ResolveConflicts, QueueOperations, RecoverOperations, WindowsFileActions
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class FileOperationScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("operations-dialog-access-keys", "Copy, move and delete dialogs expose consistent keyboard actions", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.F5);
            await c.KeyAsync(Key.N, RawInputModifiers.Alt, c.Window.OperationDialog);
            Check(c.Window.OperationDialog == null && !File.Exists(Path.Combine(c.RightPath, "alpha.txt")), "Alt+N cancels the copy dialog");
            await c.KeyAsync(Key.F5);
            await c.KeyAsync(Key.C, RawInputModifiers.Alt, c.Window.OperationDialog);
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Alt+C confirms Copy from the destination field");
            await c.SelectAsync("file2.log"); await c.KeyAsync(Key.F6);
            await c.KeyAsync(Key.M, RawInputModifiers.Alt, c.Window.OperationDialog);
            Check(File.Exists(Path.Combine(c.RightPath, "file2.log")), "Alt+M confirms Move");
            await c.SelectAsync("zeta.md"); await c.KeyAsync(Key.F8);
            await c.KeyAsync(Key.N, RawInputModifiers.Alt, c.Window.OperationDialog);
            Check(File.Exists(Path.Combine(c.LeftPath, "zeta.md")), "Alt+N cancels deletion");
            await c.KeyAsync(Key.F8);
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, c.Window.OperationDialog);
            await c.KeyAsync(Key.Y, RawInputModifiers.Alt, c.Window.OperationDialog);
            Check(!File.Exists(Path.Combine(c.LeftPath, "zeta.md")), "Alt+Y confirms deletion of the isolated fixture");
        }),
        new("operations-delete-large-tree-cancel", "UC35; Cancelling active deletion keeps all unstarted files", async c =>
        {
            var root = DeletionPerformanceProbe.CreateTree(c);
            await c.Window.Left.LoadAsync(c.LeftPath);
            var entry = c.Window.Left.Rows.Single(row => row.Name == "delete-tree").Entry;
            var gate = c.FileSystem.PauseRemovalsAfter(10);
            var job = c.Window.Operations.Enqueue(new(FileOperationKind.Delete, [entry]));
            await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            c.Window.OperationsView.ToggleDetails(); await DrainRenderingAsync(c);
            Equal(10L, job.CompletedFiles, "Deletion has removed files before cancellation");
            await c.ClickAsync(c.Window.OperationsView.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CancelCurrentOperation"));
            Equal(FileOperationState.Cancelled, job.State, "Cancel current stops the active deletion");
            Check(gate.WasCancelled, "Cancellation reaches the pending removal");
            Equal(1190, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count(), "All unstarted files remain on disk");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha.txt")), "Cancellation preserves neighbouring files");
        }),
        new("operations-delete-large-tree", "UC35; Delete many small files while operation details remain responsive", async c =>
        {
            var root = DeletionPerformanceProbe.CreateTree(c);
            await c.Window.Left.LoadAsync(c.LeftPath);
            var entry = c.Window.Left.Rows.Single(row => row.Name == "delete-tree").Entry;
            await DeletionPerformanceProbe.MeasureAsync(c, entry);
            Check(!Directory.Exists(root), "Deletion removes the complete directory tree");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha.txt")), "Deletion preserves neighbouring files");
            await c.SettleAsync();
        }),
        new("operations-copy-tree-and-empty-folder", "UC04; Recursive copy; source focus", async c =>
        {
            Directory.CreateDirectory(Path.Combine(c.LeftPath, "folder1", "empty"));
            await c.SelectAsync("folder1"); await CopyAsync(c);
            Equal("Nested file", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "inside.txt")), "F5 copies nested contents");
            Check(Directory.Exists(Path.Combine(c.RightPath, "folder1", "empty")), "F5 copies empty folders.");
            Check(c.Window.Right.Rows.Any(row => row.Name == "folder1"), "The destination panel refreshes.");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Copy preserves source focus.");
            Equal(FileOperationState.Completed, LastJob(c).State, "The copy finishes");
            c.Window.OperationsView.ToggleDetails(); await c.ScreenshotAsync("file-operations");
            c.Window.Width = 840; c.Window.Height = 400; c.Window.SetScaleForTest(1.5);
            c.Window.OperationsView.ToggleDetails(); await c.ScreenshotAsync("file-operations-compact-150");
        }),
        new("operations-conflict-skip-rename-replace", "UC34; Explicit conflict choices", async c =>
        {
            var destination = Path.Combine(c.RightPath, "alpha.txt"); File.WriteAllText(destination, "Keep me");
            await c.SelectAsync("alpha.txt"); await CopyAsync(c);
            var dialog = (FileConflictDialog)c.Window.OperationDialog!;
            await c.ScreenshotAsync("file-conflict", dialog);
            await ChooseConflictAsync(c, "ConflictSkip");
            Equal("Keep me", File.ReadAllText(destination), "Skip preserves the destination");
            await CopyAsync(c);
            dialog = (FileConflictDialog)c.Window.OperationDialog!; dialog.NewName.Text = "renamed.txt";
            await ChooseConflictAsync(c, "ConflictRename");
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "renamed.txt")), "Rename writes a separate copy");
            Equal("Keep me", File.ReadAllText(destination), "Rename preserves the existing file");
            await CopyAsync(c); await ChooseConflictAsync(c, "ConflictReplace");
            Equal("abc", File.ReadAllText(destination), "Replace uses the source contents");
        }),
        new("operations-conflict-apply-to-all-and-merge", "UC04; UC34; Folder merge and batch conflicts", async c =>
        {
            Directory.CreateDirectory(Path.Combine(c.RightPath, "folder1"));
            File.WriteAllText(Path.Combine(c.RightPath, "folder1", "inside.txt"), "old");
            File.WriteAllText(Path.Combine(c.LeftPath, "folder1", "second.txt"), "new second");
            File.WriteAllText(Path.Combine(c.RightPath, "folder1", "second.txt"), "old second");
            File.WriteAllText(Path.Combine(c.RightPath, "folder1", "destination-only.txt"), "keep");
            await c.SelectAsync("folder1"); await CopyAsync(c);
            var dialog = (FileConflictDialog)c.Window.OperationDialog!;
            await c.KeyAsync(Key.A, RawInputModifiers.Alt, dialog);
            Check(dialog.ApplyToAll.IsChecked == true, "Alt+A enables Apply to all");
            Check(dialog.GetVisualDescendants().OfType<AccessText>().Any(text => text.Text == "_Replace all"), "The Replace label shows its current scope");
            await c.ScreenshotAsync("conflict-replace-all-keyboard", dialog);
            await c.KeyAsync(Key.R, RawInputModifiers.Alt, dialog);
            Equal(FileOperationState.Completed, LastJob(c).State, "One decision resolves matching conflicts");
            Equal("new second", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "second.txt")), "Apply-to-all replaces the second file");
            Equal("keep", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "destination-only.txt")), "Folder merge preserves destination-only files");
        }),
        new("operations-move-and-copy-delete-fallback", "UC05; Same-volume move and copy-delete path", async c =>
        {
            await c.SelectAsync("folder1"); await MoveAsync(c);
            Check(!Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "A same-volume move removes the old path.");
            Equal("Nested file", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "inside.txt")), "Move preserves the tree");
            c.FileSystem.ForceCopyMove = true;
            await c.SelectAsync("alpha.txt"); await MoveAsync(c);
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "The copy-delete fallback writes the contents");
            Check(!File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "The fallback removes the copied source.");
        }),
        new("operations-move-partial-keeps-source", "UC05; UC34; Skipped move conflicts", async c =>
        {
            c.FileSystem.ForceCopyMove = true;
            Directory.CreateDirectory(Path.Combine(c.RightPath, "folder1"));
            File.WriteAllText(Path.Combine(c.RightPath, "folder1", "inside.txt"), "existing");
            File.WriteAllText(Path.Combine(c.LeftPath, "folder1", "move-me.txt"), "move");
            await c.SelectAsync("folder1"); await MoveAsync(c); await ChooseConflictAsync(c, "ConflictSkip");
            Check(File.Exists(Path.Combine(c.LeftPath, "folder1", "inside.txt")), "The skipped source remains.");
            Equal("move", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "move-me.txt")), "Other items still move");
            Check(!File.Exists(Path.Combine(c.LeftPath, "folder1", "move-me.txt")), "Only successfully copied sources are removed.");
            Equal(FileOperationState.CompletedWithSkippedItems, LastJob(c).State, "Skipped move items have an explicit result");
        }),
        new("operations-queue-capture-and-cancel", "UC33; Stable targets; responsive panels", async c =>
        {
            await c.SelectAsync("alpha.txt");
            var gate = c.FileSystem.PauseNextCopy(Path.Combine(c.LeftPath, "alpha.txt"));
            c.PressKey(Key.F5); ConfirmTransfer(c); await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            c.PressKey(Key.Down); c.PressKey(Key.F5); ConfirmTransfer(c);
            await WaitUntilAsync(() => c.Window.Operations.Jobs.Count == 2);
            Equal(2, c.Window.Operations.Jobs.Count, "A later request is queued");
            var queued = LastJob(c); var queuedName = queued.Request.Sources.Single().Name;
            c.PressKey(Key.Tab); await WaitUntilAsync(() => c.Window.ActivePanel == c.Window.Right);
            await c.Window.Right.LoadAsync(Path.Combine(c.LeftPath, "folder2"));
            c.Window.Operations.Cancel(c.Window.Operations.Jobs[0]);
            await c.SettleAsync();
            Check(gate.WasCancelled, "Cancel reaches the active worker.");
            Equal(FileOperationState.Cancelled, c.Window.Operations.Jobs[0].State, "The first request is cancelled");
            Check(File.Exists(Path.Combine(c.RightPath, queuedName)), "The queued request keeps its original destination.");
            Check(!File.Exists(Path.Combine(c.LeftPath, "folder2", queuedName)), "Panel navigation does not change queued targets.");
            Equal("right", c.Window.ActivePanel.Id, "Background completion keeps the active panel");
        }),
        new("operations-cancel-queued-and-conflict", "UC33; Cancel waiting work", async c =>
        {
            File.WriteAllText(Path.Combine(c.RightPath, "alpha.txt"), "existing");
            await c.SelectAsync("alpha.txt"); await CopyAsync(c);
            var queued = c.Window.Operations.Enqueue(new(FileOperationKind.Copy, [c.Window.Left.Rows.Single(row => row.Name == "file2.log").Entry], c.RightPath));
            c.Window.Operations.Cancel(queued);
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            Equal(FileOperationState.Cancelled, LastJob(c).State, "Queued work can be cancelled");
            Check(!File.Exists(Path.Combine(c.RightPath, "file2.log")), "Cancelled queued work never starts.");
            Equal("existing", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Conflict cancellation preserves the destination");
        }),
        new("operations-cancel-copy-preserves-destination", "UC04; UC33; Safe overwrite cancellation", async c =>
        {
            var source = Path.Combine(c.LeftPath, "large.bin"); File.WriteAllBytes(source, new byte[1024 * 1024]);
            var destination = Path.Combine(c.RightPath, "large.bin"); File.WriteAllText(destination, "original");
            using var cancellation = new CancellationTokenSource();
            try
            {
                await new LocalFileOperations(c.FileSystem).CopyAsync(source, destination, true, new CancelCopyProgress(cancellation), cancellation.Token);
                throw new InvalidOperationException("The copy must observe cancellation.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            Equal("original", File.ReadAllText(destination), "Cancelling an overwrite preserves the original destination");
            Check(!Directory.EnumerateFiles(c.RightPath, ".xcmd-*.tmp").Any(), "Cancellation removes the temporary copy.");
        }),
        new("operations-delete-confirmation-and-permanent-tree", "UC35; Explicit permanent deletion", async c =>
        {
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Delete);
            Check(c.Window.OperationDialog is FileDeleteDialog, "Delete opens Recycle Bin confirmation.");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            Check(Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "Cancel leaves the selected tree intact.");
            await c.KeyAsync(Key.Delete, RawInputModifiers.Shift);
            await c.ScreenshotAsync("permanent-delete", c.Window.OperationDialog);
            await ConfirmDeleteAsync(c);
            Check(!Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "Confirmed permanent deletion removes the selected tree.");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Deletion returns focus to the panel.");
        }),
        new("operations-shift-f8-permanent-deletion", "UC35; F8 selects recycling and Shift+F8 selects permanent deletion", async c =>
        {
            await c.SelectAsync("folder1");
            foreach (var (key, modifiers, kind) in new[]
            {
                (Key.F8, RawInputModifiers.None, FileOperationKind.Recycle),
                (Key.F8, RawInputModifiers.Shift, FileOperationKind.Delete),
                (Key.Delete, RawInputModifiers.Shift, FileOperationKind.Delete),
                (Key.Delete, RawInputModifiers.None, FileOperationKind.Recycle)
            })
            {
                await c.KeyAsync(key, modifiers);
                Check(c.Window.OperationDialog is FileDeleteDialog, "The shortcut always opens a confirmation");
                Equal(kind, ((FileDeleteDialog)c.Window.OperationDialog!).SelectedKind, "The shortcut selects the requested deletion mode");
                Check(Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "Opening confirmation does not remove the tree");
                await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
                Check(Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "Cancel keeps the selected tree");
            }
            await c.KeyAsync(Key.F8, RawInputModifiers.Shift); await ConfirmDeleteAsync(c);
            Equal(FileOperationKind.Delete, LastJob(c).Request.Kind, "Confirming Shift+F8 queues permanent deletion");
            Check(!Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "Confirmed Shift+F8 removes the selected fixture tree");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Deletion returns focus to the panel");
        }),
        new("operations-native-recycle-retains-contents", "UC35; Real Windows Recycle Bin", async c =>
        {
            var name = "xcmd-recycle-" + Guid.NewGuid().ToString("N") + ".txt";
            c.AddFile(name, "recoverable fixture"); await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync(name); await c.KeyAsync(Key.F8); await ConfirmDeleteAsync(c);
            var original = Path.Combine(c.LeftPath, name);
            Check(!File.Exists(original), "F8 removes the selected fixture from its original directory.");
            VerifyAndRestoreRecycleFixture(original, "recoverable fixture");
            Equal(FileOperationState.Completed, LastJob(c).State, "Windows reports successful recycling");
            var folderName = "xcmd-recycle-folder-" + Guid.NewGuid().ToString("N");
            var folder = Path.Combine(c.LeftPath, folderName); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "inside.txt"), "recoverable tree");
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync(folderName);
            await c.KeyAsync(Key.Delete, RawInputModifiers.Shift);
            var choice = (FileDeleteDialog)c.Window.OperationDialog!;
            await c.ClickAsync(choice.Recycle, target: choice); await c.KeyAsync(Key.Enter, target: choice);
            Check(!Directory.Exists(folder), "The selected mode recycles the whole tree.");
            VerifyAndRestoreRecycleFolder(folder, "inside.txt", "recoverable tree");
        }),
        new("operations-self-descendant-and-type-conflict", "UC04; UC05; Safe operation targets", async c =>
        {
            await c.SelectAsync("folder1");
            await c.Window.Right.LoadAsync(Path.Combine(c.LeftPath, "folder1"));
            await CopyAsync(c);
            Check(c.Window.OperationDialog is FileTransferDialog { Error.Text.Length: > 0 }, "Copying into a descendant is rejected before submission.");
            Equal(0, c.Window.Operations.Jobs.Count, "An invalid destination never enters the queue");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            Check(!Directory.Exists(Path.Combine(c.LeftPath, "folder1", "folder1")), "The rejected operation does not create a recursive tree.");
            await c.Window.Right.LoadAsync(c.RightPath);
            File.WriteAllText(Path.Combine(c.RightPath, "folder1"), "keep file");
            await CopyAsync(c);
            var dialog = (FileConflictDialog)c.Window.OperationDialog!;
            Check(!dialog.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "ConflictReplace").IsEnabled, "A folder cannot replace an unrelated file.");
            await ChooseConflictAsync(c, "ConflictSkip");
            Equal("keep file", File.ReadAllText(Path.Combine(c.RightPath, "folder1")), "Type conflicts preserve the destination");
            Equal(1L, LastJob(c).SkippedFiles, "Skipping a folder accounts for its contained files");
        }),
        new("operations-failure-continues-batch", "UC04; Partial failures", async c =>
        {
            var locked = Path.Combine(c.LeftPath, "folder1", "inside.txt");
            File.WriteAllText(Path.Combine(c.LeftPath, "folder1", "other.txt"), "copy me");
            using var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
            await c.SelectAsync("folder1"); await CopyAsync(c); await SkipFileErrorAsync(c);
            Equal("copy me",File.ReadAllText(Path.Combine(c.RightPath, "folder1", "other.txt")), "One failed file does not stop other items");
            Equal(FileOperationState.CompletedWithErrors, LastJob(c).State, "The batch reports its partial failure");
            Check(LastJob(c).Outcomes.Any(result => result.Kind == FileOutcomeKind.Failed), "The report names the failed item.");
        }),
        new("operations-junction-cycle-and-safe-delete", "UC04; UC35; UC36; Links are not traversed", async c =>
        {
            await DirectorySizeProbe.CreateJunctionAsync(c);
            await c.SelectAsync("folder1"); await CopyAsync(c); await SkipFileErrorAsync(c);
            Equal(FileOperationState.CompletedWithErrors, LastJob(c).State, "Copy reports an unsupported junction");
            Equal("Nested file", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "inside.txt")), "Ordinary files still copy");
            Check(!Directory.Exists(Path.Combine(c.RightPath, "folder1", "back")), "Copy does not follow the junction cycle.");
            await c.KeyAsync(Key.Delete, RawInputModifiers.Shift); await ConfirmDeleteAsync(c);
            Check(!Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "Permanent deletion removes the junction itself.");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha.txt")), "Deleting the junction preserves its target contents");
        }),
        new("operations-recycle-refuses-permanent-fallback", "UC35; Recycle Bin failure keeps the source", () =>
        {
            var sink = new RecycleProgressSink(CancellationToken.None);
            Check(sink.PreDeleteItem(0, 0) < 0 && sink.RefusedPermanentDeletion, "The native callback refuses a permanent fallback.");
            var permitted = new RecycleProgressSink(CancellationToken.None);
            Equal(0, permitted.PreDeleteItem(0x80, 0), "The native callback allows recyclable items");
        }),
        new("operations-feedback-transfer-confirmation", "UC04; UC05; Confirm source and destination", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F5);
            var dialog = (FileTransferDialog)c.Window.OperationDialog!;
            Check(dialog.Destination.IsFocused, "The destination is ready for editing.");
            Equal(Path.TrimEndingDirectorySeparator(c.RightPath), Path.TrimEndingDirectorySeparator(dialog.Destination.Text!), "F5 proposes the other panel");
            Check(!File.Exists(Path.Combine(c.RightPath, "alpha.txt")), "F5 waits for confirmation.");
            await c.ScreenshotAsync("copy-confirmation", dialog);
            await c.KeyAsync(Key.Escape, target: dialog);
            Equal(0, c.Window.Operations.Jobs.Count, "Cancelling does not queue a copy");
            await c.KeyAsync(Key.F6);
            dialog = (FileTransferDialog)c.Window.OperationDialog!;
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "F6 waits for confirmation.");
            dialog.Destination.Text = Path.Combine(c.RightPath, "moved.txt");
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "moved.txt")), "An edited destination name is used");
            Check(!File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Confirmed move removes the source.");
        }),
        new("operations-feedback-copy-in-source-panel", "UC04; Shift+F5 duplicates in the source panel", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F5, RawInputModifiers.Shift);
            var dialog = (FileTransferDialog)c.Window.OperationDialog!;
            Equal(Path.Combine(c.LeftPath, "alpha (copy).txt"), dialog.Destination.Text, "Shift+F5 proposes a copy in this panel");
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha (copy).txt")), "Shift+F5 creates the duplicate");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha.txt")), "The source is preserved");
            Check(!File.Exists(Path.Combine(c.RightPath, "alpha (copy).txt")), "The other panel is not the destination.");
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.F5, RawInputModifiers.Shift);
            await c.KeyAsync(Key.Enter, target: c.Window.OperationDialog);
            Equal("Nested file", File.ReadAllText(Path.Combine(c.LeftPath, "folder1 (copy)", "inside.txt")), "Shift+F5 also duplicates a tree");
        }),
        new("operations-feedback-delete-choice-and-enter", "UC35; Choose deletion mode and confirm with Enter", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Delete);
            var dialog = (FileDeleteDialog)c.Window.OperationDialog!;
            Equal(FileOperationKind.Recycle, dialog.SelectedKind, "Delete defaults to the Recycle Bin");
            Check(dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ConfirmFileDelete").IsFocused, "Yes has initial focus.");
            await c.ClickAsync(dialog.Permanently, target: dialog);
            await c.ScreenshotAsync("delete-choice", dialog);
            await c.KeyAsync(Key.Enter, target: dialog);
            Equal(FileOperationKind.Delete, LastJob(c).Request.Kind, "The selected mode overrides the shortcut default");
            Check(!File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Enter confirms deletion.");
            await c.SelectAsync("zeta.md"); await c.KeyAsync(Key.Delete, RawInputModifiers.Shift);
            dialog = (FileDeleteDialog)c.Window.OperationDialog!;
            Equal(FileOperationKind.Delete, dialog.SelectedKind, "Shift+Delete defaults to permanent deletion");
            await c.ClickAsync(dialog.Recycle, target: dialog);
            Equal(FileOperationKind.Recycle, dialog.SelectedKind, "The dialog also allows switching back to the Recycle Bin");
            await c.KeyAsync(Key.Escape, target: dialog);
            Check(File.Exists(Path.Combine(c.LeftPath, "zeta.md")), "Cancelling preserves the selected file.");
        }),
        new("operations-feedback-reject-batch-self-copy", "UC41; Reject a batch directed to its source folder", async c =>
        {
            c.AddFile("alpha (copy).txt", "existing duplicate");
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync("alpha.txt"); await c.ClickAsync(c.Row("file2.log"), RawInputModifiers.Control);
            Equal(2, c.Window.Left.SelectedNames().Length, "Two items are selected");
            await c.KeyAsync(Key.F5, RawInputModifiers.Shift); await c.KeyAsync(Key.Enter, target: c.Window.OperationDialog);
            Check(c.Window.OperationDialog is FileTransferDialog { Error.Text.Length: > 0 }, "A batch cannot be copied onto itself.");
            Equal(0, c.Window.Operations.Jobs.Count, "The rejected batch never starts");
            Equal("existing duplicate", File.ReadAllText(Path.Combine(c.LeftPath, "alpha (copy).txt")), "An existing duplicate is preserved");
            Check(!File.Exists(Path.Combine(c.LeftPath, "alpha (copy 2).txt")) && !File.Exists(Path.Combine(c.LeftPath, "file2 (copy).log")), "No automatic duplicates are created.");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
        }),
        new("operations-feedback-delayed-stable-progress", "UC33; Show only long operations without changing height", async c =>
        {
            await c.SelectAsync("file2.log"); await CopyAsync(c);
            Check(!c.Window.OperationsView.IsVisible, "A short copy does not open the operation strip.");
            var first = c.Window.Left.Rows.Single(row => row.Name == "alpha.txt").Entry;
            var second = c.Window.Left.Rows.Single(row => row.Name.Length > 180).Entry;
            var firstGate = c.FileSystem.PauseNextCopy(first.FullPath);
            c.Window.Operations.Enqueue(new(FileOperationKind.Copy, [first, second], c.RightPath));
            await WaitUntilAsync(() => firstGate.Entered.Task.IsCompleted);
            Check(!c.Window.OperationsView.IsVisible, "Progress starts hidden while the delay is pending.");
            await WaitUntilAsync(() => c.Window.OperationsView.IsVisible && c.Window.OperationsView.Bounds.Height > 0);
            var height = c.Window.OperationsView.Bounds.Height;
            var secondGate = c.FileSystem.PauseNextCopy(second.FullPath);
            firstGate.Release.TrySetResult();
            await WaitUntilAsync(() => secondGate.Entered.Task.IsCompleted);
            c.Window.UpdateLayout();
            Equal(height, c.Window.OperationsView.Bounds.Height, "Longer names do not change the strip height");
            secondGate.Release.TrySetResult(); await c.SettleAsync();
            Equal(FileOperationState.Completed, LastJob(c).State, "The long-path copy completes rather than waiting for an error decision");
            Equal(second.Size, new FileInfo(Path.Combine(c.RightPath, second.Name)).Length, "The long file name and contents reach the destination");
            await WaitUntilAsync(() => !c.Window.OperationsView.IsVisible);
            c.Window.OperationsView.ToggleDetails(); await c.SettleAsync();
            Check(c.Window.OperationsView.IsVisible, "The operation history remains available on request.");
        })
    ];
}

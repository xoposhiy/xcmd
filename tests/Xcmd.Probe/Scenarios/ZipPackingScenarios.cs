using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class ZipPackingScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("zip-menu-marked-files", "UC22; the native menu exposes Add to ZIP and packs the marked group through the real queue", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var marked = c.Window.Left.SelectedNames();
            var native = WindowsShellInteraction.InspectMenu([Path.Combine(c.LeftPath, "alpha.txt"), Path.Combine(c.LeftPath, "file2.log")]);
            Check(native.ApplicationCommands.Contains("Add to &ZIP…"), "The real native menu contains the ZIP action");
            Check(native.Verbs.Contains("copy", StringComparer.OrdinalIgnoreCase), "Windows commands remain available");
            var dialog = await ZipPackingProbe.OpenAsync(c, "alpha.txt");
            await ZipPackingProbe.CaptureDialogAsync(c, dialog);
            dialog.Editor.Text = "selected files"; await c.KeyAsync(Key.Enter, target: dialog);
            var job = c.Window.Operations.Jobs.Single();
            Equal(FileOperationState.Completed, job.State, "The queued ZIP completes");
            var entries = ZipPackingProbe.Read(Path.Combine(c.LeftPath, "selected files.zip"));
            Equal("alpha.txt|file2.log", string.Join('|', entries.Keys.Order(StringComparer.Ordinal)), "Only the two marked files are archived");
            Equal("abc", entries["alpha.txt"], "The archive preserves actual file contents");
            Equal(new string('b', 20), entries["file2.log"], "The second marked file is present");
            Equal(2L, job.CompletedFiles, "Queue reports packed files");
            Check(marked.SequenceEqual(c.Window.Left.SelectedNames()), "Packing keeps the source marks");
            Check(c.Window.Left.Rows.Any(row => row.Name == "selected files.zip"), "Panel refresh reveals the ZIP");
        }),
        new("zip-menu-folder-and-unicode", "UC22; keyboard invocation packs folders, empty directories and Unicode names", async c =>
        {
            var folder = Path.Combine(c.LeftPath, "folder1");
            Directory.CreateDirectory(Path.Combine(folder, "empty"));
            File.WriteAllText(Path.Combine(folder, "отчёт 日本語.txt"), "Unicode contents");
            await c.SelectAsync("folder1");
            var dialog = await ZipPackingProbe.OpenAsync(c, "folder1", keyboard: true);
            Equal("folder1.zip", dialog.Editor.Text, "A single folder supplies the default archive name");
            dialog.Editor.Text = "отчёт.zip"; await c.KeyAsync(Key.Enter, target: dialog);
            var entries = ZipPackingProbe.Read(Path.Combine(c.LeftPath, "отчёт.zip"));
            Check(entries.ContainsKey("folder1/empty/"), "Empty directories survive");
            Equal("Nested file", entries["folder1/inside.txt"], "Folder hierarchy survives");
            Equal("Unicode contents", entries["folder1/отчёт 日本語.txt"], "Names and contents survive");
            Check(File.Exists(Path.Combine(folder, "inside.txt")), "Packing keeps its source files");
        }),
        new("zip-menu-invalid-existing-and-cancel", "UC22; an unmarked item targets only itself and invalid or existing names never overwrite", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var marks = c.Window.Left.SelectedNames();
            File.WriteAllText(Path.Combine(c.LeftPath, "existing.zip"), "keep this archive");
            var dialog = await ZipPackingProbe.OpenAsync(c, "zeta.md");
            Equal(1, c.Shell.MenuGroups.Last().Length, "An unmarked file targets only itself");
            dialog.Editor.Text = "../invalid"; await c.KeyAsync(Key.Enter, target: dialog);
            Check(!string.IsNullOrWhiteSpace(dialog.Error.Text), "Invalid names stay in the dialog");
            dialog.Editor.Text = "existing.zip"; await c.KeyAsync(Key.Enter, target: dialog);
            Equal("keep this archive", File.ReadAllText(Path.Combine(c.LeftPath, "existing.zip")), "Existing archive is unchanged");
            Check(!string.IsNullOrWhiteSpace(dialog.Error.Text), "The name collision explains what to fix");
            await c.KeyAsync(Key.Escape, target: dialog);
            Equal(0, c.Window.Operations.Jobs.Count, "Cancel never starts packing");
            Check(marks.SequenceEqual(c.Window.Left.SelectedNames()), "Marks survive a single-item dialog and cancel");
        }),
        new("zip-error-retry-is-atomic", "UC22; a locked source leaves no partial archive and retry creates a readable ZIP", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            using var locked = new FileStream(Path.Combine(c.LeftPath, "file2.log"), FileMode.Open, FileAccess.Read, FileShare.None);
            var dialog = await ZipPackingProbe.OpenAsync(c, "alpha.txt");
            dialog.Editor.Text = "retry.zip"; await c.KeyAsync(Key.Enter, target: dialog);
            Equal(FileOperationState.WaitingForError, c.Window.Operations.Jobs.Single().State, "The queue offers a retry");
            Check(!File.Exists(Path.Combine(c.LeftPath, "retry.zip")), "No incomplete archive is published");
            Equal(0, Directory.GetFiles(c.LeftPath, ".xcmd-*.tmp").Length, "Failed attempts clean up their temporary files");
            locked.Dispose(); await PriorityBatchProbe.ChooseErrorAsync(c, "FileErrorRetry");
            Equal(FileOperationState.Completed, c.Window.Operations.Jobs.Single().State, "Retry succeeds");
            Equal(2, ZipPackingProbe.Read(Path.Combine(c.LeftPath, "retry.zip")).Count, "The successful archive contains the whole group once");
        }),
        new("zip-native-cancellation", "UC22; cancelling the real writer removes incomplete output and keeps source files", async c =>
        {
            var source = c.Window.Left.Rows.Single(row => row.Name == "alpha.txt").Entry;
            var destination = Path.Combine(c.LeftPath, "cancelled.zip");
            using var cancellation = new CancellationTokenSource();
            try
            {
                await ZipArchiveWriter.CreateAsync([source], destination, new ZipPackingProbe.CancelAfterProgress(cancellation), cancellation.Token);
                throw new InvalidOperationException("The writer should observe cancellation.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            Check(!File.Exists(destination), "Cancellation never publishes the archive");
            Equal(0, Directory.GetFiles(c.LeftPath, ".xcmd-*.tmp").Length, "Cancellation cleans temporary output");
            Equal("abc", File.ReadAllText(source.FullPath), "Cancellation leaves the original unchanged");
        })
    ];
}

using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.PriorityBatchProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class PriorityBatchScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("priority-copy-metadata", "OPS-01; real Windows copy preserves timestamps, attributes and additional streams", async c =>
        {
            var source = Path.Combine(c.LeftPath, "alpha.txt"); var target = Path.Combine(c.RightPath, "alpha.txt");
            var created = new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            File.WriteAllText(source + ":xcmd-test", "additional stream");
            File.SetCreationTimeUtc(source, created);
            var attributes = File.GetAttributes(source) | FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly;
            File.SetAttributes(source, attributes);
            try
            {
                await CopyAsync(c, "alpha.txt");
                Equal(FileOperationState.Completed, c.Window.Operations.Jobs.Last().State, "Metadata copy completes");
                Equal(created, File.GetCreationTimeUtc(target), "Creation time survives");
                Equal(File.GetLastWriteTimeUtc(source), File.GetLastWriteTimeUtc(target), "Modification time survives");
                Equal(attributes, File.GetAttributes(target), "Hidden, System and ReadOnly survive");
                Equal("additional stream", File.ReadAllText(target + ":xcmd-test"), "ADS contents survive");
                var folder = Path.Combine(c.LeftPath, "folder1"); Directory.SetCreationTimeUtc(folder, created);
                await CopyAsync(c, "folder1");
                Equal(created, Directory.GetCreationTimeUtc(Path.Combine(c.RightPath, "folder1")), "New folder creation time survives");
            }
            finally
            {
                File.SetAttributes(source, FileAttributes.Normal);
                if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);
            }
        }),
        new("priority-retry-file", "OPS-02; release a locked source and retry only the failed file", async c =>
        {
            var source = Path.Combine(c.LeftPath, "alpha.txt");
            var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None);
            try
            {
                await CopyAsync(c, "alpha.txt");
                Check(c.Window.OperationDialog is FileErrorDialog, "A failed file offers a decision");
                Equal(FileOperationState.WaitingForError, c.Window.Operations.Jobs.Last().State, "Queue waits for retry");
                locked.Dispose();
                await ChooseErrorAsync(c, "FileErrorRetry");
                Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Retry copies the unlocked file");
                Equal(0L, c.Window.Operations.Jobs.Last().FailedEntries, "Recovered attempt is not a permanent failure");
                Equal(1L, c.Window.Operations.Jobs.Last().ProcessedFiles, "Retry counts the file once");
            }
            finally { locked.Dispose(); }
        }),
        new("priority-retry-skip-and-cancel", "OPS-02; similar errors may be skipped and waiting work cancelled", async c =>
        {
            using var first = new FileStream(Path.Combine(c.LeftPath, "alpha.txt"), FileMode.Open, FileAccess.Read, FileShare.None);
            using var second = new FileStream(Path.Combine(c.LeftPath, "file2.log"), FileMode.Open, FileAccess.Read, FileShare.None);
            var entries = c.Window.Left.Rows.Where(row => row.Name is "alpha.txt" or "file2.log" or "zeta.md").Select(row => row.Entry).ToArray();
            c.Window.Operations.Enqueue(new(FileOperationKind.Copy, entries, c.RightPath)); await c.SettleAsync();
            await ChooseErrorAsync(c, "FileErrorSkipAll");
            Equal(FileOperationState.CompletedWithErrors, c.Window.Operations.Jobs.Last().State, "Skipped failures remain visible");
            Equal(2L, c.Window.Operations.Jobs.Last().FailedEntries, "Both failed items are recorded");
            Check(File.Exists(Path.Combine(c.RightPath, "zeta.md")), "Unblocked files still copy");
            await CopyAsync(c, "alpha.txt");
            await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
            Equal(FileOperationState.Cancelled, c.Window.Operations.Jobs.Last().State, "Escape cancels waiting work");
        }),
    ];
}

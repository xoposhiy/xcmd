// @tests SortEntries, ReadablePanels, RefreshEntries, MarkEntries, MeasureFolders, ViewSelectionSize, RestoreWorkspace, TryLocalRelease
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.DirectorySizeProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Scenarios;

public static class DirectorySizeScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("selection-size-readable-units", "UC20; readable selected-file totals and exact byte tooltip", async c =>
        {
            foreach (var (bytes, expected) in new[] { (0L, "0 bytes"), (1023L, "1023 bytes"), (1024L, "1 KiB"), (1024L * 1024, "1 MiB"), (1024L * 1024 * 1024, "1 GiB"), (1024L * 1024 * 1024 * 1024, "1 TiB") })
                Equal(bytes == 1023 ? $"{bytes:N0} bytes" : expected, FileSizeText.Format(bytes), "Unit boundary at " + bytes);
            c.AddFile("readable-size.bin", new string('x', 652956));
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            await c.SelectAsync("readable-size.bin");
            var summary = c.Window.Left.GetVisualDescendants().OfType<TextBlock>().Single(text => Avalonia.Automation.AutomationProperties.GetAutomationId(text) == "left.summary");
            Check(summary.Text!.Contains($"{637.7m:0.#} KiB", StringComparison.Ordinal), "A real file is shown in readable units");
            Equal($"Selected: {652956:N0} bytes", ToolTip.GetTip(summary) as string, "Exact bytes remain available in the tooltip");
            await c.ClickAsync(c.Row("alpha.txt"), RawInputModifiers.Control);
            Equal($"Selected: {652959:N0} bytes", ToolTip.GetTip(summary) as string, "Multiple selected files are summed before formatting");
            await c.ScreenshotAsync("readable-selection-size");
        }),
        new("folder-size-recursive-and-empty", "UC12; recursive folder sizes", async c =>
        {
            var expected = AddNestedFiles(c); await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift);
            var folder = c.Window.Left.Rows.Single(row => row.Name == "folder1");
            Equal(expected, folder.Measurement!.Size.Bytes, "The size includes nested file bytes");
            Equal(2L, folder.Measurement.Size.Files, "The size includes nested file counts");
            Equal("0", c.Window.Left.Rows.Single(row => row.Name == "folder2").DisplaySize, "An empty directory has zero bytes");
            Check(c.Window.Left.Rows.Single(row => row.Name == "..").Measurement == null, "The parent row is never measured.");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Calculating sizes preserves the file cursor");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Calculating sizes preserves focus.");
            var sizeCell = c.Row("folder1").GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == expected.ToString("N0"));
            Check(ToolTip.GetTip(sizeCell)?.ToString()?.Contains("Files: 2") == true, "The displayed cell reports its measurement details.");
            await c.ScreenshotAsync("calculated-folders");
        }),
        new("folder-size-sort-restore-and-refresh", "UC12; UC38; measurement persistence", async c =>
        {
            var expected = AddNestedFiles(c); await c.KeyAsync(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift);
            await c.ClickHeaderAsync("Size");
            Equal(expected, c.Window.Left.Rows.Single(row => row.Name == "folder1").Measurement!.Size.Bytes, "Sorting keeps the measured sizes");
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json")); await store.SaveAsync(c.Window.CaptureWorkspace());
            await c.Window.RestoreWorkspaceAsync((await store.ReadAsync())!);
            Equal(expected, c.Window.Left.Rows.Single(row => row.Name == "folder1").Measurement!.Size.Bytes, "Session restore keeps the measured sizes");
            using var updates = new DirectoryUpdates(c.Window); await updates.PollAsync();
            Equal(expected, c.Window.Left.Rows.Single(row => row.Name == "folder1").Measurement!.Size.Bytes, "An unchanged monitor refresh keeps restored measurements");
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Check(c.Window.Left.Rows.Where(row => row.Entry.IsDirectory).All(row => row.Measurement == null), "An explicit reload clears old measurements for recalculation.");
        }),
        new("folder-size-cancel-and-responsive-input", "UC12; background work and cancellation", async c =>
        {
            var gate = c.FileSystem.PauseNextRead(Path.Combine(c.LeftPath, "folder1"));
            c.PressKey(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(c.Window.IsCalculatingDirectorySizes && c.Window.Readiness.Busy && !c.Window.Readiness.CanRestart, "A running measurement defers release changes.");
            c.PressKey(Key.Tab); Equal("right", c.Window.ActivePanel.Id, "Panel switching remains responsive during a scan");
            c.PressKey(Key.Escape); await c.SettleAsync();
            Check(gate.WasCancelled && !c.Window.IsCalculatingDirectorySizes, "Escape cancels the outstanding directory read.");
            Check(c.Window.Right.Table.IsKeyboardFocusWithin, "Cancellation leaves focus in the panel the user selected.");
            await c.KeyAsync(Key.Tab); await c.KeyAsync(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift);
            Check(c.Window.Left.Rows.Single(row => row.Name == "folder1").Measurement != null, "A canceled measurement can be started again.");
        }),
        new("folder-size-live-refresh-keeps-other-measurements", "UC12; UC20; unrelated updates preserve measured folder sizes", async c =>
        {
            var expected = AddNestedFiles(c);
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift);
            using var updates = new DirectoryUpdates(c.Window); updates.Start();
            c.AddFile("unrelated.txt", "An external change beside the measured folders");
            await WaitAsync(() => c.Window.Left.Rows.Any(row => row.Name == "unrelated.txt"));
            Equal(expected, c.Window.Left.Rows.Single(row => row.Name == "folder1").Measurement?.Size.Bytes, "An unrelated new file preserves the recursive measurement");
            Equal("0", c.Window.Left.Rows.Single(row => row.Name == "folder2").DisplaySize, "Other measured folders keep their sizes too");
            var changed = ChangeEmptyFolder(c);
            await WaitAsync(() => c.Window.Left.Rows.Any(row => row.Name == "folder2" && row.Entry.ModifiedUtc == changed));
            Check(c.Window.Left.Rows.Single(row => row.Name == "folder2").Measurement == null, "A changed folder loses only its own stale measurement");
            Equal(expected, c.Window.Left.Rows.Single(row => row.Name == "folder1").Measurement?.Size.Bytes, "An update to another folder does not clear this measurement");
        }),
        new("folder-size-navigation-cancels-old-results", "UC12; navigation during background work", async c =>
        {
            var gate = c.FileSystem.PauseNextRead(Path.Combine(c.LeftPath, "folder1"));
            c.PressKey(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            c.PressKey(Key.Down); c.PressKey(Key.Enter); await c.SettleAsync();
            Check(gate.WasCancelled && !c.Window.IsCalculatingDirectorySizes, "Leaving the source directory cancels its scan.");
            Equal(Path.Combine(c.LeftPath, "folder2"), c.Window.Left.Session.DirectoryPath, "The requested directory opens during cancellation");
            Check(c.Window.Left.Rows.All(row => row.Measurement == null), "Late measurements cannot appear in another directory.");
        }),
        new("folder-size-partial-access", "UC12; unreadable subdirectories", async c =>
        {
            var blocked = Path.Combine(c.LeftPath, "folder1", "blocked"); Directory.CreateDirectory(blocked); c.FileSystem.DenyRead(blocked);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift);
            var folder = c.Window.Left.Rows.Single(row => row.Name == "folder1");
            Equal(11L, folder.Measurement!.Size.Bytes, "Readable files still contribute when one directory is unavailable");
            Equal(1, folder.Measurement.Size.Errors, "The result records the unreadable directory");
            Check(folder.DisplaySize.StartsWith("≥ ") && folder.SizeDescription.Contains("Unreadable directories: 1"), "Incomplete totals are marked in the cell and tooltip.");
            Equal("0", c.Window.Left.Rows.Single(row => row.Name == "folder2").DisplaySize, "A partial result does not stop other folders");
        }),
        new("folder-size-excluded-links", "UC12; regular-file totals exclude link entries without becoming incomplete", async c =>
        {
            await SupplyExcludedLinksAsync(c);
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Space);
            var folder = c.Window.Left.Current!;
            Equal("11", folder.DisplaySize, "Only regular file bytes contribute to the exact total");
            Equal(2, folder.Measurement!.Size.SkippedLinks, "Both directory and file links are excluded");
            Equal(0, folder.Measurement.Size.Errors, "The excluded directory link is never read");
            Equal(1L, folder.Measurement.Size.Files, "Link entries do not count as regular files");
            await c.SelectAsync("root-link"); await c.KeyAsync(Key.Space);
            Equal("0", c.Window.Left.Current!.DisplaySize, "Measuring a directory link itself returns zero");
        }),
        new("folder-size-native-junction-cycle", "UC12; native reparse point integration", async c =>
        {
            await CreateJunctionAsync(c);
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Space);
            var folder = c.Window.Left.Current!;
            var size = folder.Measurement!.Size;
            Equal(11L, size.Bytes, "A junction cycle cannot duplicate file bytes");
            Equal(1, size.SkippedLinks, "Native junctions are detected and skipped");
            Check(!size.Incomplete && folder.DisplaySize == "11", "Excluded links leave an exact regular-file total without a lower-bound prefix");
            using var updates = new DirectoryUpdates(c.Window); updates.Start();
            c.AddFile("unrelated.txt", "A later refresh");
            await WaitAsync(() => c.Window.Left.Rows.Any(row => row.Name == "unrelated.txt"));
            Equal("11", c.Window.Left.Rows.Single(row => row.Name == "folder1").DisplaySize, "The linked folder's size survives an unrelated automatic refresh");
            await c.KeyAsync(Key.Enter); await c.SelectAsync("back"); await c.KeyAsync(Key.Space);
            Equal("0", c.Window.Left.Current!.DisplaySize, "Space on the junction itself reports zero without following it");
        }),
        new("folder-size-close-cancels-scan", "UC12; UC38; close during background work", async c =>
        {
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json"));
            using var lifecycle = new WorkspaceLifecycle(c.Window, store, null); await lifecycle.InitializeAsync(new[] { c.LeftPath, c.RightPath });
            var gate = c.FileSystem.PauseNextRead(Path.Combine(c.LeftPath, "folder1"));
            c.PressKey(Key.Enter, RawInputModifiers.Alt | RawInputModifiers.Shift); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            c.Window.Close(); await WaitAsync(() => !c.Window.IsVisible);
            Check(gate.WasCancelled, "Closing cancels the outstanding scan before saving.");
            Equal(c.LeftPath, (await store.ReadAsync())!.Left.DirectoryPath, "Closing still saves the workspace");
        })
    ];
}

using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;

namespace Xcmd.Probe.Scenarios;

public static class ArchiveInteractionScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("archive-empty-tar-and-zero-binary-detection", "UC09; UC21; empty archives and binary files keep distinct views", async c =>
        {
            ArchiveProbe.EmptyTar(Path.Combine(c.LeftPath, "empty.tar"));
            ArchiveProbe.EmptyTar(Path.Combine(c.LeftPath, "zeros.bin"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("zeros.bin"); await c.KeyAsync(Key.F3);
            Equal(FileViewMode.Hex, c.Window.Viewer!.Mode, "Ordinary zero-filled data remains a byte view");
            Check(!c.Window.Viewer.IsArchiveReport, "Zero-filled data is not automatically presented as an empty archive");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer); await c.SelectAsync("empty.tar"); await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer!.IsArchiveReport && c.Window.Viewer.TextContent.Text!.Contains("Files: 0", StringComparison.Ordinal), "An explicit empty TAR still has an archive report");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer); await c.SelectAsync("zeros.bin");
            await c.KeyAsync(Key.PageDown, RawInputModifiers.Control);
            Check(ArchivePath.IsEntry(c.Window.Left.Session.DirectoryPath), "Ctrl+PageDown can explicitly enter an ambiguous empty archive");
            await c.KeyAsync(Key.PageUp, RawInputModifiers.Control); await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer!.IsArchiveReport, "An explicitly recognized archive keeps report behavior after leaving it");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer); await c.SelectAsync("zeros.bin");
            await c.KeyAsync(Key.PageDown, RawInputModifiers.Control);
            Check(ArchivePath.IsEntry(c.Window.Left.Session.DirectoryPath), "Ctrl+PageDown can explicitly enter an ambiguous empty archive");
            await c.KeyAsync(Key.PageUp, RawInputModifiers.Control); await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer!.IsArchiveReport, "An explicitly recognized archive keeps report behavior after leaving it");
        }),
        new("archive-pending-open-keeps-other-panel-focus", "UC21; usable panels during archive reads", async c =>
        {
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "sample.zip"), ("notes.txt", "Archived text"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("sample.zip");
            var gate = c.Archives.PauseNextRead(ArchivePath.Root(Path.Combine(c.LeftPath, "sample.zip")));
            c.PressKey(Key.Enter); await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            Check(c.Window.StatusMessage.Contains("Opening archive", StringComparison.Ordinal), "Pending archive work is visible");
            c.PressKey(Key.Tab);
            Equal(c.Window.Right, c.Window.ActivePanel, "The other panel remains available while reading");
            gate.Release.TrySetResult(); await c.SettleAsync();
            Equal(c.Window.Right, c.Window.ActivePanel, "Archive completion preserves the user's active panel");
            Check(c.Window.Right.Table.IsKeyboardFocusWithin, "Archive completion does not steal keyboard focus");
        }),
        new("archive-opening-cancel-preserves-workspace", "UC21; direct cancellation and obsolete-read rejection", async c =>
        {
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "sample.zip"), ("notes.txt", "Archived text"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("sample.zip");
            c.Window.CommandLine.Editor.Text = "unfinished command";
            var gate = c.Archives.PauseNextRead(ArchivePath.Root(Path.Combine(c.LeftPath, "sample.zip")));
            c.PressKey(Key.Enter); await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            Check(c.Window.StatusMessage.Contains("Escape cancels", StringComparison.Ordinal), "Pending work explains how to cancel");
            c.PressKey(Key.Escape); await c.SettleAsync();
            Check(gate.WasCancelled, "Escape cancels the archive adapter");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Cancellation retains the original panel");
            Equal("sample.zip", c.Window.Left.Current?.Name, "Cancellation retains the source cursor");
            Equal("unfinished command", c.Window.CommandLine.Editor.Text, "Cancellation keeps the command draft");
            gate.Release.TrySetResult(); await c.SettleAsync();
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "A late result cannot enter the archive");
        }),
        new("archive-report-modes-and-windows-menu", "UC21; explicit report content and discoverable Windows opening", async c =>
        {
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "sample.zip"), ("notes.txt", "Archived text"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("sample.zip"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            Check(viewer.IsArchiveReport && viewer.Title!.Contains("Archive contents", StringComparison.Ordinal), "The report has a clear identity");
            Check(viewer.ReadingStatus.StartsWith("Archive report", StringComparison.Ordinal), "Status identifies report bytes");
            Check(!ViewerProbe.Command(viewer, "viewer.mode.hex").IsEnabled, "Hex cannot present report bytes as archive bytes");
            await c.KeyAsync(Key.D3, target: viewer);
            Equal(FileViewMode.Text, viewer.Mode, "The Hex shortcut preserves the archive report");
            await c.ScreenshotAsync("archive-report", viewer);
            await c.KeyAsync(Key.Escape, target: viewer);
            var menu = c.Window.TitleBar.Children.OfType<Menu>().SingleOrDefault() ??
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c.Window).OfType<Menu>().Single(item => item.Name == "MainMenu");
            var fileMenu = menu.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "File");
            await c.ClickAsync(fileMenu);
            await c.ClickAsync(fileMenu.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "Open with Windows"));
            Equal(Path.Combine(c.LeftPath, "sample.zip"), c.Launcher.Requests.Single().Target, "The menu opens the source archive through Windows");
        }),
        new("archive-extraction-conflict-cancel-and-cache-refresh", "UC21; UC34; extraction safety and refreshed archive contents", async c =>
        {
            var archive = Path.Combine(c.LeftPath, "sample.zip");
            ArchiveProbe.Zip(archive, ("notes.txt", new string('x', 300_000)));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("sample.zip"); await c.KeyAsync(Key.Enter); await c.SelectAsync("notes.txt");
            var destination = Path.Combine(c.RightPath, "notes.txt");
            File.WriteAllText(destination, "Keep existing file");
            await CopyAsync(c); await ChooseConflictAsync(c, "ConflictSkip");
            Equal("Keep existing file", File.ReadAllText(destination), "Skip preserves the destination");
            await CopyAsync(c); await ChooseConflictAsync(c, "ConflictReplace");
            Equal(300_000L, new FileInfo(destination).Length, "Replace extracts the entry");
            File.WriteAllText(destination, "Keep existing file");
            var gate = c.Archives.PauseNextExtraction(c.Window.Left.Current!.Entry.FullPath);
            await CopyAsync(c);
            ArchiveProbe.AcceptReplace(c);
            await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            c.Window.Operations.Cancel(LastJob(c));
            await c.SettleAsync();
            Check(gate.WasCancelled, "Cancellation reaches the decoder during extraction");
            Equal(FileOperationState.Cancelled, LastJob(c).State, "The cancelled extraction has an explicit outcome");
            Equal("Keep existing file", File.ReadAllText(destination), "Cancellation preserves the existing destination");
            Check(!Directory.EnumerateFiles(c.RightPath, ".xcmd-*.tmp").Any(), "Cancellation removes the incomplete temporary file");
            File.Delete(archive); ArchiveProbe.Zip(archive, ("notes.txt", "Refreshed contents"));
            await c.KeyAsync(Key.R, RawInputModifiers.Control); await c.SelectAsync("notes.txt"); await c.KeyAsync(Key.F3);
            Equal("Refreshed contents", c.Window.Viewer!.TextContent.Text, "Reload invalidates the old archive contents");
        })
    ];
}

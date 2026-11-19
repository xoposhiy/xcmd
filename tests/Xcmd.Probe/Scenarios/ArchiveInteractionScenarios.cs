// @tests OpenOtherPanel, RefreshEntries, ResolveConflicts, QueueOperations, SearchResultPanel, ChooseViewMode, BrowseArchives, InspectArchives, ExtractEntries, OpenExternalTool
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
        new("archive-other-panel-both-directions", "UC21; Ctrl+Left/Right opens archive contents in the other panel and preserves source focus", async c =>
        {
            var leftArchive = Path.Combine(c.LeftPath, "sample.zip");
            var rightArchive = Path.Combine(c.RightPath, "other.zip");
            ArchiveProbe.Zip(leftArchive, ("left.txt", "Left archive"));
            ArchiveProbe.Zip(rightArchive, ("right.txt", "Right archive"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("sample.zip");
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            Equal(ArchivePath.Root(leftArchive), c.Window.Right.Session.DirectoryPath, "Ctrl+Right enters the archive in the right panel");
            Check(c.Window.Right.Rows.Any(row => row.Name == "left.txt"), "The right panel shows actual archive entries");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "The left source directory stays unchanged");
            Equal("sample.zip", c.Window.Left.Current?.Name, "The source archive stays current");
            Check(c.Window.ActivePanel == c.Window.Left && c.Window.Left.Table.IsKeyboardFocusWithin, "Focus stays in the left source panel");
            await c.Window.Right.LoadAsync(c.RightPath); await c.SelectAsync("other.zip", c.Window.Right);
            await c.KeyAsync(Key.Left, RawInputModifiers.Control);
            Equal(ArchivePath.Root(rightArchive), c.Window.Left.Session.DirectoryPath, "Ctrl+Left enters the archive in the left panel");
            Check(c.Window.Left.Rows.Any(row => row.Name == "right.txt"), "The left panel shows actual archive entries");
            Check(c.Window.ActivePanel == c.Window.Right && c.Window.Right.Table.IsKeyboardFocusWithin, "Focus stays in the right source panel");
            Equal(0, c.Launcher.Requests.Count, "The shortcut never launches an external archive program");
        }),
        new("archive-other-panel-nested-and-search", "UC21; nested archives and archive search results open their contents in the other panel", async c =>
        {
            var archive = Path.Combine(c.LeftPath, "outer.zip");
            ArchiveProbe.Nested(archive);
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("outer.zip"); await c.KeyAsync(Key.Enter);
            await c.SelectAsync("inner.zip");
            var inner = c.Window.Left.Current!.Entry;
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            Equal(ArchivePath.Root(inner.FullPath), c.Window.Right.Session.DirectoryPath, "A nested archive opens in the other panel");
            Check(c.Window.Right.Rows.Any(row => row.Name == "nested.txt"), "Nested archive contents are readable");
            Equal(ArchivePath.Root(archive), c.Window.Left.Session.DirectoryPath, "The outer archive remains open in the source");
            var result = (await c.FileSystem.InspectAsync(archive, CancellationToken.None))!;
            c.Window.Left.ShowSearchResults(c.LeftPath, [new(result, "outer.zip")]);
            c.Window.Left.FocusCurrent();
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            Equal(ArchivePath.Root(archive), c.Window.Right.Session.DirectoryPath, "An archive result opens its contents");
            Check(c.Window.Left.IsSearchResults, "Source search results remain available");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Focus stays in source results");
        }),
        new("archive-other-panel-error-and-cancel", "UC21; broken archives and cancelled opposite-panel reads preserve both panels", async c =>
        {
            File.WriteAllText(Path.Combine(c.LeftPath, "broken.zip"), "Invalid ZIP data");
            var archive = Path.Combine(c.LeftPath, "sample.zip");
            ArchiveProbe.Zip(archive, ("notes.txt", "Archived text"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("broken.zip");
            await c.KeyAsync(Key.Right, RawInputModifiers.Control);
            Check(!string.IsNullOrWhiteSpace(c.Window.LastError), "A broken archive reports its error");
            Equal(c.RightPath, c.Window.Right.Session.DirectoryPath, "A failed open keeps the previous destination");
            Equal("broken.zip", c.Window.Left.Current?.Name, "A failed open preserves the source cursor");
            await c.SelectAsync("sample.zip");
            var gate = c.Archives.PauseNextRead(ArchivePath.Root(archive));
            c.Window.CommandLine.Editor.Text = "unfinished command";
            c.PressKey(Key.Right, RawInputModifiers.Control); await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            c.PressKey(Key.Escape); await c.SettleAsync();
            Check(gate.WasCancelled, "Escape cancels the destination archive read");
            Equal(c.RightPath, c.Window.Right.Session.DirectoryPath, "Cancellation keeps the destination directory");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Cancellation keeps the source directory");
            Equal("unfinished command", c.Window.CommandLine.Editor.Text, "Cancellation preserves the command draft");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Cancellation keeps source focus");
            gate.Release.TrySetResult(); await c.SettleAsync();
            Equal(c.RightPath, c.Window.Right.Session.DirectoryPath, "A late result cannot replace the destination");
        }),
        new("archive-empty-tar-and-zero-binary-detection", "UC09; UC21; empty archives and binary files keep distinct views", async c =>
        {
            ArchiveProbe.EmptyTar(Path.Combine(c.LeftPath, "empty.tar"));
            ArchiveProbe.EmptyTar(Path.Combine(c.LeftPath, "zeros.bin"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("zeros.bin"); await c.KeyAsync(Key.F3);
            Equal(FileViewMode.Hex, c.Window.Viewer!.Reader.Mode, "Ordinary zero-filled data remains a byte view");
            Check(!c.Window.Viewer.Reader.IsArchiveReport, "Zero-filled data is not automatically presented as an empty archive");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer); await c.SelectAsync("empty.tar"); await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer!.Reader.IsArchiveReport && c.Window.Viewer.Reader.TextContent.Text!.Contains("Files: 0", StringComparison.Ordinal), "An explicit empty TAR still has an archive report");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer); await c.SelectAsync("zeros.bin");
            await c.KeyAsync(Key.PageDown, RawInputModifiers.Control);
            Check(ArchivePath.IsEntry(c.Window.Left.Session.DirectoryPath), "Ctrl+PageDown can explicitly enter an ambiguous empty archive");
            await c.KeyAsync(Key.PageUp, RawInputModifiers.Control); await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer!.Reader.IsArchiveReport, "An explicitly recognized archive keeps report behavior after leaving it");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer); await c.SelectAsync("zeros.bin");
            await c.KeyAsync(Key.PageDown, RawInputModifiers.Control);
            Check(ArchivePath.IsEntry(c.Window.Left.Session.DirectoryPath), "Ctrl+PageDown can explicitly enter an ambiguous empty archive");
            await c.KeyAsync(Key.PageUp, RawInputModifiers.Control); await c.KeyAsync(Key.F3);
            Check(c.Window.Viewer!.Reader.IsArchiveReport, "An explicitly recognized archive keeps report behavior after leaving it");
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
            Check(viewer.Reader.IsArchiveReport && viewer.Title!.Contains("Archive contents", StringComparison.Ordinal), "The report has a clear identity");
            Check(viewer.Reader.ReadingStatus.StartsWith("Archive report", StringComparison.Ordinal), "Status identifies report bytes");
            Check(!ViewerProbe.Command(viewer, "viewer.mode.hex").IsEnabled, "Hex cannot present report bytes as archive bytes");
            await c.KeyAsync(Key.D3, target: viewer);
            Equal(FileViewMode.Text, viewer.Reader.Mode, "The Hex shortcut preserves the archive report");
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
            Equal("Refreshed contents", c.Window.Viewer!.Reader.TextContent.Text, "Reload invalidates the old archive contents");
        })
    ];
}

// @tests OpenViewers, BrowseArchives, InspectArchives, ExtractEntries, OpenExternalTool, RunCommand, ReusePaths, RestoreWorkspace, ArchiveAccess
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;

namespace Xcmd.Probe.Scenarios;

public static class ArchiveScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("archive-enter-browse-view-extract-return", "UC21; Enter, F3, F5 and parent navigation through actual controls", async c =>
        {
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "sample.zip"), ("folder/notes.txt", "Archived text"), ("empty/", ""), ("root.txt", "Root text"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("sample.zip");
            await c.KeyAsync(Key.Enter);
            Check(ArchivePath.IsEntry(c.Window.Left.Session.DirectoryPath), "Enter opens an archive in the panel");
            Equal("..", c.Window.Left.Current?.Name, "Archive entry starts at parent");
            Check(c.Window.Left.Rows.Any(row => row.Name == "empty" && row.Entry.IsDirectory), "Empty folders are listed");
            await c.SelectAsync("folder"); await c.KeyAsync(Key.Enter); await c.SelectAsync("notes.txt");
            await c.KeyAsync(Key.F3);
            Equal("Archived text", c.Window.Viewer!.TextContent.Text, "F3 reads the actual entry contents");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer);
            await CopyAsync(c);
            Equal("Archived text", File.ReadAllText(Path.Combine(c.RightPath, "notes.txt")), "F5 extracts the entry to the opposite panel");
            await c.KeyAsync(Key.PageUp, RawInputModifiers.Control);
            Equal("folder", c.Window.Left.Current?.Name, "Returning to archive root restores the folder cursor");
            await c.SelectAsync("folder"); await CopyAsync(c);
            Equal("Archived text", File.ReadAllText(Path.Combine(c.RightPath, "folder", "notes.txt")), "Folder extraction keeps paths");
            await c.ScreenshotAsync("archive-panel");
            await c.KeyAsync(Key.PageUp, RawInputModifiers.Control);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Ctrl+PageUp leaves the archive root");
            Equal("sample.zip", c.Window.Left.Current?.Name, "Leaving selects the source archive");
            Equal(0, c.Launcher.Requests.Count, "Browsing never starts a program");
        }),
        new("archive-summary-and-shell-command-sequence", "UC21; F3 archive information and Ctrl+Enter then Enter bypass", async c =>
        {
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "sample archive.zip"), ("folder/notes.txt", "Archived text"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("sample archive.zip"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            Check(viewer.TextContent.Text!.Contains("Format: Zip", StringComparison.Ordinal), "F3 shows archive information instead of compressed bytes");
            Check(viewer.TextContent.Text.Contains("folder/notes.txt", StringComparison.Ordinal), "F3 lists entry paths");
            await c.ScreenshotAsync("archive-summary", viewer);
            await c.KeyAsync(Key.Escape, target: viewer);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control);
            Check(c.Window.CommandLine.HasKeyboardFocus, "Ctrl+Enter focuses the command line");
            await c.KeyAsync(Key.Enter);
            Equal("sample archive.zip", c.Launcher.Requests.Single().Target, "The second Enter sends the archive to Windows Shell");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Shell launch does not enter the archive");
        }),
        new("archive-office-arbitrary-extension-and-nested", "UC21; Ctrl+PageDown content detection and nested archives", async c =>
        {
            foreach (var name in new[] { "sample.docx", "sample.xlsx", "sample.unknown" })
            {
                ArchiveProbe.Zip(Path.Combine(c.LeftPath, name), ("content/data.xml", "<data />"));
                await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync(name);
                await c.KeyAsync(Key.PageDown, RawInputModifiers.Control);
                Check(c.Window.Left.Rows.Any(row => row.Name == "content"), "Ctrl+PageDown detects ZIP data regardless of extension");
                await c.KeyAsync(Key.Back);
            }
            await c.SelectAsync("sample.docx"); await c.KeyAsync(Key.Enter);
            Equal(Path.Combine(c.LeftPath, "sample.docx"), c.Launcher.Requests.Single().Target, "Ordinary Enter keeps the Office association");
            ArchiveProbe.Nested(Path.Combine(c.LeftPath, "nested.zip"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("nested.zip"); await c.KeyAsync(Key.Enter);
            await c.SelectAsync("inner.zip"); await c.KeyAsync(Key.Enter); await c.SelectAsync("nested.txt"); await c.KeyAsync(Key.F3);
            Equal("Nested archive text", c.Window.Viewer!.TextContent.Text, "Nested archive files can be viewed");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer); await c.KeyAsync(Key.Back); await c.KeyAsync(Key.Back);
            Equal("nested.zip", c.Window.Left.Current?.Name, "Leaving nested archives restores the original file");
        }),
        new("archive-formats-and-self-extracting-executable", "UC21; real TAR, TAR.GZ, RAR4, RAR5, 7z and 7z SFX samples", async c =>
        {
            ArchiveProbe.Tar(Path.Combine(c.LeftPath, "sample.tar"), false);
            ArchiveProbe.Tar(Path.Combine(c.LeftPath, "sample.tar.gz"), true);
            ArchiveProbe.Gzip(Path.Combine(c.LeftPath, "sample.txt.gz"));
            ArchiveProbe.CompressedSamples(c);
            foreach (var name in new[] { "Rar4.rar", "Rar5.rar", "7Zip.LZMA2.7z", "7Zip.LZMA2.exe" }) ArchiveProbe.Sample(c, name);
            foreach (var name in new[] { "sample.tar", "sample.tar.gz", "sample.txt.gz", "sample.txt.bz2", "sample.txt.xz", "sample.tar.bz2", "sample.tar.xz", "Rar4.rar", "Rar5.rar", "7Zip.LZMA2.7z", "7Zip.LZMA2.exe" })
            {
                await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync(name);
                await c.KeyAsync(name.EndsWith(".exe", StringComparison.Ordinal) ? Key.PageDown : Key.Enter,
                    name.EndsWith(".exe", StringComparison.Ordinal) ? RawInputModifiers.Control : RawInputModifiers.None);
                Check(ArchivePath.IsEntry(c.Window.Left.Session.DirectoryPath), "The real format opens: " + name + "; " + c.Window.LastError);
                Check(c.Window.Left.Rows.Count > 1, "The archive has real entries: " + name);
                await ArchiveProbe.ViewFirstFileAsync(c);
                Check(c.Window.Viewer!.Error == null && c.Window.Viewer.FileLength > 0, "An entry is really decompressed: " + name + "; " + c.Window.Viewer.Error);
                await c.KeyAsync(Key.Escape, target: c.Window.Viewer);
            }
            Equal(0, c.Launcher.Requests.Count, "The SFX sample is inspected without execution");
        }),
        new("archive-errors-readonly-and-session", "UC21; unsupported files, unsafe paths, read-only operations and workspace restore", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.PageDown, RawInputModifiers.Control);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "An unsupported file preserves the panel");
            Check(c.Window.LastError != null, "An unsupported file has a visible error");
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "unsafe.zip"), ("../outside.txt", "Unsafe"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("unsafe.zip"); await c.KeyAsync(Key.Enter);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Unsafe entry paths are rejected before navigation");
            Check(!File.Exists(Path.Combine(c.LeftPath, "outside.txt")), "Unsafe archive entries are never extracted");
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "safe.zip"), ("notes.txt", "Keep me"));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("safe.zip"); await c.KeyAsync(Key.Enter); await c.SelectAsync("notes.txt");
            await c.KeyAsync(Key.Delete); Check(c.Window.OperationDialog == null && c.Window.LastError != null, "Delete reports that archives are read-only");
            await c.KeyAsync(Key.F6); Check(c.Window.OperationDialog == null && c.Window.LastError != null, "Move cannot remove archive entries");
            await c.KeyAsync(Key.F6, RawInputModifiers.Shift); Check(c.Window.Left.EditingRow == null && c.Window.LastError != null, "Rename cannot edit archive entries");
            var state = c.Window.CaptureWorkspace(); state.Validate();
            await c.Window.Left.LoadAsync(c.LeftPath); await c.Window.RestoreWorkspaceAsync(state); await c.SettleAsync();
            Equal("notes.txt", c.Window.Left.Current?.Name, "Session restore reopens the archive and its cursor");
        })
    ];
}

// @tests DuplicateEntry, ClipboardTransfer, ResolveConflicts, RunCommand, ReusePaths
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Xcmd.Core;
using Xcmd.Desktop.Features.FileOperations;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class ClipboardScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("clipboard-copy-files-and-folder-to-panel", "UC04; clipboard files paste into the displayed folder", async c =>
        {
            var panel = c.Window.Left;
            await panel.RestoreStateAsync(panel.CaptureState() with { Cursor = "folder2", Selected = ["alpha.txt", "folder1", "отчёт 日本語.txt"], HasMarks = true });
            panel.FocusCurrent();
            await c.KeyAsync(Key.C, RawInputModifiers.Control);
            var clipboard = c.Window.Clipboard!;
            var files = await clipboard.TryGetFilesAsync();
            Equal(3, files!.Length, "Ctrl+C exports marked files and folders rather than the unmarked cursor");
            Equal(1u, BitConverter.ToUInt32((await clipboard.TryGetValueAsync(PanelClipboardController.PreferredDropEffect))!), "Windows copy effect");
            Directory.CreateDirectory(Path.Combine(c.RightPath, "highlighted"));
            await c.Window.Right.LoadAsync(c.RightPath);
            await c.SelectAsync("highlighted", c.Window.Right);
            await c.KeyAsync(Key.V, RawInputModifiers.Control);
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Paste uses the displayed directory");
            Equal("Nested file", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "inside.txt")), "Paste copies the directory tree");
            Equal("Unicode file contents", File.ReadAllText(Path.Combine(c.RightPath, "отчёт 日本語.txt")), "Unicode clipboard path survives");
            Check(!File.Exists(Path.Combine(c.RightPath, "highlighted", "alpha.txt")), "The highlighted folder is not the paste destination");
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Copy retains the source");
            Equal("highlighted", c.Window.Right.Current?.Name, "Paste keeps the cursor");
            Check(c.Window.Right.Table.IsKeyboardFocusWithin, "Paste keeps panel focus");
            Equal(3, (await clipboard.TryGetFilesAsync())!.Length, "Copy remains available for another paste");
        }),
        new("clipboard-cut-move-and-dimming", "UC05; cut keeps files until paste and clears after moving", async c =>
        {
            await c.Window.Right.LoadAsync(c.LeftPath);
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.X, RawInputModifiers.Control);
            Check(Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "Cut does not move or delete files");
            Check(c.Window.Left.Current!.IsCut && c.Window.Right.Rows.Single(row => row.Name == "folder1").IsCut, "Both panels show the cut state");
            await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Check(c.Window.Left.Current!.IsCut, "Reload preserves cut appearance");
            await c.ScreenshotAsync("clipboard-cut");
            await c.Window.Right.LoadAsync(c.RightPath);
            await c.SelectAsync("destination.txt", c.Window.Right);
            await c.KeyAsync(Key.V, RawInputModifiers.Control);
            Check(!Directory.Exists(Path.Combine(c.LeftPath, "folder1")), "Paste moves the source tree");
            Equal("Nested file", File.ReadAllText(Path.Combine(c.RightPath, "folder1", "inside.txt")), "Move retains tree contents");
            Equal(0, (await c.Window.Clipboard!.TryGetFilesAsync())?.Length ?? 0, "A completed cut cannot be pasted again");
            Check(c.Window.Left.Rows.All(row => !row.IsCut) && c.Window.Right.Rows.All(row => !row.IsCut), "Move clears dimming");
        }),
        new("clipboard-external-cut-conflict-and-retry", "UC05; Explorer format cut retains skipped sources", async c =>
        {
            var clipboard = c.Window.Clipboard!;
            var data = new DataTransfer();
            foreach (var name in new[] { "alpha.txt", "file2.log" })
                data.Add(DataTransferItem.CreateFile((await c.Window.StorageProvider.TryGetFileFromPathAsync(new Uri(Path.Combine(c.LeftPath, name))))!));
            data.Add(DataTransferItem.Create(DataFormat.CreateBytesPlatformFormat("Preferred DropEffect"), new byte[] { 2, 0, 0, 0 }));
            await clipboard.SetDataAsync(data);
            File.WriteAllText(Path.Combine(c.RightPath, "alpha.txt"), "existing");
            await c.KeyAsync(Key.Tab); await c.KeyAsync(Key.V, RawInputModifiers.Control);
            await ChooseConflictAsync(c, "ConflictSkip");
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Skipped source stays available");
            Check(!File.Exists(Path.Combine(c.LeftPath, "file2.log")), "Other source moves");
            var remaining = await clipboard.TryGetFilesAsync();
            Equal(Path.Combine(c.LeftPath, "alpha.txt"), remaining!.Single().TryGetLocalPath(), "Only the skipped item remains on the cut clipboard");
            await c.KeyAsync(Key.V, RawInputModifiers.Control); await ChooseConflictAsync(c, "ConflictReplace");
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Retry can replace the conflict");
            Check(!File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "Retry removes only the successfully moved source");
        }),
        new("clipboard-same-folder-copy-and-cut", "UC04; safe duplicate naming and same-folder cut", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.C, RawInputModifiers.Control);
            await c.KeyAsync(Key.V, RawInputModifiers.Control); await c.KeyAsync(Key.V, RawInputModifiers.Control);
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha (copy).txt")), "First same-folder copy has a new name");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha (copy 2).txt")), "Repeated paste chooses another free name");
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.X, RawInputModifiers.Control);
            var jobs = c.Window.Operations.Jobs.Count;
            await c.KeyAsync(Key.V, RawInputModifiers.Control);
            Equal(jobs, c.Window.Operations.Jobs.Count, "Same-folder cut creates no destructive move");
            Check(File.Exists(Path.Combine(c.LeftPath, "alpha.txt")), "The original remains");
            await c.KeyAsync(Key.C, RawInputModifiers.Control);
            Check(c.Window.Left.Rows.All(row => !row.IsCut), "Copy replaces the cut state");
        }),
        new("clipboard-path-and-selected-command-argument", "UC02; direct path export and selected insertion feedback", async c =>
        {
            await c.SelectAsync("отчёт 日本語.txt");
            var path = Path.Combine(c.LeftPath, "отчёт 日本語.txt");
            await MainMenuProbe.InvokeFileCommandAsync(c, "Copy full path");
            Equal(path, await c.Window.Clipboard!.TryGetTextAsync(), "The menu copies the unquoted complete path");
            var archive = Path.Combine(c.RightPath, "paths.zip");
            ArchiveProbe.Zip(archive, ("member.txt", "Member"));
            await c.Window.Right.LoadAsync(ArchivePath.Root(archive)); c.Window.Activate(c.Window.Right); await c.SelectAsync("member.txt", c.Window.Right);
            await MainMenuProbe.InvokeFileCommandAsync(c, "Copy full path");
            Equal(Path.Combine(archive + "!", "member.txt"), await c.Window.Clipboard!.TryGetTextAsync(), "Archive members copy the displayed archive path");
            c.Window.Activate(c.Window.Left); await c.SelectAsync("отчёт 日本語.txt");
            Equal("", c.Window.CommandLine.Editor.Text, "Path copying keeps the command draft");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Path copying keeps file focus");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control);
            Equal("\"отчёт 日本語.txt\"", c.Window.CommandLine.Editor.SelectedText, "Ctrl+Enter selects exactly the inserted argument");
            await c.KeyAsync(Key.C, RawInputModifiers.Control);
            Equal("\"отчёт 日本語.txt\"", await c.Window.Clipboard!.TryGetTextAsync(), "Ctrl+C in command input copies the selected name");
            await c.KeyAsync(Key.Escape); await c.KeyAsync(Key.Enter, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal(CommandLine.FormatArgument(path), c.Window.CommandLine.Editor.SelectedText, "Ctrl+Shift+Enter selects the full quoted path");
            await c.KeyAsync(Key.C, RawInputModifiers.Control);
            Equal(CommandLine.FormatArgument(path), await c.Window.Clipboard!.TryGetTextAsync(), "Immediate Ctrl+C copies the inserted path");
            await c.KeyAsync(Key.Escape); await c.TextAsync("tool ");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control);
            Equal("tool \"отчёт 日本語.txt\" ", c.Window.CommandLine.Editor.Text, "Insertion preserves the existing draft");
            Equal("\"отчёт 日本語.txt\"", c.Window.CommandLine.Editor.SelectedText, "Existing draft is excluded from the inserted selection");
            await c.Window.Clipboard!.SetTextAsync("replacement");
            await c.KeyAsync(Key.V, RawInputModifiers.Control);
            Equal("tool replacement ", c.Window.CommandLine.Editor.Text, "Ctrl+V in command input remains text editing");
            Equal(0, c.Window.Operations.Jobs.Count, "Command editing never queues file operations");
        }),
        new("clipboard-inflight-target-and-new-clipboard", "UC04; captured destination and newer clipboard survive background work", async c =>
        {
            c.FileSystem.ForceCopyMove = true;
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.X, RawInputModifiers.Control);
            var gate = c.FileSystem.PauseNextCopy(Path.Combine(c.LeftPath, "alpha.txt"));
            await c.KeyAsync(Key.Tab);
            c.PressKey(Key.V, RawInputModifiers.Control);
            await WaitUntilAsync(() => gate.Entered.Task.IsCompleted);
            await c.Window.Right.LoadAsync(Path.Combine(c.LeftPath, "folder2"));
            c.Window.Right.FocusCurrent(); c.PressKey(Key.V, RawInputModifiers.Control);
            Equal(1, c.Window.Operations.Jobs.Count, "Repeated paste does not queue the same pending cut");
            await c.Window.Clipboard!.SetTextAsync("newer user clipboard");
            gate.Release.SetResult(); await c.SettleAsync();
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Move keeps its captured destination");
            Check(!File.Exists(Path.Combine(c.LeftPath, "folder2", "alpha.txt")), "Navigation cannot redirect pending work");
            Equal("newer user clipboard", await c.Window.Clipboard!.TryGetTextAsync(), "Move completion preserves newer clipboard data");
        })
    ];
}

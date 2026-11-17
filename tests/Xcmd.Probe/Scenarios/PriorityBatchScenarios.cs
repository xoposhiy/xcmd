// @tests ReadablePanels, MarkEntries, MeasureFolders, CreateFile, TransferEntries, QueueOperations, RecoverOperations, FollowLog, CommandHistory, CompleteCommand, ShellActions, WindowsFileActions, ExternalLaunch
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
        new("priority-new-file", "UC07; Shift+F4 creates without overwrite and edits the literal path", async c =>
        {
            await c.KeyAsync(Key.F4, RawInputModifiers.Shift);
            Equal("New file", c.Window.ActiveDialog?.Title, "Shift+F4 opens the creation dialog");
            var dialog = c.Window.ActiveDialog!;
            await c.TextAsync("alpha.txt", dialog); await c.KeyAsync(Key.Enter, target: dialog);
            Check(!string.IsNullOrWhiteSpace(dialog.Error.Text), "Existing file is rejected");
            Equal("abc", File.ReadAllText(Path.Combine(c.LeftPath, "alpha.txt")), "Existing contents survive");
            dialog.Editor.Text = "new отчёт.txt";
            await c.KeyAsync(Key.Enter, target: dialog);
            Check(c.Window.ActiveDialog == null, "Successful creation closes the dialog");
            Equal(0L, new FileInfo(Path.Combine(c.LeftPath, "new отчёт.txt")).Length, "A real empty file is created");
            Equal("new отчёт.txt", c.Window.Left.Current?.Name, "New file becomes current");
            Check(c.Launcher.Requests.Single().Arguments.Contains("new отчёт.txt", StringComparison.Ordinal), "Editor receives the literal Unicode path");
            await c.KeyAsync(Key.F4, RawInputModifiers.Shift); await c.KeyAsync(Key.Escape, target: c.Window.ActiveDialog);
            Equal(1, c.Launcher.Requests.Count, "Cancel never launches the editor");
        }),
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
        new("priority-new-file-launch-failure", "UC07; an editor failure leaves the created file selected and available for retry", async c =>
        {
            c.Launcher.Failure = new IOException("Controlled editor failure");
            await c.KeyAsync(Key.F4, RawInputModifiers.Shift);
            await c.TextAsync("created.txt", c.Window.ActiveDialog);
            await c.KeyAsync(Key.Enter, target: c.Window.ActiveDialog);
            Check(c.Window.ActiveDialog == null, "Creation succeeds independently of editor startup");
            Check(File.Exists(Path.Combine(c.LeftPath, "created.txt")), "The new file remains available");
            Equal("created.txt", c.Window.Left.Current?.Name, "The new file remains selected");
            Check(c.Window.LastError?.Contains("Controlled editor failure", StringComparison.Ordinal) == true, "Editor failure remains visible");
            c.Launcher.Failure = null;
            await c.KeyAsync(Key.F4);
            Equal(2, c.Launcher.Requests.Count, "F4 retries opening the same file");
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
        new("priority-selected-sizes", "PANEL-01; selected files and measured folders total without traversal", async c =>
        {
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            Check(Summary(c).Contains("incomplete", StringComparison.Ordinal), "Unknown folder sizes are explicit");
            c.Window.Left.SetDirectorySize(new("folder1", new(42, 1, 0, 0), DateTimeOffset.UtcNow));
            Check(Summary(c).Contains("42 bytes", StringComparison.Ordinal), "Calculated folder bytes contribute immediately");
            c.Window.Left.SetDirectorySize(new("folder2", new(0, 0, 0, 0), DateTimeOffset.UtcNow));
            c.Window.Left.SetDirectorySize(new("folder1", new(42, 1, 1, 0), DateTimeOffset.UtcNow));
            Check(!Summary(c).Contains("≥", StringComparison.Ordinal), "Excluded links contribute zero without making the total incomplete");
            c.Window.Left.SetDirectorySize(new("folder1", new(42, 1, 0, 1), DateTimeOffset.UtcNow));
            Check(Summary(c).Contains("≥ 42", StringComparison.Ordinal), "Partial measurements remain lower bounds");
            Check(!c.Window.IsCalculatingDirectorySizes, "Selection never starts a scan");
        }),
        new("priority-command-completion", "CMD-01; Ctrl+Space offers quoted path choices without executing", async c =>
        {
            File.WriteAllText(Path.Combine(c.LeftPath, "report one.txt"), "one");
            File.WriteAllText(Path.Combine(c.LeftPath, "report two.txt"), "two");
            await c.KeyAsync(Key.L, RawInputModifiers.Control); await c.TextAsync("tool rep");
            await c.KeyAsync(Key.Space, RawInputModifiers.Control);
            Check(c.Window.CommandLine.Completions.IsOpen, "Completion choices open");
            Equal(2, c.Window.CommandLine.Completions.Items.ItemCount, "Both matching files are offered");
            await c.ScreenshotAsync("command-completion");
            await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Enter);
            Equal("tool \"report two.txt\"", c.Window.CommandLine.Editor.Text, "Selected path is inserted with quotes");
            Equal(0, c.Launcher.Requests.Count, "Accepting completion does not execute");
            c.Window.CommandLine.Editor.Text = "tool rep"; c.Window.CommandLine.Editor.CaretIndex = 8;
            await c.KeyAsync(Key.Space, RawInputModifiers.Control); await c.KeyAsync(Key.Escape);
            Equal("tool rep", c.Window.CommandLine.Editor.Text, "Escape preserves the draft");
            await c.KeyAsync(Key.Tab); Equal(c.Window.Right, c.Window.ActivePanel, "Tab still switches panels");
            var variable = await CommandCompletions.FindAsync(CommandToken.At("%PA", 3), c.LeftPath, CancellationToken.None);
            Check(variable.Any(item => item.Value.Equals("%PATH%", StringComparison.OrdinalIgnoreCase)), "Environment names are completed");
        }),
        new("priority-command-prefix-history", "CMD-02; history filters by the original prefix and restores the draft", async c =>
        {
            c.Window.CommandLine.AddHistory("git status"); c.Window.CommandLine.AddHistory("dotnet build"); c.Window.CommandLine.AddHistory("git diff");
            await c.KeyAsync(Key.L, RawInputModifiers.Control); await c.TextAsync("git");
            await c.KeyAsync(Key.Up, RawInputModifiers.Alt); Equal("git diff", c.Window.CommandLine.Editor.Text, "Most recent match");
            await c.KeyAsync(Key.Up, RawInputModifiers.Alt); Equal("git status", c.Window.CommandLine.Editor.Text, "Unrelated command is skipped");
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt); await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            Equal("git", c.Window.CommandLine.Editor.Text, "Newer returns to the original draft");
            Equal(0, c.Launcher.Requests.Count, "Browsing never launches a command");
        }),
        new("priority-command-directory-quotes", "CMD-01; a completed folder with spaces reaches the real Windows launcher literally", async c =>
        {
            Directory.CreateDirectory(Path.Combine(c.LeftPath, "report folder"));
            var fixture = CommandProbe.Prepare(c);
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false);
            var command = CommandLine.FormatArgument(fixture.Executable) + " " + CommandLine.FormatArgument(fixture.Report) + " rep";
            await c.KeyAsync(Key.L, RawInputModifiers.Control); await c.TextAsync(command);
            await c.KeyAsync(Key.Space, RawInputModifiers.Control); await c.KeyAsync(Key.Enter);
            Equal(0, c.Launcher.Requests.Count, "Accepting the folder does not run the command");
            await c.KeyAsync(Key.Enter);
            var report = await CommandProbe.ReadReportAsync(fixture.Report);
            Equal("report folder\\", report.Arguments.Single(), "Windows receives the complete directory including its trailing separator");
        }),
        new("priority-viewer-follow", "VIEW-01; follow appended data, pause, resume and survive truncation", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3); var viewer = c.Window.Viewer!;
            await c.KeyAsync(Key.F, RawInputModifiers.Control | RawInputModifiers.Shift, viewer);
            Check(viewer.IsFollowing, "Follow mode is enabled");
            var path = Path.Combine(c.LeftPath, "alpha.txt"); File.AppendAllText(path, "\nappended");
            await WaitAsync(c, () => viewer.TextContent.Text!.Contains("appended", StringComparison.Ordinal));
            await c.KeyAsync(Key.Home, RawInputModifiers.Control, viewer);
            Check(!viewer.IsFollowing, "Reading earlier data pauses following");
            File.AppendAllText(path, "\nmore");
            await c.KeyAsync(Key.F, RawInputModifiers.Control | RawInputModifiers.Shift, viewer);
            Check(viewer.TextContent.Text!.Contains("more", StringComparison.Ordinal), "Explicit resume catches up");
            File.WriteAllText(path, "reset");
            await WaitAsync(c, () => viewer.TextContent.Text == "reset");
            Equal(5L, viewer.FileLength, "Truncation resets the loaded length");
            await c.ScreenshotAsync("viewer-follow", viewer);
        }),
        new("priority-shell-group", "PANEL-04; marked item targets the group; an unmarked item preserves marks", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var marks = c.Window.Left.SelectedNames();
            await ShellInputProbe.RightClickAsync(c, "alpha.txt");
            Equal(2, c.Shell.MenuGroups.Last().Length, "Marked group is passed to Windows");
            Check(c.Shell.MenuGroups.Last().Contains(Path.Combine(c.LeftPath, "file2.log")), "Second mark is included");
            await ShellInputProbe.RightClickAsync(c, "zeta.md");
            Equal(Path.Combine(c.LeftPath, "zeta.md"), c.Shell.MenuGroups.Last().Single(), "Unmarked item receives a single-item menu");
            Check(marks.SequenceEqual(c.Window.Left.SelectedNames()), "Right-click never removes existing marks");
            await ShellInputProbe.RightClickAsync(c, "alpha.txt");
            await c.KeyAsync(Key.F10, RawInputModifiers.Shift);
            Equal(2, c.Shell.MenuGroups.Last().Length, "The keyboard menu also targets the marked group");
            var native = WindowsShellInteraction.InspectMenu([Path.Combine(c.LeftPath, "alpha.txt"), Path.Combine(c.LeftPath, "file2.log")]);
            Check(native.Verbs.Contains("copy", StringComparer.OrdinalIgnoreCase), "Real Windows group exposes copy without displaying a menu");
        }),
        new("priority-shell-mixed-folders", "PANEL-04; mixed-folder results explain the limitation and keep their marks", async c =>
        {
            var first = c.Window.Left.Rows.Single(row => row.Name == "alpha.txt").Entry;
            var second = c.Window.Right.Rows.Single(row => row.Name == "destination.txt").Entry;
            c.Window.Left.ShowSearchResults(c.FixturePath, [new(first, "left/alpha.txt"), new(second, "right/destination.txt")]);
            c.Window.Left.FocusCurrent(); await c.KeyAsync(Key.A, RawInputModifiers.Control);
            await c.KeyAsync(Key.F10, RawInputModifiers.Shift);
            Equal(0, c.Shell.MenuGroups.Count, "Unsupported groups never reach Windows");
            Check(c.Window.LastError?.Contains("one folder", StringComparison.Ordinal) == true, "The limitation explains the next action");
            Equal(2, c.Window.Left.SelectedNames().Length, "The group stays marked");
            await c.KeyAsync(Key.Escape);
            await ShellInputProbe.RightClickAsync(c, "alpha.txt");
            Equal(first.FullPath, c.Shell.MenuGroups.Last().Single(), "The file's ordinary folder still supports its menu");
        })
    ];
}

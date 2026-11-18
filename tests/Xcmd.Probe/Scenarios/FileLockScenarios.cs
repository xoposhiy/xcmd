// @tests RenameEntry, TransferEntries, DeleteEntries, RecoverOperations, ReportControl, WindowsFileActions
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class FileLockScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("file-lock-console-host", "Ending both command processors in a headless pseudoconsole retries the rename", async c =>
        {
            var folder = Path.Combine(c.LeftPath, "folder1");
            await using var terminal = await ConsoleLockFixture.StartAsync(folder);
            var users = await WindowsFileUsers.FindAsync([folder], default);
            var shells = users.Where(user => user.ExecutableName == "cmd.exe").ToArray();
            Equal(2, shells.Length, "Both nested command processors hold the directory");
            Check(shells.All(user => user.WindowKey == null), "A headless pseudoconsole has no application window");
            await c.BeginRenameAsync("folder1"); await c.TextAsync("renamed-folder"); await c.KeyAsync(Key.Enter);
            var dialog = (FileErrorDialog)c.Window.OperationDialog!;
            Check(!dialog.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "FileUserWindow"), "The dialog offers no link for a headless pseudoconsole");
            FileLockProbe.CheckCompact(dialog, 2);
            Check(shells.All(shell => LockingProcessFixture.ErrorText(c).Contains("PID " + shell.ProcessId, StringComparison.Ordinal)), "Every shell remains in the plain process summary");
            await c.ScreenshotAsync("locked-headless-console", dialog);
            Check(!dialog.GetVisualDescendants().OfType<RadioButton>().Any(), "Process actions need no radio selection");
            var endButtons = dialog.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "FileUserEnd").ToArray();
            Equal(2, endButtons.Length, "Each process has its own Kill button");
            await c.ClickAsync(endButtons[0], target: dialog);
            Check(dialog.IsVisible && Directory.Exists(folder), "The other command processor keeps the folder locked");
            await c.ClickAsync(endButtons[1], target: dialog);
            await terminal.WaitForExitAsync();
            await FileOperationProbe.WaitUntilAsync(() => Directory.Exists(Path.Combine(c.LeftPath, "renamed-folder")));
            await c.SettleAsync();
            Check(c.Window.OperationDialog == null, "Ending the listed shells releases the folder and dismisses the error");
        }),
        new("file-lock-shared-window", "Processes with the same window share a row and an application link", async c =>
        {
            var path = Path.Combine(c.LeftPath, "folder1");
            FileUser first = new(101, "Command Prompt", path, DateTime.UnixEpoch) { ExecutableName = "cmd.exe", WindowKey = "terminal" };
            var second = first with { ProcessId = 102 };
            var actions = new RecordingFileUserActions { TerminateProcesses = false };
            var view = new FileUsersView([first, second]) { Actions = actions };
            var window = new Window { Content = view, Width = 620, Height = 180 };
            window.Show(c.Window);
            try
            {
                await c.SettleAsync();
                var controls = view.GetVisualDescendants().ToArray();
                var summary = controls.OfType<TextBlock>().Single(control => control.Name == "FileUserProcesses");
                Equal("cmd.exe (PID 101), cmd.exe (PID 102)", summary.Text, "The shared row lists both processes");
                var end = controls.OfType<Button>().Single(button => button.Name == "FileUserEnd");
                var link = controls.OfType<Button>().Single(button => button.Name == "FileUserWindow");
                await c.ClickAsync(link, target: window);
                Equal(("window", first), actions.Calls.Single(), "The application link opens the shared window");
                await c.ClickAsync(end, target: window);
                Check(actions.Calls.Skip(1).SequenceEqual([("end", first), ("end", second)]), "The shared Kill button ends both listed processes");
            }
            finally { window.Close(); }
        }),
        new("file-lock-recycle-directory", "Recycling a locked folder opens the same process dialog and keeps its contents", async c =>
        {
            var folder = Path.Combine(c.LeftPath, "folder1");
            await using var holder = await LockingProcessFixture.StartAsync(c, folder, directory: true);
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.F8);
            await FileOperationProbe.ConfirmDeleteAsync(c);
            Check(c.Window.OperationDialog is FileErrorDialog, "A recycling failure opens an error dialog: " + c.Window.StatusMessage);
            Check(LockingProcessFixture.ErrorText(c).Contains("PID " + holder.Id, StringComparison.Ordinal), "Recycle errors identify the process using the folder: " + LockingProcessFixture.ErrorText(c));
            Check(File.Exists(Path.Combine(folder, "inside.txt")), "The locked tree is kept intact");
            await PriorityBatchProbe.ChooseErrorAsync(c, "FileErrorCancel");
        }),
        new("file-lock-rename-file", "Failed inline rename identifies the external process and can be retried after release", async c =>
        {
            var source = Path.Combine(c.LeftPath, "alpha.txt");
            await using var holder = await LockingProcessFixture.StartAsync(c, source);
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("renamed.txt");
            await c.KeyAsync(Key.Enter);
            Check(LockingProcessFixture.ErrorText(c).Contains($"PID {holder.Id}", StringComparison.Ordinal), "The rename dialog identifies the process holding the file");
            Check(File.Exists(source) && c.Window.Left.EditingRow != null, "The locked file and rename draft remain intact");
            await c.ScreenshotAsync("locked-file-rename", c.Window.OperationDialog);
            await FileLockProbe.MeasureReleaseAsync(c, holder, () => c.Window.Left.Rows.Any(row => row.Name == "renamed.txt"));
            Check(File.Exists(Path.Combine(c.LeftPath, "renamed.txt")), "Rename succeeds after the process releases the file");
        }),
        new("file-lock-rename-running-executable", "Access denied on folder rename identifies an executable running inside it", async c =>
        {
            var folder = Path.Combine(c.LeftPath, "folder1");
            await using var executable = await LockingProcessFixture.StartExecutableInsideAsync(c, folder);
            await c.BeginRenameAsync("folder1");
            await c.TextAsync("renamed-folder");
            await c.KeyAsync(Key.Enter);
            var text = LockingProcessFixture.ErrorText(c);
            Check(text.Contains("Xcmd.LaunchProbe.exe", StringComparison.Ordinal) && text.Contains($"PID {executable.Id}", StringComparison.Ordinal),
                "The dialog identifies the program whose executable is inside the folder: " + text);
            Check(text.Contains("may be blocking the rename", StringComparison.Ordinal), "The dialog presents the detected program as a possible cause");
            await executable.ReleaseAsync();
            await FileOperationProbe.WaitUntilAsync(() => c.Window.OperationDialog == null);
            Check(File.Exists(Path.Combine(c.LeftPath, "renamed-folder", "tools", "Xcmd.LaunchProbe.exe")),
                "Retry moves the executable with the folder");
        }),
        new("file-lock-rename-failed-retry", "A failed automatic retry keeps the error dialog and explains the latest failure", async c =>
        {
            var source = Path.Combine(c.LeftPath, "alpha.txt");
            await using var holder = await LockingProcessFixture.StartAsync(c, source);
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("occupied.txt");
            await c.KeyAsync(Key.Enter);
            var dialog = (FileErrorDialog)c.Window.OperationDialog!;
            Check(dialog.Users != null, "The first error identifies the process");
            c.AddFile("occupied.txt", "Keep the existing file");
            await holder.ReleaseAsync();
            await FileOperationProbe.WaitUntilAsync(() => dialog.Users == null);
            Equal(dialog, c.Window.OperationDialog, "Retry keeps the same window when the name is now taken");
            var text = LockingProcessFixture.ErrorText(c);
            Check(text.Contains("Rename still failed", StringComparison.Ordinal) && text.Contains("occupied.txt", StringComparison.Ordinal),
                "The dialog shows the failed retry and the requested name");
            await c.KeyAsync(Key.Escape, target: dialog);
            Equal("occupied.txt", c.Window.Left.EditingRow?.DraftName, "Cancel returns to the unchanged rename draft");
            Check(File.Exists(source) && File.ReadAllText(Path.Combine(c.LeftPath, "occupied.txt")) == "Keep the existing file",
                "Both files remain intact");
        }),
        new("file-lock-directory-and-child", "Folder diagnostics identify a process using the directory or a descendant", async c =>
        {
            var folder = Path.Combine(c.LeftPath, "folder1");
            await using (var holder = await LockingProcessFixture.StartAsync(c, folder, directory: true))
            {
                await c.BeginRenameAsync("folder1");
                await c.TextAsync("renamed-folder");
                await c.KeyAsync(Key.Enter);
                Check(LockingProcessFixture.ErrorText(c).Contains($"PID {holder.Id}", StringComparison.Ordinal), "A process whose working directory blocks rename is identified");
                await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
                await c.KeyAsync(Key.Escape);
            }
            await using (var holder = await LockingProcessFixture.StartAsync(c, Path.Combine(folder, "inside.txt")))
            {
                var users = await WindowsFileUsers.FindAsync([folder], default);
                Check(users.Any(user => user.ProcessId == holder.Id && user.Path == Path.Combine(folder, "inside.txt")), "A child file lock is found from the folder path");
            }
            Equal(0, (await WindowsFileUsers.FindAsync([folder], default)).Length, "Released locks are not cached as current processes");
        }),
        new("file-lock-copy-retry", "Copy errors name the locking process and Retry remains usable", async c =>
        {
            await using var holder = await LockingProcessFixture.StartAsync(c, Path.Combine(c.LeftPath, "alpha.txt"));
            await PriorityBatchProbe.CopyAsync(c, "alpha.txt");
            Check(LockingProcessFixture.ErrorText(c).Contains($"PID {holder.Id}", StringComparison.Ordinal), "The file error dialog includes the process name and PID");
            await c.ScreenshotAsync("locked-copy-process", c.Window.OperationDialog);
            await FileLockProbe.MeasureReleaseAsync(c, holder, () => FileOperationProbe.LastJob(c).State == FileOperationState.Completed);
            Equal("abc", File.ReadAllText(Path.Combine(c.RightPath, "alpha.txt")), "Retry copies the file after the lock is released");
        }),
        new("file-lock-process-actions", "Windowless processes have a plain summary and Kill retries the operation", async c =>
        {
            await using var holder = await LockingProcessFixture.StartAsync(c, Path.Combine(c.LeftPath, "alpha.txt"));
            await c.BeginRenameAsync("alpha.txt");
            await c.TextAsync("renamed.txt");
            await c.KeyAsync(Key.Tab);
            var dialog = (FileErrorDialog)c.Window.OperationDialog!;
            var actions = new RecordingFileUserActions { EndError = "Access denied by the process owner." };
            dialog.Users!.Actions = actions;
            Check(!dialog.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "FileUserWindow"), "A process without a window has a plain application name");
            Check(!dialog.GetVisualDescendants().OfType<RadioButton>().Any(), "A process action belongs to its own row");
            FileLockProbe.CheckCompact(dialog, 1);
            await c.ClickAsync(dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "FileUserEnd"), target: dialog);
            Check(LockingProcessFixture.ErrorText(c).Contains(actions.EndError, StringComparison.Ordinal), "A rejected termination remains visible without losing the operation");
            Check(dialog.IsVisible && c.Window.OperationDialog == dialog, "A denied termination keeps the operation available");
            actions.EndError = null;
            await c.ClickAsync(dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "FileUserEnd"), target: dialog);
            await FileOperationProbe.WaitUntilAsync(() => File.Exists(Path.Combine(c.LeftPath, "renamed.txt")));
            await c.SettleAsync();
            Equal("end,end", string.Join(',', actions.Calls.Select(call => call.Action)), "Each action uses the selected process without external UI automation");
            Check(actions.Calls.All(call => call.User.ProcessId == holder.Id), "Actions retain the original process identity");
            Equal("right", c.Window.ActivePanel.Id, "A successful retry preserves the original Tab intent");
        }),
        new("file-lock-process-identity", "Termination rejects reused identities and the file manager itself", async c =>
        {
            var path = Path.Combine(c.LeftPath, "alpha.txt");
            await using var holder = await LockingProcessFixture.StartAsync(c, path);
            var user = (await WindowsFileUsers.FindAsync([path], default)).Single(item => item.ProcessId == holder.Id);
            var actions = new WindowsFileUserActions();
            var stale = user with { StartedAtUtc = user.StartedAtUtc.AddSeconds(-1) };
            await actions.EndProcessAsync(stale);
            Check(actions.WaitForExitAsync(stale, default).IsCompletedSuccessfully, "A reused PID is treated as an exited original process");
            Check((await WindowsFileUsers.FindAsync([path], default)).Any(item => item.ProcessId == holder.Id), "A stale PID cannot end the running helper");
            try { await actions.EndProcessAsync(user with { ProcessId = Environment.ProcessId }); throw new InvalidOperationException("Self-termination was accepted."); }
            catch (IOException error) { Check(error.Message.Contains("own process", StringComparison.Ordinal), "Xcmd cannot terminate itself from a file error"); }
            await holder.ReleaseAsync();
            try { await actions.ShowWindowAsync(user); throw new InvalidOperationException("An exited process was accepted."); }
            catch (IOException error) { Check(error.Message.Contains("already exited", StringComparison.Ordinal), "Exited processes produce a useful Retry hint"); }
        }),
        new("file-lock-cancel-monitor", "Cancelling a pending rename prevents later process exit from retrying it", async c =>
        {
            await using var holder = await LockingProcessFixture.StartAsync(c, Path.Combine(c.LeftPath, "alpha.txt"));
            await c.BeginRenameAsync("alpha.txt"); await c.TextAsync("cancelled.txt"); await c.KeyAsync(Key.Enter);
            var dialog = (FileErrorDialog)c.Window.OperationDialog!;
            await c.KeyAsync(Key.N, RawInputModifiers.Alt, dialog);
            await holder.ReleaseAsync();
            await dialog.Users!.Monitoring;
            await c.SettleAsync();
            Check(c.Window.OperationDialog == null && !File.Exists(Path.Combine(c.LeftPath, "cancelled.txt")), "A cancelled dialog cannot retry after its process exits");
            Equal("cancelled.txt", c.Window.Left.EditingRow!.DraftName, "Cancel preserves the rename draft");
        }),
        new("file-lock-multiple-processes", "Automatic retry waits for every listed blocker", async c =>
        {
            var folder = Path.Combine(c.LeftPath, "folder1");
            await using var first = await LockingProcessFixture.StartAsync(c, folder, directory: true);
            await using var second = await LockingProcessFixture.StartAsync(c, folder, directory: true);
            await c.BeginRenameAsync("folder1"); await c.TextAsync("released-folder"); await c.KeyAsync(Key.Enter);
            var dialog = (FileErrorDialog)c.Window.OperationDialog!;
            Equal(2, dialog.GetVisualDescendants().OfType<Button>().Count(button => button.Name == "FileUserEnd"), "Independent processes have separate Kill actions");
            FileLockProbe.CheckCompact(dialog, 2);
            await first.ReleaseAsync();
            await c.SettleAsync();
            Check(c.Window.OperationDialog == dialog && Directory.Exists(folder), "One remaining process keeps the decision open");
            await second.ReleaseAsync();
            await FileOperationProbe.WaitUntilAsync(() => Directory.Exists(Path.Combine(c.LeftPath, "released-folder")));
            await c.SettleAsync();
            Check(c.Window.OperationDialog == null, "Natural process exit retries exactly once");
        }),
        new("file-lock-feedback-monitor", "Process exit preserves active feedback before retrying the operation", async c =>
        {
            await using var holder = await LockingProcessFixture.StartAsync(c, Path.Combine(c.LeftPath, "alpha.txt"));
            await c.BeginRenameAsync("alpha.txt"); await c.TextAsync("released.txt"); await c.KeyAsync(Key.Enter);
            var dialog = (FileErrorDialog)c.Window.OperationDialog!;
            await c.KeyAsync(Key.F12, target: dialog);
            await holder.ReleaseAsync();
            await dialog.Users!.Monitoring;
            await c.SettleAsync();
            Check(dialog.IsVisible && c.Window.Feedback.IsPickingIn(dialog), "An automatic retry cannot dismiss active feedback");
            await c.KeyAsync(Key.Escape, target: dialog);
            await FileOperationProbe.WaitUntilAsync(() => File.Exists(Path.Combine(c.LeftPath, "released.txt")));
            await c.SettleAsync();
        }),
        new("file-lock-skip-monitor", "Skipping a locked copy cancels its automatic retry", async c =>
        {
            await using var holder = await LockingProcessFixture.StartAsync(c, Path.Combine(c.LeftPath, "alpha.txt"));
            await PriorityBatchProbe.CopyAsync(c, "alpha.txt");
            var dialog = (FileErrorDialog)c.Window.OperationDialog!;
            await c.KeyAsync(Key.S, RawInputModifiers.Alt, dialog);
            await holder.ReleaseAsync();
            await dialog.Users!.Monitoring;
            await c.SettleAsync();
            Check(!File.Exists(Path.Combine(c.RightPath, "alpha.txt")), "An exited process cannot restart a skipped copy");
            Equal(FileOperationState.CompletedWithErrors, FileOperationProbe.LastJob(c).State, "The skipped failure remains in the operation report");
        }),
        new("file-lock-monitor-failure", "A monitoring failure is reported while other blockers remain alive", async c =>
        {
            var folder = Path.Combine(c.LeftPath, "folder1");
            await using var first = await LockingProcessFixture.StartAsync(c, folder, directory: true);
            await using var second = await LockingProcessFixture.StartAsync(c, folder, directory: true);
            var users = await WindowsFileUsers.FindAsync([folder], default);
            var view = new FileUsersView(users) { Actions = new RecordingFileUserActions { UnavailableProcessId = first.Id } };
            var retried = false;
            view.AllExited += () => retried = true;
            view.StartWatching();
            await FileOperationProbe.WaitUntilAsync(() => view.Monitoring.IsCompleted);
            await view.Monitoring;
            Check(!retried, "An inaccessible process is not treated as an exited process");
            Check(view.Children.OfType<TextBlock>().Any(text => text.IsVisible && text.Text!.Contains("Automatic retry is unavailable", StringComparison.Ordinal)), "Monitoring failures give a manual Retry instruction immediately");
            view.CancelActions();
        })
    ];
}

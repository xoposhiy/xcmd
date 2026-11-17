using Avalonia.Input;
using Avalonia.Input.Platform;
using Xcmd.Core;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class ComparisonScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("comparison-current-files-and-menu", "UC24; Ctrl+Shift+C compares the two cursors in panel order", async c =>
        {
            var left = c.Window.Left;
            var right = c.Window.Right;
            await left.RestoreStateAsync(left.CaptureState() with { Cursor = "отчёт 日本語.txt", Selected = ["alpha.txt", "file2.log"], HasMarks = true });
            await c.SelectAsync("destination.txt", right);
            await c.Window.Clipboard!.SetTextAsync("Keep clipboard");
            c.Window.CommandLine.Editor.Text = "Keep command draft";
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal(1, c.Launcher.Requests.Count, "One shortcut opens one comparison");
            var request = c.Launcher.Requests.Single();
            Equal("--new-window --diff " + CommandLine.FormatArgument(Path.Combine(c.LeftPath, "отчёт 日本語.txt")) + " " + CommandLine.FormatArgument(Path.Combine(c.RightPath, "destination.txt")), request.Arguments, "The cursors determine the files even when the right panel is active and other files are marked");
            Equal("Keep clipboard", await c.Window.Clipboard!.TryGetTextAsync(), "Comparison does not replace clipboard contents");
            Equal("Keep command draft", c.Window.CommandLine.Editor.Text, "Comparison preserves the command draft");
            Check(left.Rows.Single(row => row.Name == "alpha.txt").IsMarked, "Comparison preserves marks");
            await MainMenuProbe.InvokeFileCommandAsync(c, "Compare files in VS Code");
            Equal(2, c.Launcher.Requests.Count, "The menu opens a comparison");
            Equal(request, c.Launcher.Requests[1], "Menu and shortcut use the same files");
            Check(c.Window.LastError == null, "A successful comparison has no error");
        }),
        new("comparison-invalid-targets-and-recovery", "UC24; invalid files and launch failures keep the panels usable", async c =>
        {
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Check(c.Window.LastError?.Contains("left panel", StringComparison.Ordinal) == true, "A directory identifies the panel that needs a file");
            await c.SelectAsync("alpha.txt"); await c.SelectAsync("..", c.Window.Right);
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Check(c.Window.LastError?.Contains("right panel", StringComparison.Ordinal) == true, "The parent entry cannot be compared");
            await c.SelectAsync("destination.txt", c.Window.Right);
            File.Delete(Path.Combine(c.RightPath, "destination.txt"));
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Check(c.Window.LastError?.Contains("no longer available", StringComparison.Ordinal) == true, "A deleted file is reported before launching");
            Equal(0, c.Launcher.Requests.Count, "Invalid pairs never start another program");
            File.WriteAllText(Path.Combine(c.RightPath, "destination.txt"), "Restored");
            c.Launcher.Failure = new IOException("VS Code could not start");
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal("VS Code could not start", c.Window.LastError, "Launch failures remain visible");
            c.Launcher.Failure = null;
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Check(c.Window.LastError == null && c.Window.Right.Table.IsKeyboardFocusWithin, "Retry succeeds and retains panel focus");
            var archive = Path.Combine(c.RightPath, "comparison.zip");
            ArchiveProbe.Zip(archive, ("member.txt", "Archive content"));
            await c.Window.Right.LoadAsync(ArchivePath.Root(archive));
            await c.SelectAsync("member.txt", c.Window.Right);
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Check(c.Window.LastError?.Contains("file on disk", StringComparison.Ordinal) == true, "Archive members explain the supported scope");
            Equal(2, c.Launcher.Requests.Count, "An archive member is not sent to the external tool");
        }),
        new("comparison-held-key-and-input-focus", "UC24; held keys and text input never open duplicate comparisons", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.SelectAsync("destination.txt", c.Window.Right);
            c.Launcher.Pause = new TaskCompletionSource();
            c.Input.KeyDown(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.C, null);
            c.Input.KeyDown(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.C, null);
            await WaitUntilAsync(() => c.Launcher.Requests.Count > 0);
            Equal(1, c.Launcher.Requests.Count, "Holding the shortcut launches once");
            c.PressKey(Key.Tab);
            c.Launcher.Pause.SetResult(); await c.SettleAsync();
            c.Input.KeyUp(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.C, null);
            await c.KeyAsync(Key.L, RawInputModifiers.Control);
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal(1, c.Launcher.Requests.Count, "Command input does not trigger panel comparison");
            await c.KeyAsync(Key.Escape);
            await c.BeginRenameAsync("alpha.txt");
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal(1, c.Launcher.Requests.Count, "Rename input does not trigger comparison");
            await c.KeyAsync(Key.Escape);
        }),
        new("comparison-search-results", "UC24; comparison uses the physical path of a search result", async c =>
        {
            SearchProbe.TextFile(c, "nested/compare.txt", "Search result");
            var search = await SearchProbe.OpenAsync(c, "compare.txt");
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, search);
            await c.SelectAsync(Path.Combine("nested", "compare.txt"));
            await c.KeyAsync(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            var request = c.Launcher.Requests.Single();
            Equal("--new-window --diff " + CommandLine.FormatArgument(Path.Combine(c.LeftPath, "nested", "compare.txt")) + " " + CommandLine.FormatArgument(Path.Combine(c.RightPath, "destination.txt")), request.Arguments, "Search results supply full physical file paths");
            Check(c.Window.Left.IsSearchResults, "Comparison keeps the search results available");
        }),
        new("comparison-empty-and-git-targets", "UC24; missing cursors and Git snapshots cannot become comparison files", c =>
        {
            var file = c.Window.Left.Rows.Single(row => row.Name == "alpha.txt").Entry;
            var snapshot = file with { FullPath = GitPath.Tree(c.LeftPath, "context", "revision", "alpha.txt") };
            foreach (var (left, right, panel) in new (FileEntry?, FileEntry?, string)[]
            {
                (null, file, "left"), (file, null, "right"), (snapshot, file, "left"), (file, snapshot, "right")
            })
            {
                try
                {
                    FileComparison.FromEntries(left, right);
                    throw new InvalidOperationException("An unsupported cursor must not become a comparison file.");
                }
                catch (IOException error) { Check(error.Message.Contains(panel + " panel", StringComparison.Ordinal), "The error identifies the unsupported panel"); }
            }
            return Task.CompletedTask;
        }),
        new("comparison-native-launch-boundary", "UC24; the real Windows launcher preserves comparison arguments", ComparisonProbe.VerifyNativeLaunchAsync)
    ];
}

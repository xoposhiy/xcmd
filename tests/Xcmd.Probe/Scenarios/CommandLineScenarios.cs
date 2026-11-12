using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.CommandProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class CommandLineScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("command-line-copy-selection-or-all", "UC02; command clipboard", async c =>
        {
            var clipboard = c.Window.Clipboard!;
            await clipboard.SetTextAsync("previous clipboard");
            await c.TextAsync("tool 日本語 argument");
            var editor = c.Window.CommandLine.Editor;
            editor.CaretIndex = 5; editor.ClearSelection();
            await c.KeyAsync(Key.C, RawInputModifiers.Control);
            Equal(editor.Text, await clipboard.TryGetTextAsync(), "Ctrl+C without selection copies the whole command");
            Equal(5, editor.CaretIndex, "Copying the whole command keeps the caret");
            Equal("", editor.SelectedText, "Copying the whole command keeps the selection empty");
            editor.SelectionStart = 8; editor.SelectionEnd = 5;
            await c.KeyAsync(Key.C, RawInputModifiers.Control);
            Equal("日本語", await clipboard.TryGetTextAsync(), "Ctrl+C with selection copies only that selection");
            Equal("日本語", editor.SelectedText, "Copying keeps the text selected");
            Equal("tool 日本語 argument", editor.Text, "Copying keeps the draft");
            Equal(0, c.Launcher.Requests.Count, "Copying never executes the command");
            await c.KeyAsync(Key.Escape); await c.KeyAsync(Key.L, RawInputModifiers.Control); await c.KeyAsync(Key.C, RawInputModifiers.Control);
            Equal("日本語", await clipboard.TryGetTextAsync(), "Copying an empty command keeps the clipboard");
        }),
        new("command-line-long-directory-and-help", "UC02; readable command layout", async c =>
        {
            var path = @"C:\work\projects\" + new string('a', 120) + @"\日本語\runtimes";
            c.Window.CommandLine.SetDirectory(path);
            var label = c.Window.CommandLine.GetVisualDescendants().OfType<TextBlock>().Single(text => AutomationProperties.GetAutomationId(text) == "command-line.directory");
            foreach (var width in new[] { 1280d, 840d })
            {
                c.Window.Width = width; c.Window.Height = 400;
                foreach (var scale in new[] { 1d, 1.5d })
                {
                    c.Window.SetScaleForTest(scale);
                    await c.ScreenshotAsync($"command-layout-{width}-{scale * 100}");
                    var visible = string.Concat(label.TextLayout.TextLines.SelectMany(line => line.TextRuns).OfType<ShapedTextRun>().Select(run => run.Text.ToString()));
                    Check(visible.StartsWith('…') && visible.EndsWith(@"\日本語\runtimes>", StringComparison.Ordinal), "A long path shows an ellipsis followed by its unchanged tail.");
                    Equal(path, ToolTip.GetTip(label), "Hover help keeps the full directory path");
                    Check(c.Window.CommandLine.Editor.Bounds.Width >= 300, "The editor remains usable beside a long path.");
                    Check(width != 1280 || label.Bounds.Width is >= 490 and <= 510, "The directory can use the wider 510-pixel limit.");
                    var help = c.Window.GetVisualDescendants().OfType<WrapPanel>().Single(panel => panel.Name == "KeyboardHelp");
                    Equal(11, help.Children.Count, "Useful shortcut groups omit deletion hints");
                    Check(help.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Alt+F7  Find files"), "File search is discoverable in keyboard help");
                    var hints = help.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray();
                    Check(hints.Contains("F3  View") && hints.Contains("F4  Edit"), "File viewing and editing shortcuts remain discoverable");
                    Check(help.Children.OfType<Border>().Skip(1).All(border => border.BorderThickness.Left == 1), "Shortcut groups have visible separators.");
                    Check(help.Children.All(group => group.Bounds.Right <= help.Bounds.Width && group.Bounds.Bottom <= help.Bounds.Height), "All shortcut groups fit without clipping.");
                    Check(help.GetVisualDescendants().OfType<TextBlock>().All(text => text.TextLayout.TextLines.Count == 1), "Each shortcut stays on the same line as its action.");
                    var helpBottom = help.TranslatePoint(new Avalonia.Point(0, help.Bounds.Height), c.Window)!.Value.Y;
                    Check(helpBottom < c.Window.Bounds.Height, "Wrapped help fits inside the window.");
                }
            }
        }),
        new("command-line-typing-escape-and-focus", "UC02; quick launch keyboard routing", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.TextAsync("tool first");
            Check(c.Window.CommandLine.HasKeyboardFocus, "Plain typing focuses the command line.");
            Equal("tool first", c.Window.CommandLine.Editor.Text, "The first text event is inserted once");
            Check(!c.Window.Readiness.CanRestart, "Command editing delays release updates.");
            await c.KeyAsync(Key.Escape);
            Equal("", c.Window.CommandLine.Editor.Text, "Escape clears the command");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Escape returns focus to the active panel.");
            await c.TextAsync("draft"); await c.KeyAsync(Key.Down);
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Down returns to the active panel.");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Returning focus does not move the file cursor");
            await c.KeyAsync(Key.Left); Check(c.Window.CommandLine.HasKeyboardFocus, "Left focuses the command line.");
            Equal("draft", c.Window.CommandLine.Editor.Text, "Focusing preserves the command");
            await c.KeyAsync(Key.Up); await c.KeyAsync(Key.Right);
            Check(c.Window.CommandLine.HasKeyboardFocus, "Right focuses the command line.");
            await c.KeyAsync(Key.Up); await c.KeyAsync(Key.Escape);
            Equal("", c.Window.CommandLine.Editor.Text, "Escape from files also clears the command");
        }),
        new("command-line-shell-request-and-active-directory", "UC01; UC02; Windows Shell launch", async c =>
        {
            await c.KeyAsync(Key.Tab); await c.TextAsync("tool.exe \"two words\" & literal"); await c.KeyAsync(Key.Enter);
            var request = c.Launcher.Requests.Single();
            Equal(ExternalLaunchKind.Command, request.Kind, "The command uses Windows Shell");
            Equal("tool.exe", request.Target, "The program is passed directly");
            Equal("\"two words\" & literal", request.Arguments, "The raw arguments are preserved without a command interpreter");
            Equal(c.RightPath, request.WorkingDirectory, "The active panel supplies the working directory");
            Equal("", c.Window.CommandLine.Editor.Text, "A successful launch clears the command");
            Equal("right", c.Window.ActivePanel.Id, "Launching keeps the active panel");
            await c.TextAsync("cmd"); await c.KeyAsync(Key.Enter);
            Equal("cmd", c.Launcher.Requests.Last().Target, "A terminal name follows the ordinary Shell launch path");
            Equal(c.RightPath, c.Launcher.Requests.Last().WorkingDirectory, "A terminal command uses the current panel directory");
        }),
        new("command-line-cd-active-panel-and-errors", "UC02; local cd navigation", async c =>
        {
            await c.TextAsync("CD folder1"); await c.KeyAsync(Key.Enter);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "cd changes the active panel");
            Equal(c.RightPath, c.Window.Right.Session.DirectoryPath, "cd keeps the other panel directory");
            Equal(0, c.Launcher.Requests.Count, "cd does not start a program");
            await c.TextAsync("cd.."); await c.KeyAsync(Key.Enter);
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "cd.. opens the parent");
            var target = Path.Combine(c.LeftPath, "space 日本語"); Directory.CreateDirectory(target);
            await c.TextAsync("cd /d \"" + target + "\""); await c.KeyAsync(Key.Enter);
            Equal(target, c.Window.Left.Session.DirectoryPath, "cd supports quoted paths and /d");
            await c.TextAsync("cd missing"); await c.KeyAsync(Key.Enter);
            Equal(target, c.Window.Left.Session.DirectoryPath, "A failed cd keeps the directory");
            Equal("cd missing", c.Window.CommandLine.Editor.Text, "A failed cd keeps the input for correction");
            Check(c.Window.LastError != null, "A failed cd shows an error.");
            await c.KeyAsync(Key.Escape); await c.TextAsync("cd"); await c.KeyAsync(Key.Enter);
            Equal(target, c.Window.Left.Session.DirectoryPath, "Bare cd keeps the current directory");
        }),
        new("command-line-history-and-draft", "UC02; command history", async c =>
        {
            foreach (var text in new[] { "tool first", "tool second", "tool first" })
            { await c.TextAsync(text); await c.KeyAsync(Key.Enter); }
            await c.TextAsync("unfinished");
            await c.KeyAsync(Key.Up, RawInputModifiers.Alt); Equal("tool first", c.Window.CommandLine.Editor.Text, "Alt+Up recalls the latest command");
            await c.KeyAsync(Key.Up, RawInputModifiers.Alt); Equal("tool second", c.Window.CommandLine.Editor.Text, "Older unique commands remain available");
            await c.KeyAsync(Key.Up, RawInputModifiers.Alt); Equal("tool second", c.Window.CommandLine.Editor.Text, "History stops at the oldest command");
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt); await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            Equal("unfinished", c.Window.CommandLine.Editor.Text, "Alt+Down restores the unfinished draft");
            await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Up, RawInputModifiers.Alt);
            Check(c.Window.CommandLine.HasKeyboardFocus, "Alt+Up from files opens command history.");
            Equal("tool first", c.Window.CommandLine.Editor.Text, "Panel history access recalls a command");
            await c.KeyAsync(Key.Down); await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
            Check(c.Window.ActiveNavigationDialog != null, "Alt+Down from files still opens directory history.");
        }),
        new("command-line-insert-name-path-and-selection", "UC02; name and path insertion", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Enter, RawInputModifiers.Control);
            Equal("alpha.txt ", c.Window.CommandLine.Editor.Text, "Ctrl+Enter inserts the current name");
            Check(c.Window.CommandLine.HasKeyboardFocus, "Insertion focuses the command line.");
            await c.KeyAsync(Key.Escape); await c.KeyAsync(Key.Enter, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal(CommandLine.FormatArgument(Path.Combine(c.LeftPath, "alpha.txt")) + " ", c.Window.CommandLine.Editor.Text, "Ctrl+Shift+Enter inserts the full path");
            await c.KeyAsync(Key.Escape); await c.KeyAsync(Key.Down); await c.SelectAsync("отчёт 日本語.txt");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control);
            Equal("\"отчёт 日本語.txt\" ", c.Window.CommandLine.Editor.Text, "A name with spaces is quoted");
            c.Window.CommandLine.Editor.SelectAll(); await c.KeyAsync(Key.Enter, RawInputModifiers.Control);
            Equal("\"отчёт 日本語.txt\" ", c.Window.CommandLine.Editor.Text, "Insertion replaces the text selection");
        }),
        new("command-line-errors-repeat-and-pending-launch", "UC02; launch failures and pending input", async c =>
        {
            c.Launcher.Failure = new IOException("Controlled launch failure.");
            await c.TextAsync("missing-tool"); await c.KeyAsync(Key.Enter);
            Equal("missing-tool", c.Window.CommandLine.Editor.Text, "A failed launch keeps the command");
            Equal(0, c.Window.CommandLine.Capture().History.Length, "Failed commands do not enter history");
            Check(c.Window.LastError?.Contains("Controlled", StringComparison.Ordinal) == true, "A launch failure is visible.");
            c.Launcher.Failure = null; c.Launcher.Pause = new();
            c.Input.KeyDown(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            c.Input.KeyDown(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Equal(2, c.Launcher.Requests.Count, "Held Enter creates one new launch");
            Check(c.Window.StatusMessage.StartsWith("Opening:", StringComparison.Ordinal), "A pending launch shows immediate progress.");
            c.Input.KeyUp(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            c.Input.Text(" changed");
            c.Launcher.Pause.SetResult(); await c.SettleAsync();
            Equal("missing-tool changed", c.Window.CommandLine.Editor.Text, "Completion preserves input edited while launching");
        }),
        new("command-line-native-quoted-exe-and-raw-arguments", "UC02; actual Windows ShellExecute", async c =>
        {
            var fixture = Prepare(c);
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false);
            await c.KeyAsync(Key.Tab);
            await c.TextAsync($"\"{fixture.Executable}\" \"{fixture.Report}\" \"two words\" & 日本語 \"\"");
            await c.KeyAsync(Key.Enter);
            var report = await ReadReportAsync(fixture.Report);
            Equal(c.RightPath, report.WorkingDirectory, "The real Shell launch uses the active panel directory");
            Equal("two words|&|日本語|", string.Join('|', report.Arguments), "Windows receives quoted, Unicode, operator and empty arguments");
        }),
        new("command-line-native-relative-exe-and-shortcut", "UC02; relative programs and Windows associations", async c =>
        {
            var fixture = Prepare(c); await c.Window.Left.LoadAsync(fixture.Directory); c.Window.Left.FocusCurrent();
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false);
            await c.TextAsync("\"tool with spaces.exe\""); await c.KeyAsync(Key.Enter);
            Equal(fixture.Directory, (await ReadReportAsync(fixture.Report)).WorkingDirectory, "A relative executable resolves from the active directory");
            File.Delete(fixture.Report);
            var shortcut = Path.Combine(c.LeftPath, "tool link.lnk"); CreateShortcut(shortcut, fixture, c.RightPath);
            await c.Window.Left.LoadAsync(c.LeftPath); c.Window.Left.FocusCurrent();
            await c.TextAsync("\"tool link.lnk\""); await c.KeyAsync(Key.Enter);
            Equal(c.RightPath, (await ReadReportAsync(fixture.Report)).WorkingDirectory, "ShellExecute resolves a relative shortcut and its configured directory");
        }),
        new("command-line-native-launch-failure", "UC02; Windows launch error", async c =>
        {
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false);
            await c.TextAsync("xcmd-missing-" + Guid.NewGuid().ToString("N") + ".exe");
            var draft = c.Window.CommandLine.Editor.Text; await c.KeyAsync(Key.Enter);
            Equal($"Cannot open '{draft}': not found.", c.Window.LastError, "A native launch error shows the target and a short reason");
            Equal(draft, c.Window.CommandLine.Editor.Text, "The failed native command remains editable");
        }),
        new("command-line-native-environment-refresh", "UC01; UC02; current Windows environment", async c =>
        {
            var fixture = Prepare(c);
            using var variable = new EnvironmentFixture("original", "original");
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false);
            variable.SetUser("updated 日本語");
            await c.TextAsync(EnvironmentFixture.InspectThroughCmd(fixture, variable.Name)); await c.KeyAsync(Key.Enter);
            var report = await ReadReportAsync(fixture.Report);
            Equal("updated 日本語", report.Environment![variable.Name], "A real CMD sees the user variable changed after launcher startup");
            Equal(c.LeftPath, report.WorkingDirectory, "The terminal keeps the active panel working directory");
            Equal("original", variable.ProcessValue, "Launching does not replace Xcmd's own process environment");
            File.Delete(fixture.Report); variable.SetUser("updated again");
            await c.TextAsync(EnvironmentFixture.InspectThroughCmd(fixture, variable.Name)); await c.KeyAsync(Key.Enter);
            Equal("updated again", (await ReadReportAsync(fixture.Report)).Environment![variable.Name], "Every launch reads the latest settings");
        }),
        new("command-line-native-environment-add-remove-and-process-values", "UC02; environment additions and deletions", async c =>
        {
            var fixture = Prepare(c);
            using var removed = new EnvironmentFixture("original", "stale");
            using var added = new EnvironmentFixture();
            using var processOnly = new EnvironmentFixture(processValue: "inherited-only");
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false);
            removed.SetUser(null); added.SetUser("added after startup");
            await c.TextAsync(EnvironmentFixture.InspectThroughCmd(fixture, removed.Name, added.Name, processOnly.Name)); await c.KeyAsync(Key.Enter);
            var report = await ReadReportAsync(fixture.Report);
            Equal<string?>(null, report.Environment![removed.Name], "A variable deleted before the first launch is absent in CMD");
            Equal("added after startup", report.Environment[added.Name], "A new user variable reaches CMD");
            Equal("inherited-only", report.Environment[processOnly.Name], "Process-only inherited values are preserved");
            Equal("stale", removed.ProcessValue, "The parent's original value is restored after launch");
        }),
        new("command-line-native-environment-expanded-target-and-values", "UC02; current environment expansion", async c =>
        {
            var fixture = Prepare(c);
            using var target = new EnvironmentFixture("C:\\xcmd-old-target", "C:\\xcmd-old-target");
            using var expanded = new EnvironmentFixture();
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false);
            target.SetUser(fixture.Executable);
            expanded.SetUser("%" + target.Name + "%", expandable: true);
            await c.TextAsync($"%{target.Name}% --environment \"{fixture.Report}\" {expanded.Name}"); await c.KeyAsync(Key.Enter);
            var report = await ReadReportAsync(fixture.Report);
            Equal(fixture.Executable, report.Environment![expanded.Name], "Windows expands stored values using the current user environment");
            Equal(fixture.Executable, report.Executable, "The program path expands from the updated variable");
            Equal("C:\\xcmd-old-target", target.ProcessValue, "Target expansion leaves Xcmd's original environment intact");
        }),
        new("command-line-native-environment-powershell", "UC01; UC02; fresh PowerShell environment", async c =>
        {
            var fixture = Prepare(c);
            using var variable = new EnvironmentFixture("original", "original");
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false);
            variable.SetUser("fresh PowerShell");
            await c.TextAsync(EnvironmentFixture.InspectThroughPowerShell(fixture, variable.Name)); await c.KeyAsync(Key.Enter);
            var report = await ReadReportAsync(fixture.Report);
            Equal("fresh PowerShell", report.Environment![variable.Name], "A real PowerShell receives the updated user setting");
            Equal(c.LeftPath, report.WorkingDirectory, "PowerShell starts in the active panel directory");
        }),
        new("command-line-native-environment-path-and-failures", "UC02; current PATH and environment errors", async c =>
        {
            var fixture = Prepare(c);
            using var environment = new LaunchEnvironmentFixture();
            environment.Values["Path"] = fixture.Directory + ";" + environment.Values["Path"];
            environment.SetStalePath();
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false, () => environment.Values);
            await c.TextAsync($"\"tool with spaces.exe\" --environment \"{fixture.Report}\" Path"); await c.KeyAsync(Key.Enter);
            var report = await ReadReportAsync(fixture.Report);
            Equal(environment.Values["Path"], report.Environment!["Path"], "The child receives the current PATH snapshot");
            Equal(fixture.Executable, report.Executable, "ShellExecute finds the program through the updated PATH");
            Equal("C:\\xcmd-missing-path", environment.ProcessPath, "The parent PATH is restored");
            File.Delete(fixture.Report);
            await c.TextAsync("xcmd-missing-" + Guid.NewGuid().ToString("N") + ".exe"); await c.KeyAsync(Key.Enter);
            Equal("C:\\xcmd-missing-path", environment.ProcessPath, "A Shell error also restores the parent PATH");
            await c.KeyAsync(Key.Escape);
            c.Launcher.ForwardTo = new WindowsExternalLauncher(showWindows: false, () => throw new IOException("Controlled environment read failure."));
            await c.TextAsync("cmd"); await c.KeyAsync(Key.Enter);
            Equal("cmd", c.Window.CommandLine.Editor.Text, "A failed environment read preserves the command for retry");
            Check(c.Window.LastError?.Contains("environment read failure", StringComparison.Ordinal) == true, "Environment errors are visible without falling back to stale values.");
            Equal("C:\\xcmd-missing-path", environment.ProcessPath, "An environment read error leaves the parent PATH unchanged");
        }),
        new("command-line-persistence-and-old-session", "UC38; command line state", async c =>
        {
            await c.TextAsync("tool first"); await c.KeyAsync(Key.Enter); await c.TextAsync("unfinished");
            c.Window.CommandLine.Editor.CaretIndex = 3; c.Window.CommandLine.Editor.SelectionStart = 1; c.Window.CommandLine.Editor.SelectionEnd = 3;
            var state = c.Window.CaptureWorkspace(); var path = Path.Combine(c.FixturePath, "session.json");
            await new SessionStore(path).SaveAsync(state); c.Window.CommandLine.Clear();
            await c.Window.RestoreWorkspaceAsync((await new SessionStore(path).ReadAsync())!);
            Equal("unfinished", c.Window.CommandLine.Editor.Text, "The draft is restored");
            Equal("nf", c.Window.CommandLine.Editor.SelectedText, "The text selection is restored");
            Check(c.Window.CommandLine.HasKeyboardFocus, "Command focus is restored.");
            await c.KeyAsync(Key.Up, RawInputModifiers.Alt); Equal("tool first", c.Window.CommandLine.Editor.Text, "Saved history is restored");
            var oldJson = JsonSerializer.Serialize(state)[..^1] + ",\"Terminal\":{\"SessionId\":\"old\"}}";
            var old = JsonSerializer.Deserialize<WorkspaceState>(oldJson)!; old.Validate();
            await c.Window.RestoreWorkspaceAsync(old with { CommandLine = null });
            Equal("", c.Window.CommandLine.Editor.Text, "A terminal-era session without a command draft opens an empty line");
            Equal(1, c.Launcher.Requests.Count, "Restoration does not execute a draft");
        }),
        new("command-line-search-rename-and-feedback", "UC03; UC17; UC39; input isolation", async c =>
        {
            await c.KeyAsync(Key.A, RawInputModifiers.Alt); await c.TextAsync("a"); await c.TextAsync("l");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Alt search stays in the panel");
            Equal("", c.Window.CommandLine.Editor.Text, "Name search does not type a command");
            await c.KeyAsync(Key.Escape); await c.BeginRenameAsync("alpha.txt"); await c.KeyAsync(Key.Right);
            Check(c.Window.Left.RenameEditor?.IsFocused == true, "Rename arrows stay in the editor.");
            await c.KeyAsync(Key.Escape); await c.TextAsync("unfinished");
            await c.KeyAsync(Key.F12); await c.ClickAsync(c.Window.CommandLine.Editor);
            var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Keep the command line compact.", comment); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            Check(c.Window.CommandLine.HasKeyboardFocus, "Feedback restores command focus.");
            Equal("unfinished", c.Window.CommandLine.Editor.Text, "Feedback preserves the command");
        }),
        new("command-line-compact-scaled-layout", "UC02; offscreen command line layout", async c =>
        {
            Check(c.Window.GetVisualDescendants().OfType<ComboBox>().Count() == 0, "There is no shell selector.");
            c.Window.Width = 840; c.Window.Height = 400; await c.ScreenshotAsync("command-line-compact");
            Check(c.Window.CommandLine.Bounds.Height < 45, "The command line uses one compact row.");
            Check(c.Window.CommandLine.Editor.Bounds.Width > 300, "The command editor remains usable at minimum width.");
            c.Window.SetScaleForTest(1.5); await c.ScreenshotAsync("command-line-150-percent");
        }),
        new("command-line-history-bound-and-input-limit", "UC02; bounded command state", c =>
        {
            var history = new CommandHistory();
            for (var index = 0; index < 110; index++) history.Add("tool " + index);
            Equal(100, history.Capture().Length, "Command history keeps at most 100 entries");
            Equal("tool 10", history.Capture()[0], "History discards the oldest entries");
            Equal(CommandLine.MaximumLength, c.Window.CommandLine.Editor.MaxLength, "The editor limits command length");
            Equal("https://example.com", CommandLine.Parse("https://example.com").Target, "URLs remain Shell targets");
            Equal("cdrom.exe", CommandLine.Parse("cdrom.exe").Target, "Programs starting with cd are ordinary targets");
            return Task.CompletedTask;
        })
    ];
}

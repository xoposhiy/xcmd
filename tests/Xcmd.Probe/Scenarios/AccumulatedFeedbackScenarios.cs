using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class AccumulatedFeedbackScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("accumulated-space-folder-size", "Space calculates only the current folder, without moving the cursor", async c =>
        {
            var bytes = DirectorySizeProbe.AddNestedFiles(c);
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Space);
            Equal(bytes, c.Window.Left.Current!.Measurement!.Size.Bytes, "Space calculates the focused folder recursively");
            Check(c.Window.Left.Rows.Single(row => row.Name == "folder2").Measurement == null, "Other folders are not scanned");
            Equal("folder1", c.Window.Left.Current.Name, "Space preserves the cursor");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Space preserves panel focus");
        }),
        new("accumulated-home-from-command", "Ctrl+tilde works from the command editor in both layouts and preserves its draft", async c =>
        {
            c.FileSystem.SupplyDirectory(UserDirectories.Home, []);
            var opposite = c.Window.Right.Session.DirectoryPath;
            foreach (var (symbol, modifiers) in new[] { ("`", RawInputModifiers.Control), ("ё", RawInputModifiers.Control), ("~", RawInputModifiers.Control | RawInputModifiers.Shift) })
            {
                await c.Window.Left.LoadAsync(c.LeftPath);
                c.Window.CommandLine.Editor.Text = "unfinished command";
                c.Window.CommandLine.FocusCommand();
                c.Input.KeyDown(Key.OemTilde, modifiers, PhysicalKey.Backquote, symbol);
                c.Input.KeyUp(Key.OemTilde, modifiers, PhysicalKey.Backquote, symbol);
                await c.SettleAsync();
                Equal(UserDirectories.Home, c.Window.Left.Session.DirectoryPath, "The physical shortcut opens Home from command focus");
                Equal("unfinished command", c.Window.CommandLine.Editor.Text, "The command draft remains intact");
                Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Focus returns to files");
            }
            Equal(opposite, c.Window.Right.Session.DirectoryPath, "The opposite panel is unchanged");
        }),
        new("accumulated-navigation-menu-destinations", "Home and drive-root shortcuts are discoverable through Navigate and preserve the command draft", async c =>
        {
            var root = Path.GetPathRoot(c.LeftPath)!;
            c.FileSystem.SupplyDirectory(UserDirectories.Home, []); c.FileSystem.SupplyDirectory(root, []);
            c.Window.CommandLine.Editor.Text = "unfinished command";
            var menu = c.Window.GetVisualDescendants().OfType<Menu>().Single();
            var navigate = menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Navigate");
            foreach (var (title, destination) in new[] { ("Home folder", UserDirectories.Home), ("Drive root", root) })
            {
                var command = navigate.Items.OfType<MenuItem>().Single(item => item.Header as string == title);
                Check(command.InputGesture != null, "The menu exposes the existing shortcut");
                await c.ClickAsync(navigate); await c.ClickAsync(command);
                Equal(destination, c.Window.Left.Session.DirectoryPath, "The menu opens the destination in the active panel");
                Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Navigation restores file-panel focus");
                Equal("unfinished command", c.Window.CommandLine.Editor.Text, "Navigation keeps the command draft");
            }
        }),
        new("accumulated-cd-home-and-path-display", "cd expands home paths; panel and command paths use a lowercase drive letter", async c =>
        {
            var home = UserDirectories.Home;
            var child = Path.Combine(home, "sub folder");
            c.FileSystem.SupplyDirectory(home, []); c.FileSystem.SupplyDirectory(child, []);
            var opposite = c.Window.Right.Session.DirectoryPath;
            foreach (var (command, expected) in new[] { ("cd ~", home), ("cd ~/", home), ("cd \"~\\sub folder\"", child), ("cd \"~/sub folder\"", child) })
            {
                await c.KeyAsync(Key.L, RawInputModifiers.Control);
                await c.TextAsync(command); await c.KeyAsync(Key.Enter);
                Equal(Path.TrimEndingDirectorySeparator(expected), c.Window.Left.Session.DirectoryPath, "The home command changes the active panel to the requested folder");
                Equal("", c.Window.CommandLine.Editor.Text, "A successful home command clears the command editor");
                Check(c.Window.LastError == null, "Home expansion does not report a missing literal tilde folder");
            }
            foreach (var directory in new[] { @"C:\Mixed Folder\日本語", @"c:\Mixed Folder\日本語" })
            {
                c.FileSystem.SupplyDirectory(directory, []);
                await c.Window.Left.LoadAsync(directory); await c.SettleAsync();
                var header = c.Window.Left.GetVisualDescendants().OfType<TextBlock>().Single(text => text.GetValue(FeedbackTarget.IdProperty) == "PanelView.path");
                var prompt = c.Window.CommandLine.GetVisualDescendants().OfType<TextBlock>().Single(text => AutomationProperties.GetAutomationId(text) == "command-line.directory");
                Equal(@"c:\Mixed Folder\日本語", header.Text, "Only the drive letter changes case in the panel path");
                Equal(@"c:\Mixed Folder\日本語>", prompt.Text, "The command prompt uses the same displayed path");
                Equal(header.Text, ToolTip.GetTip(header) as string, "The full path tooltip agrees with the displayed path");
            }
            c.Window.CommandLine.SetDirectory(@"\\Server\Share\Mixed Folder");
            var unc = c.Window.CommandLine.GetVisualDescendants().OfType<TextBlock>().Single(text => AutomationProperties.GetAutomationId(text) == "command-line.directory");
            Equal(@"\\Server\Share\Mixed Folder>", unc.Text, "UNC server and folder casing stays intact");
            Equal(opposite, c.Window.Right.Session.DirectoryPath, "Home navigation and path display changes leave the opposite panel alone");
            Equal(0, c.Launcher.Requests.Count, "Local navigation never starts a shell");
        }),
        new("accumulated-type-column-readable", "Common long types fit a compact wider column and every type has a full tooltip", async c =>
        {
            c.AddFile("NuGet.Config", "configuration"); c.AddFile("sample.longconfig", "x");
            await c.Window.Left.LoadAsync(c.LeftPath);
            c.Window.Left.ResetColumns();
            await c.SelectAsync("sample.longconfig");
            var type = c.Window.Left.Table.Columns[1];
            Check(type.Width.IsAbsolute && type.ActualWidth >= 88 && type.ActualWidth < 120, "The type column is readable without taking excessive name space");
            var text = c.Row("sample.longconfig").GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "LONGCONFIG");
            Equal("LONGCONFIG", ToolTip.GetTip(text) as string, "The full type remains available in a tooltip");
            var state = c.Window.Left.CaptureState();
            Equal(88d, state.Columns[1].Width, "The session records the chosen width");
            var store = new SessionStore(Path.Combine(c.FixturePath, "auto-session.json"));
            await store.SaveAsync(c.Window.CaptureWorkspace());
            Equal(88d, (await store.ReadAsync())!.Left.Columns[1].Width, "The width round-trips through the real validated session format");
            await c.Window.Left.RestoreStateAsync(state); await c.SettleAsync();
            Equal(88d, type.Width.Value, "Restoring a session preserves the width");
            await c.ScreenshotAsync("type-column-readable");
        }),
        new("accumulated-startup-age", "Title bar shows whole minutes and a fresh-update message clears on input", async c =>
        {
            var store = new SessionStore(Path.Combine(c.FixturePath, "session.json"));
            await store.SaveAsync(c.Window.CaptureWorkspace());
            using var lifecycle = new WorkspaceLifecycle(c.Window, store, null);
            await lifecycle.InitializeAsync([], requireSavedSession: true);
            Check(c.Window.UpdateMessage.StartsWith("Just updated", StringComparison.Ordinal), "A replacement announces its new version");
            c.Window.Feedback.ReleaseId = "test-release";
            c.Window.UpdateUptime(TimeSpan.FromSeconds(59));
            Check(c.Window.Title!.EndsWith("Uptime: 0 min.", StringComparison.Ordinal), "Age uses whole minutes and an English uptime label");
            c.Window.UpdateUptime(TimeSpan.FromSeconds(61));
            Check(c.Window.Title!.Contains("test-release · Uptime: 1 min.", StringComparison.Ordinal), "The release and elapsed minute are visible together");
            var uptime = c.Window.TitleBar.GetVisualDescendants().OfType<TextBlock>().Single(text => text.GetValue(FeedbackTarget.IdProperty) == "CommanderFrame.uptime");
            Equal("Uptime: 1 min.", uptime.Text, "The real title bar reflects the age");
            c.Window.UpdateUptime(TimeSpan.FromMinutes(9));
            Equal("Uptime: 9 min.", uptime.Text, "The title uses English wording");
            c.Window.Width = c.Window.MinWidth; await c.SettleAsync();
            Check(uptime.Bounds.Width > 20 && uptime.IsVisible, "Age keeps its own space in a narrow title bar");
            await c.KeyAsync(Key.L, RawInputModifiers.Control);
            Equal("", c.Window.UpdateMessage, "Input clears the fresh-update notice");
            await c.ScreenshotAsync("startup-age");
        }),
    ];
}

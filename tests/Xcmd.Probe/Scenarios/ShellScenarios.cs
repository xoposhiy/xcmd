using Avalonia;
using Avalonia.Input;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class ShellScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("shell-keyboard-menu", "Shift+F10 and Ctrl+Down open the current item's native menu and preserve cursor, marks and focus", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.A, RawInputModifiers.Control);
            var marks = c.Window.Left.SelectedNames();
            await c.KeyAsync(Key.F10, RawInputModifiers.Shift);
            Equal(Path.Combine(c.LeftPath, "alpha.txt"), c.Shell.Menus.Single(), "Shift+F10 target");
            Check(!c.Shell.Extended, "Shift in the shortcut does not request extended verbs");
            var row = c.Row("alpha.txt");
            var anchor = row.PointToScreen(new Point(48, row.Bounds.Height));
            Equal((anchor.X, anchor.Y), c.Shell.Position, "Menu is anchored below the current row");
            await c.KeyAsync(Key.Down, RawInputModifiers.Control);
            Equal(2, c.Shell.Menus.Count, "Ctrl+Down opens the menu");
            Equal("alpha.txt", c.Window.Left.Current?.Name, "Ctrl+Down does not move the cursor");
            Check(marks.SequenceEqual(c.Window.Left.SelectedNames()), "Both shortcuts preserve marks");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Focus returns to the file panel");
            await c.SelectAsync("folder1", c.Window.Left);
            await c.KeyAsync(Key.Down, RawInputModifiers.Control);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Shell.Menus.Last(), "Folder menu target");
            await c.SelectAsync("destination.txt", c.Window.Right);
            await c.KeyAsync(Key.F10, RawInputModifiers.Shift);
            Equal(Path.Combine(c.RightPath, "destination.txt"), c.Shell.Menus.Last(), "Opposite panel keyboard target");
            var count = c.Shell.Menus.Count;
            await c.KeyAsync(Key.L, RawInputModifiers.Control);
            await c.KeyAsync(Key.Down, RawInputModifiers.Control);
            await c.KeyAsync(Key.F10, RawInputModifiers.Shift);
            Equal(count, c.Shell.Menus.Count, "Command editor shortcuts do not open a file menu");
        }),
        new("shell-menu-and-properties-input", "Explorer menu targets the clicked row; Alt+Enter opens native properties without opening the file", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.A, RawInputModifiers.Control);
            var marks = c.Window.Left.SelectedNames().Length;
            await ShellInputProbe.RightClickAsync(c, "отчёт 日本語.txt");
            Equal(Path.Combine(c.LeftPath, "отчёт 日本語.txt"), c.Shell.Menus.Single(), "Right-click target");
            Equal("отчёт 日本語.txt", c.Window.Left.Current?.Name, "Cursor follows clicked row");
            Equal(marks, c.Window.Left.SelectedNames().Length, "Right click preserves marks");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Alt);
            Equal(c.Shell.Menus.Single(), c.Shell.Properties.Single(), "Properties target the cursor");
            Equal(0, c.Launcher.Requests.Count, "Alt+Enter does not open the file");
            await ShellInputProbe.RightClickAsync(c, "destination.txt", opposite: true, RawInputModifiers.Shift);
            Equal(c.Window.Right, c.Window.ActivePanel, "Inactive panel activates");
            Equal(Path.Combine(c.RightPath, "destination.txt"), c.Shell.Menus.Last(), "Inactive panel target");
            Check(c.Shell.Extended, "Shift requests extended shell commands");
            await c.SelectAsync("folder1", c.Window.Left);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Alt);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Shell.Properties.Last(), "Folder properties");
            Equal(c.LeftPath, c.Window.Left.Session.DirectoryPath, "Alt+Enter does not enter the folder");
        }),
        new("shell-errors-and-parent-row", "Shell errors are reported; parent navigation row has no native action", async c =>
        {
            await c.SelectAsync("..");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Alt);
            await ShellInputProbe.RightClickAsync(c, "..");
            Equal(0, c.Shell.Properties.Count + c.Shell.Menus.Count, "No action for parent navigation");
            await c.SelectAsync("alpha.txt");
            c.Shell.Fail = true;
            await c.KeyAsync(Key.Enter, RawInputModifiers.Alt);
            Equal("Shell fixture failure.", c.Window.LastError, "Properties error");
            await ShellInputProbe.RightClickAsync(c, "alpha.txt");
            Equal("Shell fixture failure.", c.Window.LastError, "Menu error");
            Check(!c.Window.IsMenuOpen && !c.Window.IsBusy, "Failure leaves the application usable");
        }),
        new("shell-native-discovery", "Real Windows shell discovers file and directory commands without showing a menu", c =>
        {
            foreach (var name in new[] { "отчёт 日本語.txt", "folder1" })
            {
                var path = Path.Combine(c.LeftPath, name);
                // Record cold and warm legacy calls separately so first-use loading does
                // not get mistaken for the benefit of asynchronous command-state checks.
                var cold = WindowsShellInteraction.InspectMenu(path, asynchronousState: false);
                var warm = WindowsShellInteraction.InspectMenu(path, asynchronousState: false);
                var optimized = WindowsShellInteraction.InspectMenu(path);
                c.Measurements[name + ".legacyCold"] = cold;
                c.Measurements[name + ".legacyWarm"] = warm;
                c.Measurements[name + ".asyncState"] = optimized;
                var verbs = optimized.Verbs;
                Check(verbs.Contains("properties", StringComparer.OrdinalIgnoreCase), "Windows exposes properties for " + name);
                Check(verbs.Contains("open", StringComparer.OrdinalIgnoreCase), "Windows exposes open for " + name);
            }
            return Task.CompletedTask;
        })
    ];
}

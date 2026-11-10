using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.RuleProbe;

namespace Xcmd.Probe.Scenarios;

public static class RuleScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("rules-natural-order-number-runs", "UC13; numeric ordering and stable ties", _ =>
        {
            (string Left, string Right, int Order)[] pairs =
            [
                ("file2", "file10", -1), ("file02b", "file2a", 1),
                ("file01", "file1", -1), ("a000b", "a0b", -1),
                ("a", "A", 1), ("", "a", -1), ("same", "same", 0),
                ("file٢", "file2", 1), ("file" + new string('0', 300) + "2", "file9", -1)
            ];
            foreach (var (left, right, order) in pairs)
            {
                Equal(order, Math.Sign(PanelRules.NaturalCompare(left, right)), "Natural order: " + left + " / " + right);
                Equal(-order, Math.Sign(PanelRules.NaturalCompare(right, left)), "Reverse natural order");
            }
            return Task.CompletedTask;
        }),
        new("rules-name-validation-priority-and-devices", "UC17; Windows names and validation feedback", _ =>
        {
            foreach (var name in new[] { "CON", "con.txt", "PRN", "AUX.log", "NUL", "CONIN$", "CONOUT$.txt", "COM1", "com9.txt", "LPT¹", "COM².txt", "lpt³.data" })
                Equal("This name is reserved by Windows.", PanelRules.ValidateName(name), "Reserved name: " + name);
            foreach (var name in new[] { "COM0", "COM10", "LPT0", "CONSOLE", ".CON", "report 日本語.txt" })
                Equal<string?>(null, PanelRules.ValidateName(name), "Ordinary name: " + name);
            Equal("Enter a name.", PanelRules.ValidateName(new string(' ', 256)), "Whitespace takes priority over length");
            Equal("Use a single valid file name.", PanelRules.ValidateName(".."), "The parent name is rejected before the trailing dot");
            Equal("Use a single valid file name.", PanelRules.ValidateName(new string('x', 256) + ':'), "Invalid characters take priority over length");
            Equal("A name cannot end with a space or a dot.", PanelRules.ValidateName("CON."), "Trailing dots take priority over reserved names");
            Equal("A name cannot exceed 255 characters.", PanelRules.ValidateName(new string('x', 256)), "The length limit is preserved");
            return Task.CompletedTask;
        }),
        new("rules-command-parser-request-and-quotes", "UC02; command targets and raw arguments", _ =>
        {
            (string Text, CommandLineAction Action)[] cases =
            [
                ("  tool.exe first & second  ", new("tool.exe", "first & second", false)),
                ("\"tool name.exe\"  \"two words\"", new("tool name.exe", "\"two words\"", false)),
                ("tool \"unfinished", new("tool", "\"unfinished", false)),
                ("CD..", new("..", "", true)), ("cd/d folder", new("folder", "", true)),
                ("cd /D", new("", "", true)), ("cd /d \"space 日本語\"", new("space 日本語", "", true)),
                ("cd\u00a0folder", new("folder", "", true)), ("cd/foo", new("/foo", "", true)),
                ("cd-rom.exe", new("cd-rom.exe", "", false)), ("\"cd\"", new("cd", "", false))
            ];
            foreach (var (text, action) in cases) Equal(action, CommandLine.Parse(text), "Command request: " + text);
            foreach (var text in new[] { "\"tool", "\"\"", "\"tool\"next" })
                Rejects<ArgumentException>(() => CommandLine.Parse(text), "Close the program path quotes and separate arguments with a space.");
            Rejects<ArgumentException>(() => CommandLine.Parse("tool\targument"), "The command is too long or contains control characters.");
            Rejects<ArgumentException>(() => CommandLine.Parse("cd \"unfinished"), "Use one pair of quotes around the directory path.");
            return Task.CompletedTask;
        }),
        new("rules-workspace-panel-validation", "UC38; saved navigation and layout constraints", c =>
        {
            var state = c.Window.CaptureWorkspace();
            state.Validate();
            PanelState[] invalid =
            [
                state.Left with { DirectoryPath = "relative" }, state.Left with { Sort = new((SortField)999) },
                state.Left with { Selected = null! }, state.Left with { Cursors = null! },
                state.Left with { History = [null!] }, state.Left with { History = Enumerable.Repeat(c.LeftPath, 101).ToArray() },
                state.Left with { Columns = [null!] }, state.Left with { Columns = [new(double.NaN, false)] },
                state.Left with { ScrollX = -1 }, state.Left with { ScrollY = double.PositiveInfinity }
            ];
            foreach (var panel in invalid)
                Rejects<InvalidDataException>(() => (state with { Left = panel }).Validate(), "The saved panel state is invalid.");
            (state with { Left = state.Left with { History = Enumerable.Repeat(c.LeftPath, 100).ToArray(), Columns = [], Selected = [] } }).Validate();
            return Task.CompletedTask;
        }),
        new("rules-workspace-favorite-identity", "UC16; UC38; favorite identity and saved paths", c =>
        {
            var state = c.Window.CaptureWorkspace();
            var favorite = new FavoriteDirectory("favorite", "Work", c.LeftPath);
            (state with { Favorites = [favorite, favorite with { Id = "FAVORITE", Path = c.RightPath }] }).Validate();
            FavoriteDirectory[][] invalid =
            [
                [null!], [favorite with { Name = "invalid\nlabel" }], [favorite with { Id = "" }],
                [favorite with { Name = new string('x', 101) }], [favorite with { Path = "relative" }],
                [favorite, favorite with { Path = c.RightPath }],
                [favorite, favorite with { Id = "other", Path = c.LeftPath.ToUpperInvariant().Replace('\\', '/') + '/' }]
            ];
            foreach (var favorites in invalid)
                Rejects<InvalidDataException>(() => (state with { Favorites = favorites }).Validate(), "The saved favorites are invalid.");
            return Task.CompletedTask;
        }),
        new("rules-panel-shortcut-precedence", "UC13; UC14; UC17; combined keyboard modifiers", async c =>
        {
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.F6, RawInputModifiers.Control | RawInputModifiers.Shift);
            Check(c.Window.Left.EditingRow != null, "Shift+F6 rename takes priority over Ctrl+F6 sorting.");
            Equal(SortField.Name, c.Window.Left.Session.SortOrder.Field, "Rename keeps the sort order");
            await c.KeyAsync(Key.Escape);
            await c.KeyAsync(Key.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal("right", c.Window.ActivePanel.Id, "Tab switches panels with additional modifiers");
            await c.KeyAsync(Key.Tab);
            var revision = c.Window.Left.Session.Revision;
            await c.KeyAsync(Key.Left, RawInputModifiers.Control);
            Equal(revision, c.Window.Left.Session.Revision, "Opening the source panel in itself does not reload it");
            await c.SelectAsync("folder1");
            await c.KeyAsync(Key.Right, RawInputModifiers.Control | RawInputModifiers.Shift);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Right.Session.DirectoryPath, "Ctrl+Right opens the pointed directory with Shift held");
            Equal("left", c.Window.ActivePanel.Id, "Opposite-panel navigation keeps the source panel active");
            await c.KeyAsync(Key.Down, RawInputModifiers.Control | RawInputModifiers.Alt);
            Check(!c.Window.HasOpenDialog, "The panel history shortcut requires Alt without Control.");
        }),
        new("rules-navigation-shortcuts-and-editor-priority", "UC15; UC16; navigation input routing", async c =>
        {
            c.Window.Favorites.Add(c.LeftPath);
            await c.KeyAsync(Key.D, RawInputModifiers.Control);
            var dialog = c.Window.ActiveNavigationDialog!;
            await c.KeyAsync(Key.Down, RawInputModifiers.Control | RawInputModifiers.Alt, dialog);
            Equal(DirectoryList.History, dialog.ActiveList, "Alt+Down selects history even with Control held in the dialog");
            await c.KeyAsync(Key.D, RawInputModifiers.Control | RawInputModifiers.Alt, dialog);
            Equal(DirectoryList.Favorites, dialog.ActiveList, "Ctrl+D selects favorites with additional modifiers");
            dialog.NameEditor.Focus();
            dialog.NameEditor.Text = "Working files";
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control | RawInputModifiers.Shift, dialog);
            Equal("Working files", c.Window.Favorites.Items.Single().Name, "Enter saves the focused name editor with additional modifiers");
            Check(dialog.IsVisible, "Confirming the name editor does not open the directory or close the dialog.");
            await c.KeyAsync(Key.Escape, target: dialog);
        })
    ];
}

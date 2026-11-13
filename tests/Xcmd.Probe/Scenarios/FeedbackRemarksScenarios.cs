using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.FeedbackRemarksProbe;

namespace Xcmd.Probe.Scenarios;

public static class FeedbackRemarksScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("remarks-search-query-controls-and-history", "UC23; keep results while editing, Enter on options, field navigation and saved query history", async c =>
        {
            var search = await SearchProbe.OpenAsync(c, "alpha", "abc");
            var original = search.Session;
            var result = SearchProbe.Results(search).Single();
            Option(search, "SearchLiteralName").Focus();
            await c.KeyAsync(Key.Space, target: search);
            Equal(original, search.Session, "Changing an option keeps the current search");
            Equal(result, SearchProbe.Results(search).Single(), "Changing an option keeps results");
            await c.KeyAsync(Key.Enter, target: search);
            Check(!ReferenceEquals(original, search.Session), "Enter on a checkbox applies the changed query");
            Check(Option(search, "SearchLiteralName").IsChecked == true, "Enter does not toggle the checkbox");
            search.StartSearch(); var running = search.Completion; search.StartSearch();
            Check(ReferenceEquals(running, search.Completion), "Search does not restart an unchanged running query");
            Option(search, "SearchMatchCase").IsChecked = true;
            Check(ReferenceEquals(running, search.Completion), "Changing options does not cancel active work");
            await c.SettleAsync();
            search.NameEditor.Text = "zeta"; search.TextEditor.Text = "notes"; search.StartSearch(); await c.SettleAsync();
            await c.KeyAsync(Key.F, RawInputModifiers.Alt, search);
            Check(search.NameEditor.IsKeyboardFocusWithin, "Alt+F selects the name field");
            await c.KeyAsync(Key.Down, target: search); Check(search.TextEditor.IsKeyboardFocusWithin, "Down switches query fields");
            await c.KeyAsync(Key.Up, target: search); Check(search.NameEditor.IsKeyboardFocusWithin, "Up switches query fields");
            await c.KeyAsync(Key.T, RawInputModifiers.Alt, search);
            Check(search.TextEditor.IsKeyboardFocusWithin, "Alt+T selects the text field");
            await c.KeyAsync(Key.Down, RawInputModifiers.Alt, search); await c.KeyAsync(Key.Down, target: search); await c.KeyAsync(Key.Enter, target: search);
            Equal("abc", search.TextEditor.Text, "Alt+Down opens history; arrows and Enter recall the previous text");
            Equal("zeta.md", SearchProbe.Results(search).Single().Entry.Name, "History recall keeps old results until Search");
            var saved = c.Window.CaptureWorkspace();
            var store = new SessionStore(Path.Combine(c.FixturePath, "search-history.json")); await store.SaveAsync(saved);
            Check((await store.ReadAsync())!.FileSearch!.NameHistory!.Contains("alpha", StringComparer.Ordinal), "Name history is saved with the workspace");
            search.Close(); c.Window.FileSearch.Restore((await store.ReadAsync())!.FileSearch); c.Window.FileSearch.Open(); await c.SettleAsync();
            Check(c.Window.FileSearch.Window!.CaptureState().TextHistory!.Contains("notes", StringComparer.Ordinal), "Text history survives window recreation");
        }),
        new("remarks-search-compact-preview-and-exclusions", "UC23; separate preview column, long-line literal search and visible ignore reasons", async c =>
        {
            await SearchProbe.GitAsync(c.LeftPath, "init", "--quiet");
            await SearchProbe.GitAsync(c.LeftPath, "config", "core.excludesFile", Path.Combine(c.FixturePath, "empty-excludes"));
            SearchProbe.TextFile(c, ".gitignore", "ignored/\n*.tmp\n");
            SearchProbe.TextFile(c, "ignored/hidden.txt", "needle"); SearchProbe.TextFile(c, "hidden.tmp", "needle");
            SearchProbe.TextFile(c, "node_modules/hidden.txt", "needle");
            SearchProbe.TextFile(c, "long.txt", new string('a', 350_000) + "needle\n");
            var search = await SearchProbe.OpenAsync(c, "", "needle");
            Equal("long.txt", SearchProbe.Results(search).Single().Entry.Name, "Literal search includes matches after a very long line prefix");
            Equal(0, search.Session!.Failures.Count, "Long lines are not errors in plain text search");
            CheckCompactResults(search);
            var locations = search.Session.DeferredLocations;
            Check(locations.Any(location => location.Reason == "Git ignored" && !location.Directory && location.Rule!.Contains("*.tmp", StringComparison.Ordinal)), "Ignored files show the exact Git rule");
            Check(locations.Any(location => location.Path.EndsWith("node_modules", StringComparison.Ordinal) && location.Reason.StartsWith("Dependencies", StringComparison.Ordinal)), "Dependency folders have a separate reason");
            await c.ClickAsync(search.GetVisualDescendants().OfType<Expander>().Single(control => control.Name == "SearchDeferredDetails"), target: search);
            await c.ScreenshotAsync("search-compact-preview-and-scope", search);
        }),
        new("remarks-viewer-search-memory", "UC10; reuse query across files and viewers, with selected Find text", async c =>
        {
            c.AddFile("first.txt", "First needle sample"); c.AddFile("second.txt", "Second needle sample");
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("first.txt"); await c.KeyAsync(Key.F3);
            var first = c.Window.Viewer!;
            await ViewerProbe.StartSearchAsync(c, first, "needle");
            await c.KeyAsync(Key.Right, RawInputModifiers.Alt, first);
            Equal("second.txt", Path.GetFileName(first.CurrentPath), "Viewer navigation changes files");
            await c.KeyAsync(Key.F3, target: first);
            Equal("needle", first.TextContent.SelectedText, "F3 reuses the previous file's query");
            await c.KeyAsync(Key.F, RawInputModifiers.Control, first);
            Equal("needle", first.Search.SelectedText, "Find selects the previous query for replacement");
            first.Close(); await c.SettleAsync();
            await c.SelectAsync("first.txt"); await c.KeyAsync(Key.F3);
            var second = c.Window.Viewer!; await c.KeyAsync(Key.F3, target: second);
            Equal("needle", second.TextContent.SelectedText, "New viewers reuse the last query");
            await c.KeyAsync(Key.F, RawInputModifiers.Control, second); await c.TextAsync("First", second);
            Equal("First", second.Search.Text, "Typing replaces the selected query");
        }),
        new("remarks-native-icons-and-directory-shortcuts", "UC14; native icon pixels, root shortcut and physical home shortcut in both layouts", async c =>
        {
            await CheckNativeIconsAsync(c);
            c.FileSystem.SupplyDirectory(Path.GetPathRoot(c.LeftPath)!, []);
            c.FileSystem.SupplyDirectory(UserDirectories.Home, []);
            var opposite = c.Window.Right.Session.DirectoryPath;
            await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "folder1"));
            PhysicalShortcut(c, Key.OemBackslash, PhysicalKey.Backslash, "\\"); await c.SettleAsync();
            Equal(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(c.LeftPath)!), c.Window.Left.Session.DirectoryPath, "Ctrl+Backslash opens the current drive root");
            foreach (var symbol in new[] { "`", "ё" })
            {
                await c.Window.Left.LoadAsync(c.LeftPath);
                PhysicalShortcut(c, Key.OemTilde, PhysicalKey.Backquote, symbol); await c.SettleAsync();
                Equal(UserDirectories.Home, c.Window.Left.Session.DirectoryPath, "The physical home shortcut works in either layout");
            }
            Equal(opposite, c.Window.Right.Session.DirectoryPath, "Directory shortcuts preserve the opposite panel");
        }),
        new("remarks-feedback-open-and-resolved-tabs", "UC39; open notes first, resolved notes in a separate tab", async c =>
        {
            await c.Window.Feedback.Store!.AddAsync(new("closed", FeedbackIssue.Closed, "Closed comment", new("closed", "Closed item"), "test"));
            await c.Window.Feedback.Store.AddAsync(new("open", FeedbackIssue.New, "Open comment", new("open", "Open item"), "test"));
            await c.Window.Feedback.RefreshAsync();
            c.Window.Feedback.ShowNotes(); await c.SettleAsync();
            var window = c.Window.Feedback.ActiveNotes!;
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            Equal(0, tabs.SelectedIndex, "Notes starts in the Open tab");
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            Equal("open", list.ItemsSource!.Cast<FeedbackIssue>().Single().Id, "Open notes do not contain resolved comments");
            tabs.SelectedIndex = 1; await c.SettleAsync();
            list = window.GetVisualDescendants().OfType<ListBox>().Single();
            Equal("closed", list.ItemsSource!.Cast<FeedbackIssue>().Single().Id, "Resolved comments have their own tab");
            list.SelectedIndex = 0; await c.ClickAsync(Button(window, "RemoveNote"), target: window);
            Check(c.Window.Feedback.Notes.All(note => note.Id != "closed"), "Remove applies to the selected tab");
            await c.ScreenshotAsync("feedback-tabs", window);
        })
    ];
}

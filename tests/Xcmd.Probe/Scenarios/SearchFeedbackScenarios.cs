using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.SearchFeedbackProbe;

namespace Xcmd.Probe.Scenarios;

public static class SearchFeedbackScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("search-feedback-resize-whole-name-and-skipped-table", "UC23; resizable saved columns, whole-name matching, compact skipped paths and simple status", async c =>
        {
            await SearchProbe.GitAsync(c.LeftPath, "init", "--quiet");
            await SearchProbe.GitAsync(c.LeftPath, "config", "core.excludesFile", Path.Combine(c.FixturePath, "empty-excludes"));
            SearchProbe.TextFile(c, ".gitignore", "*.tmp\n"); SearchProbe.TextFile(c, "hidden.tmp", "needle");
            SearchProbe.TextFile(c, "node_modules/hidden.txt", "needle");
            c.AddFile("report.txt", "needle"); c.AddFile("old-report.txt", "needle"); c.AddFile("report.txt.bak", "needle");
            var search = await SearchProbe.OpenAsync(c, "report.txt", "needle");
            Equal(3, SearchProbe.Results(search).Length, "Part-name matching remains the default");
            search.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "SearchNameMode").SelectedIndex = 1;
            search.StartSearch(); await c.SettleAsync();
            Equal("report.txt", SearchProbe.Results(search).Single().Entry.Name, "Whole name matches exactly without adding wildcards");
            search.NameEditor.Text = "*.txt"; search.StartSearch(); await c.SettleAsync();
            Equal(2, SearchProbe.Results(search).Length, "A wildcard mask matches the whole name");
            var before = search.Results.Columns[1].ActualWidth;
            await ResizeColumnAsync(c, search, search.Results, 1, 40);
            Check(search.Results.Columns[1].ActualWidth > before + 20, "The real Line separator changes its width");
            var root = search.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SearchRoot");
            Check(root.TranslatePoint(default, search)!.Value.Y <= 16, "The search path starts near the top without an empty feedback row");
            Check(!search.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Names match any part", StringComparison.Ordinal) == true), "The redundant help line is removed");
            Check(search.StatusMessage.StartsWith("Complete", StringComparison.Ordinal) && search.StatusMessage.Contains("skipped", StringComparison.Ordinal), "The status uses Complete and skipped");
            Equal("Search skipped files", FeedbackRemarksProbe.Button(search, "SearchExpand").Content?.ToString(), "The action uses the same skipped term");
            await c.ClickAsync(search.GetVisualDescendants().OfType<Expander>().Single(control => control.Name == "SearchDeferredDetails"), target: search);
            var skipped = search.GetVisualDescendants().OfType<TableView>().Single(table => table.Name == "SearchDeferredLocations");
            Equal(3, skipped.Columns.Count, "Skipped locations separate Reason, Path and rule source");
            Check(skipped.GetVisualDescendants().OfType<TableViewRow>().All(row => row.Bounds.Height <= 27), "Skipped rows are compact");
            Check(!skipped.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("contents not counted", StringComparison.Ordinal) == true), "The repeated folder note is not in each row");
            await ResizeColumnAsync(c, search, skipped, 0, -30);
            var expected = search.CaptureState();
            var store = new SessionStore(Path.Combine(c.FixturePath, "search-columns.json")); await store.SaveAsync(c.Window.CaptureWorkspace());
            search.Close(); c.Window.FileSearch.Restore((await store.ReadAsync())!.FileSearch); c.Window.FileSearch.Open(); await c.SettleAsync();
            search = c.Window.FileSearch.Window!;
            Check(expected.ResultColumns!.SequenceEqual(search.CaptureState().ResultColumns!) && expected.SkippedColumns!.SequenceEqual(search.CaptureState().SkippedColumns!), "Both tables restore their saved column widths");
            Check(search.CaptureState().WholeName, "Whole-name mode is saved");
            search.StartSearch(); await c.SettleAsync();
            await c.ClickAsync(search.GetVisualDescendants().OfType<Expander>().Single(control => control.Name == "SearchDeferredDetails"), target: search);
            await c.ScreenshotAsync("search-resizable-tables", search);
        }),
        new("search-feedback-independent-single-window", "UC23; independent search window, live panel actions and Alt+F7 reuse", async c =>
        {
            var search = await SearchProbe.OpenAsync(c, "alpha");
            var session = search.Session; var results = SearchProbe.Results(search);
            Check(search.Owner == null && !search.Topmost, "Search has no owner that keeps it above the main window");
            Check(!c.Window.HasOpenDialog && !c.Window.Readiness.CanRestart, "Search allows panel commands while automatic replacement still waits");
            c.Window.Activate(); c.Window.Left.FocusCurrent();
            await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter);
            Equal(Path.Combine(c.LeftPath, "folder1"), c.Window.Left.Session.DirectoryPath, "Panel Enter navigates while search remains open");
            await c.KeyAsync(Key.F7);
            var folderDialog = c.Window.ActiveDialog!;
            Check(folderDialog != null, "Main-window dialogs remain available");
            await c.TextAsync("created-during-search", folderDialog); await c.KeyAsync(Key.Enter, target: folderDialog);
            Check(Directory.Exists(Path.Combine(c.LeftPath, "folder1", "created-during-search")), "The normal folder command changes real fixture files");
            search.WindowState = WindowState.Minimized;
            c.Window.Left.FocusCurrent(); await c.KeyAsync(Key.F7, RawInputModifiers.Alt);
            Check(ReferenceEquals(search, c.Window.FileSearch.Window) && search.WindowState == WindowState.Normal, "Alt+F7 restores the existing search window");
            Check(ReferenceEquals(session, search.Session) && results.SequenceEqual(SearchProbe.Results(search)), "Returning to search keeps the query, work and results");
        }),
        new("search-feedback-alt-name-position-priority", "UC03; substring name jumping, earliest position and stable display-order ties", async c =>
        {
            c.AddFile("a-target-end.txt", "a"); c.AddFile("target-start.txt", "b");
            c.AddFile("a-needle.txt", "c"); c.AddFile("b-needle.txt", "d"); c.AddFile("longer-needle.txt", "e");
            await c.Window.Left.LoadAsync(c.LeftPath);
            await JumpAsync(c, "target");
            Equal("target-start.txt", c.Window.Left.Current?.Name, "A prefix match outranks an earlier visible middle match");
            await c.TextAsync("-end");
            Equal("a-target-end.txt", c.Window.Left.Current?.Name, "Continued typing can select a middle match");
            await JumpAsync(c, "needle");
            Equal("a-needle.txt", c.Window.Left.Current?.Name, "The leftmost occurrence wins; equal positions use the top row");
            var jump = new NameJump(); jump.Begin();
            Equal("xxneedle", jump.Append("needle", ["needle"], ["aaaaneedle", "xxxneedle", "xxneedle", "yyneedle"]), "Rule ranking compares positions before row order");
            Equal<string?>(null, jump.Append("!", ["needle!"], ["xxneedle"]), "Unmatched input is ignored");
            Equal("needle", jump.Prefix, "Rejected input does not change the query");
        })
    ];
}

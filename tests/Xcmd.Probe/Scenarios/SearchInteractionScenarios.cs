using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.SearchInteractionProbe;

namespace Xcmd.Probe.Scenarios;

public static class SearchInteractionScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("search-interaction-alt-highlight-and-cycle", "UC03; highlighted name matches, arrow cycling, Escape and command reset", async c =>
        {
            c.AddFile("aa-needle.txt", "a"); c.AddFile("bb-needle.txt", "b"); c.AddFile("needle-first.txt", "c");
            await c.Window.Left.LoadAsync(c.LeftPath);
            await SearchFeedbackProbe.JumpAsync(c, "needle");
            Equal("needle-first.txt", c.Window.Left.Current?.Name, "The initial match still gives priority to a name prefix");
            foreach (var name in new[] { "aa-needle.txt", "bb-needle.txt", "needle-first.txt" }) CheckHighlight(c, name, "needle");
            await c.ScreenshotAsync("alt-name-highlights");
            await c.KeyAsync(Key.Down); Equal("aa-needle.txt", c.Window.Left.Current?.Name, "Down wraps to the next matching row");
            await c.KeyAsync(Key.Down); Equal("bb-needle.txt", c.Window.Left.Current?.Name, "Down skips unrelated rows");
            await c.KeyAsync(Key.Up); Equal("aa-needle.txt", c.Window.Left.Current?.Name, "Up moves to the previous match");
            Equal("needle", c.Window.NameSearchPrefix, "Arrows keep the query");
            await c.KeyAsync(Key.Escape);
            Check(!c.Window.IsNameSearchActive && c.Window.Left.Rows.All(row => row.NameMatchLength == 0), "Escape clears query and highlighting");
            await SearchFeedbackProbe.JumpAsync(c, "needle"); await c.KeyAsync(Key.Tab);
            Check(!c.Window.IsNameSearchActive && c.Window.Left.Rows.All(row => row.NameMatchLength == 0), "Tab clears highlighting before switching panels");
            await c.KeyAsync(Key.Tab); await SearchFeedbackProbe.JumpAsync(c, "needle"); await c.KeyAsync(Key.R, RawInputModifiers.Control);
            Check(!c.Window.IsNameSearchActive && c.Window.Left.Rows.All(row => row.NameMatchLength == 0), "Another command clears the query and highlighting");
        }),
        new("search-interaction-focus-and-query-group", "UC23; focus results after Search, arrow boundary navigation and name mode grouping", async c =>
        {
            c.AddFile("focus-a.txt", "needle"); c.AddFile("focus-b.txt", "needle");
            var search = await SearchProbe.OpenAsync(c, "focus-", "needle");
            Check(search.Results.IsKeyboardFocusWithin, "Search moves keyboard focus to results");
            await c.KeyAsync(Key.Down, target: search); Equal(1, search.Results.SelectedIndex, "Down immediately navigates matching files");
            await c.KeyAsync(Key.Up, target: search); Equal(0, search.Results.SelectedIndex, "Up moves to the first result");
            Check(search.Results.IsKeyboardFocusWithin, "Up within results keeps list focus");
            await c.KeyAsync(Key.Up, target: search); Check(search.TextEditor.IsKeyboardFocusWithin, "Up from the first row returns to Containing text");
            await c.KeyAsync(Key.Down, target: search); Check(search.Results.IsKeyboardFocusWithin, "Down from Containing text returns to results");
            var mode = search.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "SearchNameMode");
            var nameTop = search.NameEditor.TranslatePoint(default, search)!.Value;
            var modeTop = mode.TranslatePoint(default, search)!.Value;
            Check(Math.Abs(nameTop.Y - modeTop.Y) < 4 && modeTop.X > nameTop.X, "Name matching mode sits beside the name field");
            var start = FeedbackRemarksProbe.Button(search, "SearchStart");
            Check(start.TranslatePoint(default, search)!.Value.Y >= search.TextEditor.TranslatePoint(default, search)!.Value.Y + search.TextEditor.Bounds.Height,
                "Search appears below both query fields");
            await c.ScreenshotAsync("search-grouped-query", search);
            search.NameEditor.Text = "focus-a.txt"; mode.SelectedIndex = 1; mode.Focus();
            await c.KeyAsync(Key.Enter, target: search);
            Equal("focus-a.txt", SearchProbe.Results(search).Single().Entry.Name, "Enter applies the query from the closed name-mode list");
            Check(search.Results.IsKeyboardFocusWithin, "Applying the name mode returns focus to results");
        }),
        new("search-interaction-detail-tables-and-location-search", "UC23; typed skip/error tables and double-click scope to folders, files and nested archives", async c =>
        {
            await SearchProbe.GitAsync(c.LeftPath, "init", "--quiet");
            await SearchProbe.GitAsync(c.LeftPath, "config", "core.excludesFile", System.IO.Path.Combine(c.FixturePath, "empty-excludes"));
            SearchProbe.TextFile(c, ".gitignore", "ignored/\n*.tmp\n");
            SearchProbe.TextFile(c, "ignored/inside.txt", "needle"); SearchProbe.TextFile(c, "hidden.tmp", "needle");
            c.AddFile("visible.txt", "needle"); ArchiveProbe.Nested(System.IO.Path.Combine(c.LeftPath, "outer.zip"));
            var search = await SearchProbe.OpenAsync(c, "", "needle");
            var skippedExpander = search.GetVisualDescendants().OfType<Expander>().Single(control => control.Name == "SearchDeferredDetails");
            var failedExpander = search.GetVisualDescendants().OfType<Expander>().Single(control => control.Name == "SearchFailureDetails");
            skippedExpander.IsExpanded = true; failedExpander.IsExpanded = true; await c.SettleAsync();
            var skipped = Table(search, "SearchDeferredLocations"); var failed = Table(search, "SearchFailedLocations");
            Check(ToolTip.GetTip(skipped)?.ToString()?.Contains("replace current results", StringComparison.Ordinal) == true &&
                ToolTip.GetTip(failed)?.ToString()?.Contains("replace current results", StringComparison.Ordinal) == true,
                "Both detail tables explain the effect of double-click");
            Check(skipped.Columns.Select(column => column.Header?.ToString()).SequenceEqual(new[] { "Reason", "Path", "Rule / source" }), "Skipped rows separate the short reason and rule source");
            Check(failed.Columns.Select(column => column.Header?.ToString()).SequenceEqual(new[] { "Reason", "Path", "Explanation" }), "Errors use table columns instead of multiline items");
            var failure = search.Session!.Failures.Single(item => item.Path.EndsWith("inner.zip", StringComparison.Ordinal));
            Equal("Archive", failure.Reason, "Archive issues have a short reason token");
            var ignored = search.Session.DeferredLocations.Single(item => item.Path.EndsWith("ignored", StringComparison.Ordinal));
            Check(ignored.Rule!.Contains(System.IO.Path.Combine(c.LeftPath, ".gitignore"), StringComparison.Ordinal), "The rule source includes the exact ignore-file location");
            await c.ScreenshotAsync("search-detail-tables", search);
            await DoubleClickRowAsync(c, search, skipped, ignored);
            Equal("inside.txt", SearchProbe.Results(search).Single().Entry.Name, "Double-click searches inside the selected ignored folder");
            var note = await FeedbackProbe.CommentAsync(c, search, search.Results.ContainerFromIndex(0)!, "Find files: Search result: inside.txt");
            Equal("search.SearchResults.row", note.ElementId, "Search table feedback does not use the main panel row identifier");
            Equal(ignored.Path, note.PanelPath, "Search feedback records the scoped search directory");
            search.SearchLocation(c.LeftPath, false); await c.SettleAsync(); skippedExpander.IsExpanded = true; await c.SettleAsync();
            var file = search.Session!.DeferredLocations.Single(item => item.Path.EndsWith("hidden.tmp", StringComparison.Ordinal));
            await DoubleClickRowAsync(c, search, skipped, file);
            Equal("hidden.tmp", SearchProbe.Results(search).Single().Entry.Name, "Double-click on a file searches only that file");
            search.SearchLocation(c.LeftPath, false); await c.SettleAsync(); failedExpander.IsExpanded = true; await c.SettleAsync();
            search.TextEditor.Text = "Nested";
            failure = search.Session!.Failures.Single(item => item.Path.EndsWith("inner.zip", StringComparison.Ordinal));
            await DoubleClickRowAsync(c, search, failed, failure);
            Equal("nested.txt", SearchProbe.Results(search).Single().Entry.Name, "Double-click on a nested-archive issue searches that archive as the new root");
            Check(search.CaptureState().FailureColumns?.Length == 3, "Error-table column widths are captured");
            var oldState = c.Window.CaptureWorkspace() with { FileSearch = new FileSearchState(SkippedColumns: [new(3, true), new(2, true)]) };
            oldState.Validate();
        })
    ];
}

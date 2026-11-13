using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Desktop;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class GitRowsScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("git-rows-file-states", "Git column distinguishes clean, staged, modified, added, untracked, renamed and conflicted paths", async c =>
        {
            try
            {
                await GitRowsFixture.CreateAsync(c, conflict: true);
                var rows = c.Window.Left.Rows.ToDictionary(row => row.Name);
                Equal("●", rows["alpha.txt"].Git.WorkingGlyph, "An unstaged modification has a working-tree icon");
                Equal("", rows["alpha.txt"].Git.IndexGlyph, "An unstaged modification has no staged icon");
                Equal("✓", rows["zeta.md"].Git.IndexGlyph, "A staged modification has a staged icon");
                Equal("●", rows["zeta.md"].Git.WorkingGlyph, "A later edit remains visible beside the staged icon");
                Equal("+", rows["added 日本語.txt"].Git.IndexGlyph, "A staged new file has an added icon");
                Equal("?", rows["loose.txt"].Git.WorkingGlyph, "An untracked file has its own icon");
                Equal("→", rows["renamed file 日本語.txt"].Git.IndexGlyph, "NUL-delimited rename paths retain spaces and Unicode");
                Check(!rows["file10.bin"].Git.HasChanges, "An unchanged file has no icons");
                Equal("!", rows["folder1"].Git.IndexGlyph, "A folder containing a conflict attracts attention");
                Equal("−", rows["deleted-folder"].Git.WorkingGlyph, "A deleted child is visible on its containing folder");
                Equal("→", rows["old-folder"].Git.IndexGlyph, "The source folder of a rename remains marked");
                var cell = c.Row("zeta.md").GetVisualDescendants().OfType<Grid>().Single(control => control.GetValue(FeedbackTarget.IdProperty) == "PanelView.gitCell");
                Check(cell.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).SequenceEqual(new[] { "✓", "●" }), "The actual cell renders both independent states");
                Check(ToolTip.GetTip(cell)?.ToString()?.Contains("Staged: modified") == true, "The cell explains its icons in English");
                foreach (var scale in new[] { 1d, 1.5d })
                {
                    c.Window.SetScaleForTest(scale); await c.ScreenshotAsync("git-file-icons-" + scale * 100);
                }
                await c.SelectAsync("folder1"); await c.KeyAsync(Key.Enter);
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                Equal("!", c.Window.Left.Rows.Single(row => row.Name == "inside.txt").Git.IndexGlyph, "The actual conflicted file has a conflict icon");
            }
            finally { GitFixture.AllowCleanup(c.LeftPath); }
        }),
        new("git-rows-subfolders-and-refresh", "Folder cells aggregate descendants while native icons stay unchanged before and after commit", async c =>
        {
            try
            {
                await GitRowsFixture.CreateAsync(c);
                var ignored = c.Window.Left.Rows.Single(row => row.Name == "ignored");
                foreach (var name in new[] { "dirty", "clean-folder", "ignored" })
                {
                    Equal("DIR", c.Window.Left.Rows.Single(row => row.Name == name).Kind, "Folders keep their ordinary type");
                    var row = c.Row(name);
                    Check(row.GetVisualDescendants().OfType<Image>().Single().IsVisible, "The native Windows folder image is always visible");
                    Check(!row.GetVisualDescendants().OfType<PathIcon>().Any(), "No coloured replacement folder icon remains");
                }
                Check(!ignored.Git.HasChanges, "An ignored folder has an empty Git cell");
                await c.SelectAsync("new-tree"); await c.KeyAsync(Key.Enter); await GitFixture.RefreshAsync(c.Window.Left.Git);
                Equal("?", c.Window.Left.Rows.Single(row => row.Name == "loose.txt").Git.WorkingGlyph, "Files within a collapsed untracked directory inherit its untracked state");
                Check(!c.Window.Left.Rows.Single(row => row.Name == "ignored.cache").Git.HasChanges, "Ignored descendants do not inherit untracked markers");
                await c.KeyAsync(Key.Back); await GitFixture.RefreshAsync(c.Window.Left.Git);
                await c.SelectAsync("dirty"); await c.KeyAsync(Key.Enter); await GitFixture.RefreshAsync(c.Window.Left.Git);
                Equal("●", c.Window.Left.Rows.Single(row => row.Name == "nested").Git.WorkingGlyph, "Navigation retains the status of a deeper descendant");
                await GitFixture.RunAsync(c.LeftPath, "add", "."); await GitFixture.RunAsync(c.LeftPath, "commit", "-m", "all changes");
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                var nested = c.Window.Left.Rows.Single(row => row.Name == "nested");
                Check(!nested.Git.HasChanges, "A refreshed clean snapshot clears both icon slots");
                Check(c.Row("nested").GetVisualDescendants().OfType<Image>().Single().IsVisible, "Committing does not replace the native folder icon");
                await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "ignored")); await GitFixture.RefreshAsync(c.Window.Left.Git); await c.SettleAsync();
                Check(!c.Window.Left.Table.Columns[3].IsVisible && c.Window.Left.Rows.All(row => !row.Git.HasChanges), "Entering an ignored folder hides irrelevant Git statuses");
                await c.Window.Left.LoadAsync(c.FixturePath); await GitFixture.RefreshAsync(c.Window.Left.Git); await c.SettleAsync();
                Equal("DIR", c.Window.Left.Rows.Single(row => row.Name == "left").Kind, "A repository root also keeps the ordinary directory type");
                var repository = c.Row("left");
                Check(repository.GetVisualDescendants().OfType<Image>().Single().IsVisible && !repository.GetVisualDescendants().OfType<PathIcon>().Any(), "Repository roots use only their native Windows icon");
            }
            finally { GitFixture.AllowCleanup(c.LeftPath); }
        }),
        new("git-rows-layout-and-session-migration", "Git column keeps old column preferences, survives session restore and fits a narrow panel", async c =>
        {
            try
            {
                await GitRowsFixture.CreateAsync(c);
                var panel = c.Window.Left;
                var state = panel.CaptureState() with { Columns = [new(3, true), new(97, false), new(144, false), new(1, true)] };
                await panel.RestoreStateAsync(state); await GitFixture.RefreshAsync(panel.Git); await c.SettleAsync();
                Equal(97d, panel.Table.Columns[1].Width.Value, "A four-column session keeps its type width");
                Equal(144d, panel.Table.Columns[2].Width.Value, "A four-column session keeps its size width");
                Equal(56d, panel.Table.Columns[3].Width.Value, "The new Git column receives its compact default");
                Check(panel.Table.Columns[^1].Width.IsStar, "The modified column retains its final stretch position");
                var store = new SessionStore(Path.Combine(c.FixturePath, "columns.json"));
                await store.SaveAsync(c.Window.CaptureWorkspace());
                Equal(5, (await store.ReadAsync())!.Left.Columns.Length, "The new session persists all five column preferences");
                panel.ResetColumns();
                await c.SelectAsync("alpha.txt");
                foreach (var scale in new[] { 1d, 1.5d })
                {
                    c.Window.SetScaleForTest(scale);
                    foreach (var width in new[] { 1280d, 840d })
                    {
                        c.Window.Width = width; await c.SettleAsync();
                        Check(panel.Table.Columns[3].IsVisible, "The Git icons remain visible at the minimum window width");
                        var scroll = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().First();
                        Check(scroll.Extent.Width <= scroll.Viewport.Width + 1, "The new column does not cause horizontal overflow");
                        Equal("alpha.txt", panel.Current?.Name, "Column adaptation preserves the file cursor");
                        await c.ScreenshotAsync("git-columns-" + width + "-" + scale * 100);
                    }
                }
            }
            finally { GitFixture.AllowCleanup(c.LeftPath); }
        })
    ];
}

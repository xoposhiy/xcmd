using Avalonia.Media;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class GitIgnoreScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("git-ignore-indicators", "Ignored files have a faint marker and tracked files matching ignore rules have a warning", async c =>
        {
            try
            {
                await GitRowsFixture.CreateAsync(c);
                File.WriteAllText(Path.Combine(c.LeftPath, "committed.cache"), "tracked");
                await GitFixture.RunAsync(c.LeftPath, "add", "-f", "committed.cache");
                await GitFixture.RunAsync(c.LeftPath, "commit", "-m", "tracked ignored fixture");
                File.WriteAllText(Path.Combine(c.LeftPath, "staged.cache"), "staged");
                File.WriteAllText(Path.Combine(c.LeftPath, "ordinary.cache"), "ignored");
                File.WriteAllText(Path.Combine(c.LeftPath, "ignored-note.txt"), "ignored text");
                File.AppendAllText(Path.Combine(c.LeftPath, ".gitignore"), "ignored-note.txt\n");
                Directory.CreateDirectory(Path.Combine(c.LeftPath, "ignored-folder.cache", "nested"));
                File.WriteAllText(Path.Combine(c.LeftPath, "ignored-folder.cache", "nested", "contents.txt"), "ignored descendant");
                File.WriteAllText(Path.Combine(c.LeftPath, "ignored", "tracked.txt"), "tracked child");
                await GitFixture.RunAsync(c.LeftPath, "add", "-f", "staged.cache", "ignored/tracked.txt");
                await c.Window.Left.LoadAsync(c.LeftPath);
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                var rows = c.Window.Left.Rows.ToDictionary(row => row.Name);
                Equal("·", rows["ordinary.cache"].Git.WorkingGlyph, "An ignored file has a quiet dot");
                Check(!rows["ordinary.cache"].Git.HasChanges, "Ignore indicators do not count as modifications");
                Equal("!", rows["committed.cache"].Git.WorkingGlyph, "A clean committed ignored file has a warning");
                Equal("!", rows["staged.cache"].Git.WorkingGlyph, "A force-added ignored file has a warning");
                Equal("+", rows["staged.cache"].Git.IndexGlyph, "The staged status remains visible beside the warning");
                Equal("!", rows["ignored"].Git.WorkingGlyph, "A folder reports tracked ignored descendants");
                Check(rows["committed.cache"].Git.Description.Contains("Warning:", StringComparison.Ordinal), "The warning explains that Git still tracks the file");
                Check(!Equals(rows["ordinary.cache"].Git.WorkingBrush, rows["committed.cache"].Git.WorkingBrush), "Ignored and suspicious files use distinct colors");
                var folder = rows["ignored-folder.cache"];
                var ignoredColor = folder.NameForeground;
                Check(!Equals(ignoredColor, rows["clean-folder"].NameForeground), "Ignored folder names are gray");
                Equal(ignoredColor, rows["ordinary.cache"].NameForeground, "Ignored files and folders use the same gray name color");
                Equal(ignoredColor, rows["ignored-note.txt"].NameForeground, "Ignored text files have gray names");
                Equal(Color.Parse("#8DD3E8"), NavigationProbe.TypeColor(c, "ignored-note.txt"), "Ignored text files keep the text color in the actual Type cell");
                Check(!Equals(ignoredColor, rows["committed.cache"].NameForeground), "Tracked ignored files retain their normal emphasis");
                Check(!Equals(ignoredColor, rows["ignored"].NameForeground), "Folders with tracked ignored files keep their normal emphasis");
                folder.SetMarked(true);
                Check(!Equals(ignoredColor, folder.NameForeground), "Marked ignored folders keep the selection color");
                folder.SetMarked(false); folder.SetCurrent(true, true);
                Equal(Brushes.White, folder.NameForeground, "The current ignored folder remains readable on the cursor");
                folder.SetCurrent(false, true);
                await c.SelectAsync("ignored-note.txt");
                Equal(Brushes.White, rows["ignored-note.txt"].NameForeground, "The current ignored file remains readable on the cursor");
                Equal(Color.Parse("#8DD3E8"), NavigationProbe.TypeColor(c, "ignored-note.txt"), "The current ignored file keeps its type color");
                await c.SelectAsync("ordinary.cache");
                foreach (var scale in new[] { 1d, 1.5d })
                {
                    c.Window.SetScaleForTest(scale);
                    await c.ScreenshotAsync("git-ignore-indicators-" + scale * 100);
                }
                await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "ignored"));
                Equal(ignoredColor, c.Window.Left.Rows.Single(row => row.Name == "generated.bin").NameForeground, "Ignored file names are gray before waiting for Git refresh");
                Equal("!", c.Window.Left.Rows.Single(row => row.Name == "tracked.txt").Git.WorkingGlyph, "Tracked warnings are available on the first display");
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                Equal("·", c.Window.Left.Rows.Single(row => row.Name == "generated.bin").Git.WorkingGlyph, "Ignored folder contents inherit the quiet marker");
                Equal("!", c.Window.Left.Rows.Single(row => row.Name == "tracked.txt").Git.WorkingGlyph, "Tracked children retain the warning inside ignored folders");
                await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "ignored-folder.cache"));
                Equal(ignoredColor, c.Window.Left.Rows.Single(row => row.Name == "nested").NameForeground, "Ignored subfolders are gray on the first display");
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                await c.SelectAsync("..");
                Equal(ignoredColor, c.Window.Left.Rows.Single(row => row.Name == "nested").NameForeground, "Nested folders inherit the ignored gray name");
                File.WriteAllText(Path.Combine(c.LeftPath, ".gitignore"), "ignored/\n*.cache\n!ignored-folder.cache/\n");
                await GitFixture.RefreshAsync(c.Window.Left.Git);
                Check(!Equals(ignoredColor, c.Window.Left.Rows.Single(row => row.Name == "nested").NameForeground), "Changing ignore rules immediately restores the normal folder color");
            }
            finally { GitFixture.AllowCleanup(c.LeftPath); }
        })
    ];
}

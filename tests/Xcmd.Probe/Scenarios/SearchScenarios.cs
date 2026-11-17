// @tests FindFiles, FindFileText, PrioritizeSearch, InspectSearchScope, ControlSearch, OpenSearchMatch, SearchSources, AsyncCoordination
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.SearchProbe;

namespace Xcmd.Probe.Scenarios;

public static class SearchScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("search-scheduling-regex-and-case", "UC23; ordinary results during blocked archive and large-file work; cancellation, regex and text case", async c =>
        {
            using (var work = new SearchWorkProbe(c.LeftPath))
            {
                var result = await work.WaitForOrdinaryResultAsync();
                Equal("answer.txt", result.Entry.Name, "A small nested file completes while archive and large-file workers remain blocked");
                Check(!work.Completion.IsCompleted, "The selected scope is still running");
                await work.CancelAsync();
                Check(work.Session.Stopped, "Cancellation reaches all blocked workers");
            }
            TextFile(c, "regex.txt", "Header\nThe answer is 42.\n");
            var search = await OpenAsync(c, "regex", "ANSWER");
            Equal(1, Results(search).Length, "Literal text ignores case by default");
            search.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "SearchMatchCase").IsChecked = true;
            search.StartSearch(); await c.SettleAsync();
            Equal(0, Results(search).Length, "Text case can be required");
            search.TextEditor.Text = "^The answer is \\d+\\.$";
            search.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "SearchRegex").IsChecked = true;
            search.StartSearch(); await c.SettleAsync();
            Equal(2L, Results(search).Single().Match!.Line, "Regex works one line at a time and reports the line number");
        }),
        new("search-name-mask-scope-and-navigation", "UC23; UC31; keyboard search, deferred dependencies, archives and panel navigation", async c =>
        {
            TextFile(c, "report.md", "First report");
            TextFile(c, "notes/report-old.md", "Old report");
            TextFile(c, "node_modules/report-dependency.md", "Dependency report");
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "bundle.zip"), ("docs/report-archive.md", "Archive report"));
            var search = await OpenAsync(c, "report");
            Equal(3, Results(search).Length, "Ordinary files and archive entries match before dependencies");
            Check(search.Session!.DeferredCount > 0, "Deferred scope is visible");
            await c.ScreenshotAsync("file-search", search);
            await c.ClickAsync(search.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SearchExpand"), target: search);
            Equal(4, Results(search).Length, "Expansion preserves matches and adds dependency files");
            Equal(4, Results(search).Select(result => result.Entry.FullPath).Distinct().Count(), "Expansion does not repeat results");
            search.Results.SelectedItem = Results(search).Single(result => result.Entry.Name == "report-old.md"); search.Results.Focus();
            await c.KeyAsync(Key.Enter, target: search);
            Equal(Path.Combine(c.LeftPath, "notes"), c.Window.Left.Session.DirectoryPath, "Enter opens the containing folder");
            Equal("report-old.md", c.Window.Left.Current?.Name, "The matching file is selected for panel commands");
            await c.Window.Left.LoadAsync(c.LeftPath);
            search = await OpenAsync(c, "node_modules");
            Equal(0, Results(search).Length, "A deferred folder name is absent from the ordinary pass");
            search.Expand(); await c.SettleAsync();
            Check(Results(search).Single().Entry.IsDirectory, "Expansion matches the deferred folder itself");
            search.Close();
            await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "node_modules"));
            search = await OpenAsync(c, "*.md");
            Equal(1, Results(search).Length, "An explicitly selected dependency root is searched");
        }),
        new("search-text-encodings-binary-and-preview", "UC23; automatic encodings, combined query, binary skip and F3 match preview", async c =>
        {
            const string phrase = "\u041F\u0440\u0438\u0432\u0435\u0442";
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            foreach (var codePage in new[] { 65001, 1200, 1201, 12000, 12001, 1251, 866 })
                await File.WriteAllTextAsync(Path.Combine(c.LeftPath, $"encoding-{codePage}.txt"), string.Concat(Enumerable.Repeat(phrase + " \u042D\u0442\u043E \u0442\u0435\u043A\u0441\u0442 \u0434\u043B\u044F \u043F\u0440\u043E\u0432\u0435\u0440\u043A\u0438 \u043A\u043E\u0434\u0438\u0440\u043E\u0432\u043A\u0438.\n", 20)), Encoding.GetEncoding(codePage));
            await File.WriteAllBytesAsync(Path.Combine(c.LeftPath, "encoding-binary.txt"), new byte[128].Concat(Encoding.UTF8.GetBytes(phrase)).ToArray());
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "texts.zip"), ("encoding-archive.txt", "Header\n" + phrase));
            var search = await OpenAsync(c, "encoding-*.txt", phrase);
            Equal(8, Results(search).Length, "Unicode, supported legacy text and an archive member match once; binary contents are skipped");
            Check(Results(search).All(result => result.Match!.Excerpt.Contains(phrase, StringComparison.Ordinal)), "Excerpts use the matching encoding");
            search.Results.SelectedItem = Results(search).Single(result => result.Entry.Name == "encoding-1251.txt"); search.Results.Focus();
            await c.KeyAsync(Key.F3, target: search);
            Equal(phrase, c.Window.Viewer!.TextContent.SelectedText, "F3 opens the match with the search encoding");
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer); await c.KeyAsync(Key.Escape, target: c.Window.Viewer);
            Check(c.Window.FileSearch.Window == search, "The result list remains available after preview");
            await File.WriteAllTextAsync(Path.Combine(c.LeftPath, "western.txt"), string.Concat(Enumerable.Repeat("caf\u00e9 cr\u00e8me\n", 40)), Encoding.GetEncoding(1252));
            search.NameEditor.Text = "western.txt"; search.TextEditor.Text = "caf\u00e9"; search.StartSearch(); await c.SettleAsync();
            Check(Results(search).Single().Match!.Excerpt.Contains("caf\u00e9", StringComparison.Ordinal), "Western legacy text is decoded automatically");
        }),
        new("search-archive-format-defaults", "UC23; default name and contents search in real TAR, TAR.GZ, RAR4 and RAR5 archives", async c =>
        {
            ArchiveProbe.Tar(Path.Combine(c.LeftPath, "sample.tar"), false);
            ArchiveProbe.Tar(Path.Combine(c.LeftPath, "sample.tar.gz"), true);
            foreach (var name in new[] { "Rar4.rar", "Rar5.rar" }) ArchiveProbe.Sample(c, name);
            var search = await OpenAsync(c, "*.txt");
            foreach (var name in new[] { "sample.tar", "sample.tar.gz", "Rar4.rar", "Rar5.rar" })
                Check(Results(search).Any(result => result.Entry.FullPath.StartsWith(ArchivePath.Root(Path.Combine(c.LeftPath, name)), StringComparison.Ordinal)),
                    "Name search includes archive entries by default: " + name);
            search.TextEditor.Text = ".";
            search.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "SearchRegex").IsChecked = true;
            search.StartSearch(); await c.SettleAsync();
            foreach (var name in new[] { "sample.tar", "sample.tar.gz", "Rar4.rar", "Rar5.rar" })
                Check(Results(search).Any(result => result.Match?.Line > 0 && result.Entry.FullPath.StartsWith(ArchivePath.Root(Path.Combine(c.LeftPath, name)), StringComparison.Ordinal)),
                    "Contents search decodes real archive entries: " + name);
            Equal(0, c.Launcher.Requests.Count, "Archive search does not launch extracted programs");
        }),
        new("search-git-ignore-tracked-and-exceptions", "UC23; effective Git ignore rules, nested exceptions and tracked files", async c =>
        {
            await GitAsync(c.LeftPath, "init", "--quiet");
            await GitAsync(c.LeftPath, "config", "core.excludesFile", Path.Combine(c.FixturePath, "empty-excludes"));
            TextFile(c, ".gitignore", "*.log\nignored/\n");
            TextFile(c, "tracked.log", "Tracked"); TextFile(c, "other.log", "Ignored");
            TextFile(c, "ignored/kept.log", "Tracked inside ignored directory"); TextFile(c, "ignored/skipped.log", "Ignored sibling");
            TextFile(c, "notes/.gitignore", "!keep.log\n"); TextFile(c, "notes/keep.log", "Exception");
            await GitAsync(c.LeftPath, "add", "-f", "--", "tracked.log", "ignored/kept.log");
            var search = await OpenAsync(c, "*.log");
            Equal(3, Results(search).Length, "Tracked files and nested exceptions stay in the normal scope");
            Check(Results(search).Any(result => result.Entry.Name == "kept.log"), "An ignored folder does not hide tracked descendants");
            search.Expand(); await c.SettleAsync();
            Equal(6, Results(search).Length, "Expansion includes ignored files and the existing fixture log");
            search.Close();
            await c.Window.Left.LoadAsync(Path.Combine(c.LeftPath, "ignored"));
            search = await OpenAsync(c, "*.log");
            Equal(2, Results(search).Length, "An explicitly selected Git-ignored root searches its own files");
        }),
        new("search-query-replacement-and-incomplete-coverage", "UC23; obsolete queries, malformed archives, regex validation and cancellation", async c =>
        {
            TextFile(c, "broken.zip", "Not an archive"); TextFile(c, "answer.txt", "The answer is 42.\n");
            var search = await OpenAsync(c, "answer", "42");
            Equal(1, Results(search).Length, "Useful results survive an unreadable archive");
            Check(search.Session!.Failures.Count > 0 && search.StatusMessage.Contains("could not be searched", StringComparison.Ordinal), "Incomplete coverage is visible");
            search.NameEditor.Text = "zeta"; search.TextEditor.Text = ""; search.StartSearch();
            search.NameEditor.Text = "answer"; search.StartSearch(); await c.SettleAsync();
            Check(Results(search).All(result => result.Entry.Name == "answer.txt"), "Old results cannot enter the replacement query");
            search.TextEditor.Text = "[";
            search.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "SearchRegex").IsChecked = true;
            search.StartSearch();
            var error = await SearchInteractionProbe.QueryErrorAfterRefreshAsync(c, search);
            Check(!search.IsSearching && error.IsVisible && error.Text?.Length > 0, "Invalid regex remains visible after progress refreshes");
            Equal("answer.txt", Results(search).Single().Entry.Name, "Invalid input keeps the previous results");
            await c.ScreenshotAsync("invalid-query", search);
            search.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "SearchRegex").IsChecked = false;
            search.TextEditor.Text = ""; search.StartSearch(); search.Stop(); await c.SettleAsync();
            Check(!error.IsVisible, "A valid query clears the validation error");
            Check(search.StatusMessage.Contains("incomplete", StringComparison.Ordinal), "Stopped searches cannot claim completion");
        })
    ];
}

// @tests ReadWorkingFileDiff, QuickViewFile, FavoriteLocations
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.ViewerDiffProbe;

namespace Xcmd.Probe.Scenarios;

public static class ViewerDiffScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("viewer-git-diff-inline-edits", "Similar replacement lines use a stronger background only for the changed text", async c =>
        {
            const string name = "replacement.txt";
            c.AddFile(name, "version = 12345\nseparate\napple\n");
            using var repository = await InitializeAsync(c, name);
            c.AddFile(name, "version = 19845\nseparate\nBRICK\n");
            var viewer = await OpenAsync(c, name);
            await ToggleAsync(c, viewer);
            var diff = Text(viewer);
            var before = diff.IndexOf("12345", StringComparison.Ordinal);
            var after = diff.IndexOf("19845", StringComparison.Ordinal);
            var unrelated = diff.IndexOf("BRICK", StringComparison.Ordinal);
            foreach (var theme in new[] { "dark", "github" })
            {
                if (theme == "github")
                {
                    await AppearanceProbe.ChooseThemeAsync(c, viewer, theme);
                    viewer.SetScaleForTest(1.5);
                }
                var colors = await ReadBackgroundsAsync(c, viewer, "diff-inline-" + theme,
                    before, before + 1, before + 3, after, after + 1, after + 3, unrelated);
                Check(colors[1] != colors[0] && colors[0] == colors[2], "The removed substring alone has a stronger background");
                Check(colors[4] != colors[3] && colors[3] == colors[5], "The inserted substring alone has a stronger background");
                Equal(colors[3], colors[6], "An unrelated addition keeps its line background");
            }
        }),
        new("viewer-git-diff-partially-staged", "F3 shows what changed in the index and on disk in one view", async c =>
        {
            c.AddFile("layers.txt", "before\nold value\ncontext\nsecond old\nafter\n");
            using var repository = await InitializeAsync(c, "layers.txt");
            c.AddFile("layers.txt", "before\nindex value\ncontext\nsecond old\nafter\n");
            await GitFixture.RunAsync(c.LeftPath, "add", "layers.txt");
            c.AddFile("layers.txt", "before\ndisk value\ncontext\nsecond new\nafter\n");
            var viewer = await OpenAsync(c, "layers.txt");
            CheckGitState(viewer, "Partially staged");
            Equal("before\ndisk value\ncontext\nsecond new\nafter\n", Text(viewer), "F3 opens the working copy by default");
            Equal("Show Diff", ViewerProbe.Command(viewer, "viewer.diff").Header, "The View menu offers the diff");
            await ShowFromMenuAsync(c, viewer);
            Equal("before\nold value\nindex value\nindex value\ndisk value\ncontext\nsecond old\nsecond new\nafter\n", Text(viewer), "The index change and its reversal are visible in context");
            Equal("Show Text", ViewerProbe.Command(viewer, "viewer.diff").Header, "The same command returns to text");
            await c.ScreenshotAsync("viewer-git-diff-dark", viewer);
            await HoverLineAsync(c, viewer, "old value");
            Equal("Staged deletion", ToolTip.GetTip(viewer.Reader.TextContent), "Hover explains the stage and direction");

            await c.KeyAsync(Key.PageDown, RawInputModifiers.Control, viewer);
            Equal(Text(viewer).IndexOf("old value", StringComparison.Ordinal), viewer.Reader.TextContent.CaretIndex, "Next change visits the start of the first block");
            await c.KeyAsync(Key.PageDown, RawInputModifiers.Control, viewer);
            var last = Text(viewer).IndexOf("second old", StringComparison.Ordinal);
            Equal(last, viewer.Reader.TextContent.CaretIndex, "One jump reaches the next block");
            await c.KeyAsync(Key.PageDown, RawInputModifiers.Control, viewer);
            Equal(last, viewer.Reader.TextContent.CaretIndex, "Next change stops at the last block");
            await c.KeyAsync(Key.PageUp, RawInputModifiers.Control, viewer);
            Equal(Text(viewer).IndexOf("old value", StringComparison.Ordinal), viewer.Reader.TextContent.CaretIndex, "Previous change returns to the first block");

            await c.KeyAsync(Key.F, RawInputModifiers.Control, viewer);
            await c.TextAsync("old value", viewer);
            Equal("old value", viewer.Reader.TextContent.SelectedText, "The normal Find command can select a deleted line");
            viewer.Reader.TextContent.Focus();
            await c.KeyAsync(Key.C, RawInputModifiers.Control, viewer);
            Equal("old value", await viewer.Clipboard!.TryGetTextAsync(), "Copy omits the change marker");
            await CaptureLightThemeAsync(c, viewer);
            await ToggleAsync(c, viewer);
            Check(!Text(viewer).Contains("index value", StringComparison.Ordinal), "Returning to text shows only the working copy");
        }),
        new("viewer-git-diff-status-click", "Clicking the Git status switches between the working text and its diff", async c =>
        {
            const string name = "changed.txt";
            c.AddFile(name, "original\n");
            using var repository = await InitializeAsync(c, name);
            c.AddFile(name, "working\n");
            var viewer = await OpenAsync(c, name);
            var status = GitStatus(viewer);
            Equal("Git: Changed · Show Diff (Ctrl+D)", ToolTip.GetTip(status), "The status explains how to open the diff");
            await c.ClickAsync(status, target: viewer);
            Equal("original\nworking\n", Text(viewer), "The Git status opens the diff");
            Equal("Git: Changed · Show Text (Ctrl+D)", ToolTip.GetTip(status), "The status explains how to return to the working text");

            await c.KeyAsync(Key.F12, target: viewer);
            await c.ClickAsync(status, target: viewer);
            var comment = c.Window.Feedback.ActiveComment;
            Check(comment != null, "The Git status remains selectable for feedback");
            Equal("original\nworking\n", Text(viewer), "Selecting the status for feedback leaves the diff open");
            await c.KeyAsync(Key.Escape, target: comment);

            await c.ClickAsync(status, target: viewer);
            Equal("working\n", Text(viewer), "The Git status returns to the working text");
        }),
        new("viewer-git-diff-status-and-rename", "F3 reports the Git state even when a text diff is unavailable or empty", async c =>
        {
            using var external = new ChildRepositoryFixture();
            var viewer = await OpenAsync(c, "alpha.txt", external.Repository);
            CheckGitState(viewer, "Outside repository");
            Check(!ViewerProbe.Command(viewer, "viewer.diff").IsVisible, "The diff is hidden outside Git");
            await c.KeyAsync(Key.Escape, target: viewer);

            c.AddFile(".gitignore", "ignored/\n");
            Directory.CreateDirectory(Path.Combine(c.LeftPath, "ignored"));
            c.AddFile("ignored/cache.txt", "generated");
            c.AddFile("original.txt", "one\ntwo\nthree\n");
            await File.WriteAllBytesAsync(Path.Combine(c.LeftPath, "binary.dat"), [0, 0, 0, 0]);
            using var repository = await InitializeAsync(c, ".gitignore", "original.txt", "binary.dat");
            viewer = await OpenAsync(c, "alpha.txt");
            CheckGitState(viewer, "Not tracked");
            Check(!ViewerProbe.Command(viewer, "viewer.diff").IsVisible, "Untracked files have no comparison");
            await c.KeyAsync(Key.Escape, target: viewer);
            viewer = await OpenAsync(c, "binary.dat");
            CheckGitState(viewer, "Unchanged");
            Check(!ViewerProbe.Command(viewer, "viewer.diff").IsVisible, "Binary mode has no text comparison");
            var binaryStatus = GitStatus(viewer);
            Equal("Git: Unchanged", ToolTip.GetTip(binaryStatus), "The Git status only reports the state when a diff is unavailable");
            await c.ClickAsync(binaryStatus, target: viewer);
            Equal(FileViewMode.Hex, viewer.Reader.Mode, "Clicking the passive status leaves the binary view open");
            await c.KeyAsync(Key.Escape, target: viewer);

            await GitFixture.RunAsync(c.LeftPath, "mv", "--", "original.txt", "renamed 日本語.txt");
            viewer = await OpenAsync(c, "renamed 日本語.txt");
            CheckGitState(viewer, "Staged");
            await ToggleAsync(c, viewer);
            Check(viewer.Reader.ReadingStatus.Contains("No changes", StringComparison.Ordinal), "The diff compares the renamed file with its previous path");
            await c.KeyAsync(Key.Escape, target: viewer);

            await GitFixture.RunAsync(c.LeftPath, "add", "alpha.txt");
            viewer = await OpenAsync(c, "alpha.txt");
            await ToggleAsync(c, viewer);
            CheckGitState(viewer, "Staged");
            Check(ViewerProbe.Command(viewer, "viewer.next-change").IsEnabled, "The original content is one block of additions");
            await c.KeyAsync(Key.Escape, target: viewer);

            await c.SelectAsync("ignored");
            await c.KeyAsync(Key.Enter);
            await c.SelectAsync("cache.txt");
            await c.KeyAsync(Key.F3);
            viewer = c.Window.Viewer!;
            CheckGitState(viewer, "Ignored");
            Check(!ViewerProbe.Command(viewer, "viewer.diff").IsVisible, "An ignored file is not eligible for diff");
        }),
        new("viewer-git-diff-paging-and-refresh", "F3 keeps the reading position and reloads both Git comparisons when it regains focus", async c =>
        {
            var before = string.Concat(Enumerable.Range(0, 2000).Select(number => $"Context near row {number:D4}\n"));
            const string name = "long.txt";
            c.AddFile(name, before + "old tail\n");
            using var repository = await InitializeAsync(c, name);
            c.AddFile(name, before + "new tail\n");
            var viewer = await OpenAsync(c, name);
            CheckGitState(viewer, "Changed");
            viewer.Reader.FileScroll.Value = viewer.Reader.FileScroll.Maximum / 2;
            await c.SettleAsync();
            var anchor = viewer.Reader.FirstVisibleByte;
            Check(anchor > 16 * 1024, "The regular viewer is on a later page");
            await ToggleAsync(c, viewer);
            Check(Math.Abs(viewer.Reader.FirstVisibleByte - anchor) < 100, "Entering diff keeps the same working line in view");
            var diffAnchor = viewer.Reader.FirstVisibleByte;
            await ViewerProbe.WheelAsync(c, viewer, -5);
            Check(viewer.Reader.FirstVisibleByte > diffAnchor, "Scrolling down updates the visible position in a later diff page");
            await ViewerProbe.WheelAsync(c, viewer, 5);
            Equal(diffAnchor, viewer.Reader.FirstVisibleByte, "Scrolling back restores the diff position");
            await c.KeyAsync(Key.PageDown, RawInputModifiers.Control, viewer);
            Check(Text(viewer).Contains("old tail\nnew tail", StringComparison.Ordinal), "Next change can read the tail after the first page");

            c.Window.Activate();
            await GitFixture.RunAsync(c.LeftPath, "add", name);
            c.AddFile(name, before + "old tail\n");
            viewer.Activate();
            await c.SettleAsync();
            CheckGitState(viewer, "Partially staged");
            Check(Text(viewer).Contains("old tail\nnew tail\nnew tail\nold tail", StringComparison.Ordinal), "Staged work stays visible when the disk text matches HEAD again");
            await ToggleAsync(c, viewer);
            Check(viewer.Reader.FirstVisibleByte > 16 * 1024 && Text(viewer).EndsWith("old tail\n", StringComparison.Ordinal), "Returning to text keeps the late reading position and reads the current file");
        }),
        new("viewer-git-diff-conflict-and-unborn", "F3 compares conflicts to HEAD and can show staged text before the first commit", async c =>
        {
            c.AddFile("conflict.txt", "base\n");
            using var repository = await InitializeAsync(c);
            await GitFixture.RunAsync(c.LeftPath, "add", "conflict.txt");
            var viewer = await OpenAsync(c, "conflict.txt");
            CheckGitState(viewer, "Staged");
            await ToggleAsync(c, viewer);
            Check(ViewerProbe.Command(viewer, "viewer.next-change").IsEnabled, "A file in an unborn branch has staged additions");
            await c.KeyAsync(Key.Escape, target: viewer);
            await GitFixture.RunAsync(c.LeftPath, "commit", "-m", "Base");
            await GitFixture.RunAsync(c.LeftPath, "switch", "-c", "other");
            c.AddFile("conflict.txt", "other\n");
            await GitFixture.RunAsync(c.LeftPath, "commit", "-am", "Other");
            await GitFixture.RunAsync(c.LeftPath, "switch", "main");
            c.AddFile("conflict.txt", "ours\n");
            await GitFixture.RunAsync(c.LeftPath, "commit", "-am", "Ours");
            try { await GitFixture.RunAsync(c.LeftPath, "merge", "other"); }
            catch (IOException) { }
            viewer = await OpenAsync(c, "conflict.txt");
            CheckGitState(viewer, "Conflicted");
            await ToggleAsync(c, viewer);
            Check(Text(viewer).Contains("<<<<<<< HEAD\nours\n=======\nother\n>>>>>>> other", StringComparison.Ordinal), "The working conflict markers can be read in the diff");
            await HoverLineAsync(c, viewer, "<<<<<<<");
            Equal("Working copy addition", ToolTip.GetTip(viewer.Reader.TextContent), "An unresolved index cannot label the edit staged or unstaged");
        }),
        new("viewer-git-diff-shortcut-and-syntax", "The window shortcut works from Find and diff rows keep their original syntax", async c =>
        {
            const string name = "sample.cs";
            const string staged = "class Sample\n{\n    const string Shared = \"shared\";\n    const string Value = \"index\";\n}\n";
            var original = staged.Replace("    const string Shared", "    /* old comment\n    const string Hidden = \"old\";\n    const string Shared", StringComparison.Ordinal)
                .Replace("    const string Value", "    */\n    const string Value", StringComparison.Ordinal)
                .Replace("\"index\"", "\"head\"", StringComparison.Ordinal);
            c.AddFile(name, original);
            using var repository = await InitializeAsync(c, name);
            c.AddFile(name, staged);
            await GitFixture.RunAsync(c.LeftPath, "add", name);
            c.AddFile(name, staged.Replace("\"index\"", "\"disk\"", StringComparison.Ordinal));
            var viewer = await OpenAsync(c, name);
            CheckGitState(viewer, "Partially staged");
            Equal("C#", viewer.Reader.SyntaxStatus, "F3 already has source highlighting");
            await c.KeyAsync(Key.F, RawInputModifiers.Control, viewer);
            Check(viewer.Reader.Search.IsKeyboardFocusWithin, "The find box has focus");
            await ToggleWithPhysicalKeyAsync(c, viewer);
            Equal("Show Text", ViewerProbe.Command(viewer, "viewer.diff").Header, "The physical Ctrl+D works even with Find focused");
            var syntax = viewer.Reader.TextContent.Syntax!;
            SyntaxProbe.CheckMarker(syntax, "const string Hidden", SyntaxKind.Comment);
            SyntaxProbe.CheckMarker(syntax, "const string Shared", SyntaxKind.Keyword);
            SyntaxProbe.CheckMarker(syntax, "\"head\"", SyntaxKind.String);
            SyntaxProbe.CheckMarker(syntax, "\"index\"", SyntaxKind.String);
            SyntaxProbe.CheckMarker(syntax, "\"disk\"", SyntaxKind.String);
            CheckRenderedSyntax(viewer);
            await c.ScreenshotAsync("viewer-git-diff-syntax-dark", viewer);
            await CaptureLightThemeAsync(c, viewer);
            CheckGitState(viewer, "Partially staged");
            CheckRenderedSyntax(viewer);
            await SyntaxProbe.ChooseLanguageAsync(c, viewer, "plain");
            Equal("Plain text", viewer.Reader.SyntaxStatus, "The syntax menu also works in diff mode");
            await SyntaxProbe.ChooseLanguageAsync(c, viewer, "auto");
            Equal("C#", viewer.Reader.SyntaxStatus, "Automatic syntax is restored in diff mode");
            await ToggleAsync(c, viewer);
            Equal("C#", viewer.Reader.SyntaxStatus, "The same key returns to highlighted working text");
            await ReleaseDiffKeyAsync(c, viewer);
            Equal("Show Text", ViewerProbe.Command(viewer, "viewer.diff").Header, "The shortcut works when Windows only sends the key release");
            await ReleaseDiffKeyAsync(c, viewer);
            Equal("Show Diff", ViewerProbe.Command(viewer, "viewer.diff").Header, "A release without a press can also return to text");
        }),
        new("viewer-git-diff-quick-view-focus", "Ctrl+D follows focus between Quick View and the file list", async c =>
        {
            const string name = "layers.txt";
            c.AddFile(name, "original\n");
            using var repository = await InitializeAsync(c, name);
            c.AddFile(name, "working\n");
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync(name, c.Window.Left);
            await c.KeyAsync(Key.Q, RawInputModifiers.Control);
            var reader = c.Window.GetVisualDescendants().OfType<FileViewer>().Single();
            var diff = ViewerProbe.Command(reader, "viewer.diff");
            Check(diff.IsVisible, "Quick View offers the same diff command");

            await c.KeyAsync(Key.D, RawInputModifiers.Control);
            Check(c.Window.ActiveNavigationDialog != null, "The source list retains the Favorites shortcut");
            Equal("Show Diff", diff.Header, "The preview remains in text while the source list has focus");
            await c.KeyAsync(Key.Escape, target: c.Window.ActiveNavigationDialog);

            await ReleaseDiffKeyAsync(c, c.Window);
            Check(c.Window.ActiveNavigationDialog != null, "A release without a press also opens Favorites from the source list");
            await c.KeyAsync(Key.Escape, target: c.Window.ActiveNavigationDialog);

            var input = new InputDriver(c.Window);
            input.KeyDown(Key.None, RawInputModifiers.Control, PhysicalKey.D, "в");
            input.KeyUp(Key.None, RawInputModifiers.Control, PhysicalKey.D, "в");
            await c.SettleAsync();
            Check(c.Window.ActiveNavigationDialog != null, "The physical Ctrl+D opens Favorites on another keyboard layout");
            await c.KeyAsync(Key.Escape, target: c.Window.ActiveNavigationDialog);

            await c.KeyAsync(Key.Tab, RawInputModifiers.Control);
            Check(reader.IsKeyboardFocusWithin, "Ctrl+Tab focuses the reader");
            await ReleaseDiffKeyAsync(c, c.Window);
            Equal("original\nworking\n", reader.TextContent.Text, "The reader shortcut shows the combined diff");
            Equal<Window?>(null, c.Window.ActiveNavigationDialog, "The reader shortcut stays in Quick View");
            await c.ClickAsync(GitStatus(reader));
            Equal("working\n", reader.TextContent.Text, "Clicking the status also returns Quick View to the working text");
        })
    ];
}

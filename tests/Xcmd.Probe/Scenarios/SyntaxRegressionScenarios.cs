// @tests OpenViewers, ReadLargeFiles, HighlightSource, FollowLog, ReportHighlighting, ArchiveAccess, RepositoryDocuments, SyntaxSpans
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class SyntaxRegressionScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("syntax-page-pointer-drag", "Drag across a highlighted source page", async c =>
        {
            var viewer = await SyntaxDragProbe.OpenAsync(c);
            try
            {
                var box = viewer.Reader.TextContent;
                Check(box.Syntax!.Spans.Count > 500, "The page has dense syntax highlighting");
                var selected = string.Concat(Enumerable.Repeat(SyntaxDragProbe.SampleLine, 6));
                c.Measurements["dragStepMedianMs"] = SyntaxDragProbe.Drag(viewer, selected.Length, 0);
                Equal(selected, box.SelectedText, "Dragging backward selects the exact source text");
                await c.KeyAsync(Key.C, RawInputModifiers.Control, viewer);
                Equal(selected, await viewer.Clipboard!.TryGetTextAsync(), "Copy preserves the selected Unicode text");
                await SyntaxDragProbe.CheckSelectionColorAsync(c, viewer);

                await SyntaxProbe.ChooseLanguageAsync(c, viewer, "plain");
                SyntaxDragProbe.Drag(viewer, 0, selected.Length);
                Equal(selected, box.SelectedText, "Dragging forward selects the same plain text");
            }
            finally { viewer.Close(); }
        }),
        new("syntax-report-current-selection-and-copy", "UC09; reports use current selection after menus, support pointer entry and confirm saving", async c =>
        {
            c.AddFile("report.cs", "public class Sample { string first = \"alpha\"; string second = \"beta 😀\"; }\n");
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("report.cs"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            SyntaxProbe.Select(viewer, "alpha");
            await c.ClickAsync((MenuItem)viewer.Reader.Commands.Items[1]!, target: viewer);
            await c.KeyAsync(Key.Escape, target: viewer);
            SyntaxProbe.Select(viewer, "beta 😀");
            await c.KeyAsync(Key.C, RawInputModifiers.Control, viewer);
            Equal("beta 😀", await viewer.Clipboard!.TryGetTextAsync(), "Plain copy keeps the exact selected Unicode text");
            await c.KeyAsync(Key.F12, RawInputModifiers.Control | RawInputModifiers.Shift, viewer);
            Check(SyntaxProbe.ReportContext(c).Contains("beta 😀", StringComparison.Ordinal) && !SyntaxProbe.ReportContext(c).Contains("alpha", StringComparison.Ordinal), "A dismissed menu cannot supply an older selection");
            await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveComment);
            Equal(0, (await c.Window.Feedback.Store!.ReadAsync()).Length, "Cancel does not save a report");
            Equal("beta 😀", viewer.Reader.TextContent.SelectedText, "Cancel preserves the selected source");
            await SyntaxProbe.OpenContextReportAsync(c, viewer);
            Check(SyntaxProbe.ReportContext(c).Contains("beta 😀", StringComparison.Ordinal), "The pointer report includes the selected code");
            await c.TextAsync("Review this string color.", c.Window.Feedback.ActiveComment);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, c.Window.Feedback.ActiveComment);
            Check(viewer.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "ViewerFeedbackStatus" && text.IsVisible && text.Text == "Comment saved."), "Saving is confirmed in the originating viewer");
            Equal(1, (await c.Window.Feedback.Store!.ReadAsync()).Length, "The pointer report is saved once");
        }),
        new("syntax-scroll-and-replacement-during-load", "UC10; in-page scrolling retains pending syntax while replacement rejects obsolete context", async c =>
        {
            var path = Path.Combine(c.LeftPath, "delayed.cs");
            await File.WriteAllTextAsync(path, string.Concat(Enumerable.Repeat("public class Sample {} // comment\n", 1300)));
            var gate = new SyntaxReadGate();
            var viewer = await SyntaxProbe.OpenAsync(c, path, gate);
            try
            {
                gate.Arm(); c.PressKey(Key.End, RawInputModifiers.Control, viewer); await gate.WaitForContextAsync();
                var page = viewer.Reader.ByteOffset;
                viewer.UpdateLayout();
                var scroll = viewer.Reader.TextContent.GetVisualDescendants().OfType<ScrollViewer>().Single();
                var previousY = scroll.Offset.Y;
                c.PressKey(Key.PageDown, target: viewer);
                Check(scroll.Offset.Y > previousY && viewer.Reader.ByteOffset == page, "Page Down scrolls within the displayed page");
                await gate.ReleaseAsync(c); await SyntaxProbe.WaitForSyntaxAsync(viewer);
                Check(!gate.WasCancelled, "Scrolling within displayed text does not cancel its pending syntax");
                Equal("C#", viewer.Reader.SyntaxStatus, "Syntax appears after the pending context arrives");
                gate.Arm(); c.PressKey(Key.End, RawInputModifiers.Control, viewer); await gate.WaitForContextAsync();
                await viewer.Reader.OpenAsync();
                await gate.ReleaseAsync(c);
                Check(gate.WasCancelled, "Reload cancels obsolete syntax context");
                Equal(0L, viewer.Reader.ByteOffset, "Obsolete completion cannot replace the reloaded first page");
                Equal(viewer.Reader.TextContent.Text, viewer.Reader.TextContent.Syntax?.Text, "Tokens belong to the current source snapshot");
            }
            finally { viewer.Close(); }
        }),
        new("syntax-follow-edits-and-rendering", "UC09; source edits rebuild tokens and colored text renders at 100/150 percent", async c =>
        {
            c.AddFile("follow.cs", "/* comment\n");
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("follow.cs"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            await c.KeyAsync(Key.F, RawInputModifiers.Control | RawInputModifiers.Shift, viewer);
            var path = Path.Combine(c.LeftPath, "follow.cs");
            await File.AppendAllTextAsync(path, "*/ public class Sample { int value = 42; }\n");
            await WorkspaceProbe.WaitAsync(() => viewer.Reader.TextContent.Syntax?.Text.Contains("42", StringComparison.Ordinal) == true);
            SyntaxProbe.CheckMarker(viewer.Reader.TextContent.Syntax!, "public", SyntaxKind.Keyword);
            await File.WriteAllTextAsync(path, "string value = \"changed\"; // current\n");
            await WorkspaceProbe.WaitAsync(() => viewer.Reader.TextContent.Syntax?.Text.StartsWith("string", StringComparison.Ordinal) == true);
            SyntaxProbe.CheckMarker(viewer.Reader.TextContent.Syntax!, "changed", SyntaxKind.String);
            await c.KeyAsync(Key.F, RawInputModifiers.Control | RawInputModifiers.Shift, viewer);
            await SyntaxProbe.RenderScalesAsync(c, viewer);
        }),
        new("syntax-archive-and-git-sources", "UC09; actual archive members and committed Git blobs keep language detection", async c =>
        {
            const string source = "public class Stored { int value = 42; }\n";
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "code.zip"), ("stored.cs", source));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("code.zip"); await c.KeyAsync(Key.Enter);
            await c.SelectAsync("stored.cs"); await c.KeyAsync(Key.F3);
            Equal("C#", c.Window.Viewer!.Reader.SyntaxStatus, "An archive member retains its language");
            SyntaxProbe.CheckMarker(c.Window.Viewer.Reader.TextContent.Syntax!, "public", SyntaxKind.Keyword);
            await c.KeyAsync(Key.Escape, target: c.Window.Viewer);
            c.AddFile("stored.cs", source);
            using var repository = await GitHistoryFixture.CreateAsync(c.LeftPath);
            await File.WriteAllTextAsync(Path.Combine(c.LeftPath, "stored.cs"), "working text");
            using var documents = new GitHistoryDocuments();
            var materialized = await documents.FileAsync(c.LeftPath, "HEAD", "stored.cs", CancellationToken.None);
            var files = new ViewerFiles();
            var page = await files.ReadAsync(materialized, 0, CancellationToken.None);
            var highlighted = await new SyntaxHighlighter().ReadAsync(files, materialized, page, Encoding.UTF8, null, CancellationToken.None);
            Equal(source, highlighted.Text, "The Git adapter highlights the committed source instead of working text");
            SyntaxProbe.CheckMarker(highlighted, "public", SyntaxKind.Keyword);
        }),
        new("syntax-failure-cancellation-and-detection", "UC09; missing grammars fall back, cancellation remains observable and compound extensions win", () =>
        {
            var highlighter = new SyntaxHighlighter();
            var missing = highlighter.Highlight("keep source", new("missing", "Missing", "source.xcmd-missing"), CancellationToken.None);
            Equal("keep source", missing.Text, "Missing grammar preserves source");
            Check(missing.Spans.Count == 0 && missing.Notice != null, "Missing grammar explains the plain-text fallback");
            var cancelled = false;
            try { highlighter.Highlight("public class Sample {}", SyntaxHighlighter.Detect("sample.cs", "")!, new CancellationToken(true)); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "Cancellation is not converted into a grammar error");
            Equal("jsonc", SyntaxHighlighter.Detect(".eslintrc.json", "")?.Id, "The compound extension selects JSON with comments");
            Equal("xml", SyntaxHighlighter.Detect("PROJECT.CSPROJ.USER", "")?.Id, "Compound project extensions are case-insensitive");
        })
    ];
}

// @tests ChooseViewMode, ReadLargeFiles, FindViewerText, HighlightSource, ReaderThemes, ReportHighlighting, SyntaxSpans, FeedbackIdentity
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class SyntaxScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("syntax-language-grammars", "UC09; real bundled grammars for known file types and embedded code", async c =>
        {
            foreach (var sample in SyntaxProbe.Samples)
            {
                var document = await SyntaxProbe.ReadAsync(c, sample);
                Equal(sample.Language, document.Language?.Id, "File name or shebang selects " + sample.File);
                Check(document.Notice == null, "The grammar completes: " + sample.File + " " + document.Notice);
                Equal(sample.Text, document.Text, "Highlighting preserves every source character");
                SyntaxProbe.CheckMarker(document, sample.Marker, sample.Kind);
                var start = sample.Text.IndexOf(sample.Marker, StringComparison.Ordinal);
                var selection = document.Select(start, sample.Marker.Length);
                Equal(sample.Marker, selection.Text, "A portable selection retains its original text");
                Check(selection.Spans.All(span => span.Start >= 0 && span.Start + span.Length <= selection.Text.Length), "Export spans are clipped and relative to the selection");
                if (sample.Language == "html") SyntaxProbe.CheckMarker(document, "const", SyntaxKind.Keyword);
                if (sample.Language == "markdown") SyntaxProbe.CheckMarker(document, "const", SyntaxKind.Keyword);
            }
        }),
        new("syntax-page-context-and-limits", "UC10; multiline context, Unicode, unknown types and bounded work", async c =>
        {
            var content = "/* comment\n" + string.Concat(Enumerable.Repeat("still comment € 😀\n", 1200)) + "*/\npublic class End {}";
            var sample = new SyntaxProbe.Sample("multiline.cs", "csharp", content, "still", SyntaxKind.Comment);
            foreach (var encoding in new Encoding[] { new UTF8Encoding(true), Encoding.Unicode })
            {
                var page = await SyntaxProbe.ReadAsync(c, sample, encoding, FileSlice.PageSize);
                Check(page.Notice == null, "Page context completes for " + encoding.WebName);
                SyntaxProbe.CheckMarker(page, "still", SyntaxKind.Comment);
            }
            var unknown = await SyntaxProbe.ReadAsync(c, new("unknown.data", "", "plain text", "", SyntaxKind.Plain));
            Check(unknown.Language == null && unknown.Spans.Count == 0, "Unknown types use plain text");
            var longLine = await SyntaxProbe.ReadAsync(c, sample with { Text = new string('x', SyntaxHighlighter.MaximumLineLength + 1) });
            Check(longLine.Spans.Count == 0 && longLine.Notice?.Contains("line exceeds", StringComparison.Ordinal) == true, "Long lines have an explicit plain-text fallback");
            var large = await SyntaxProbe.ReadAsync(c, sample with { Text = new string('x', SyntaxHighlighter.MaximumContextBytes + FileSlice.PageSize) }, offset: SyntaxHighlighter.MaximumContextBytes);
            Check(large.Spans.Count == 0 && large.Notice?.Contains("1 MiB", StringComparison.Ordinal) == true, "Distant pages do not guess missing multiline context");
        }),
        new("syntax-viewer-selection-and-report", "UC09; colored reading, theme changes, search, manual language and one-step reports", async c =>
        {
            const string source = "// A short sample\r\npublic class Hello\r\n{\r\n    string message = \"hello 😀\";\r\n    int answer = 42;\r\n}\r\n";
            c.AddFile("highlight.cs", source);
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("highlight.cs"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            Equal("C#", viewer.Reader.SyntaxStatus, "The viewer identifies its syntax language");
            ViewerProbe.CheckReadingLayout(viewer);
            foreach (var theme in new[] { "dark", "github", "light", "solarized-light", "forest-light", "solarized-dark", "dracula", "nord", "forest-dark" })
            {
                await AppearanceProbe.ChooseThemeAsync(c, viewer, theme);
                viewer.Reader.TextContent.SelectionStart = viewer.Reader.TextContent.SelectionEnd = 0;
                viewer.Reader.TextContent.UpdateLayout();
                SyntaxProbe.CheckRenderedColors(viewer);
            }
            await c.ScreenshotAsync("syntax-code", viewer);
            await c.KeyAsync(Key.F, RawInputModifiers.Control, viewer); await c.TextAsync("message", viewer);
            Equal("message", viewer.Reader.TextContent.SelectedText, "Search selects the original source in colored text");
            await c.KeyAsync(Key.Escape, target: viewer);
            viewer.Reader.TextContent.SelectionStart = source.IndexOf("\"hello", StringComparison.Ordinal);
            viewer.Reader.TextContent.SelectionEnd = viewer.Reader.TextContent.SelectionStart + "\"hello 😀\"".Length;
            var selected = viewer.Reader.TextContent.SelectedText;
            await c.KeyAsync(Key.F12, RawInputModifiers.Control | RawInputModifiers.Shift, viewer);
            var comment = c.Window.Feedback.ActiveComment!;
            Check(comment != null && !c.Window.Feedback.IsPicking, "The report shortcut opens the comment form directly");
            var context = comment!.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "FeedbackReportContext").Text!;
            Check(context.Contains(selected, StringComparison.Ordinal) && !context.Contains("int answer", StringComparison.Ordinal), "Only selected code is included and visible before saving");
            await c.ScreenshotAsync("syntax-report", comment);
            await c.TextAsync("The selected string has the wrong color.", comment);
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            var issue = (await c.Window.Feedback.Store!.ReadAsync()).Single();
            Check(issue.Text.Contains("Language: C#", StringComparison.Ordinal) && issue.Text.Contains(SyntaxHighlighter.Version, StringComparison.Ordinal), "Saved feedback includes reproducible syntax context");
            Equal("FileViewerWindow.TextContent", issue.Target.Id, "Reports retain the viewer's stable feedback target");
            Equal(selected, viewer.Reader.TextContent.SelectedText, "Submitting a report preserves the selected code");
            await SyntaxProbe.ChooseLanguageAsync(c, viewer, "plain");
            Equal("Plain text", viewer.Reader.SyntaxStatus, "Plain text disables coloring");
            await SyntaxProbe.ChooseLanguageAsync(c, viewer, "auto");
            Equal("C#", viewer.Reader.SyntaxStatus, "Automatic language restores coloring");
            await c.KeyAsync(Key.D3, target: viewer);
            Check(viewer.Reader.TextContent.Syntax == null, "Hex mode clears syntax spans");
            await c.KeyAsync(Key.D1, target: viewer);
            Equal(source, viewer.Reader.TextContent.Text, "Returning to text preserves the decoded source");
        })
    ];
}

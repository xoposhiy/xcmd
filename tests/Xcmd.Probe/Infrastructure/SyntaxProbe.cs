// @tests HighlightSource, ReaderThemes, ReportHighlighting, SyntaxSpans
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public static class SyntaxProbe
{
    public static async Task<FileViewerWindow> OpenAsync(ProbeContext context, string path, IViewerFiles files)
    {
        var viewer = new FileViewerWindow([path], 0, files, context.Window.Feedback, _ => Task.CompletedTask, _ => Task.CompletedTask);
        context.Window.Feedback.Attach(viewer, showButton: false);
        viewer.Show();
        await viewer.Reader.OpenAsync();
        await context.SettleAsync();
        ProbeContext.Equal(IntPtr.Zero, viewer.TryGetPlatformHandle()!.Handle, "Controlled viewers remain headless");
        return viewer;
    }

    public static Task WaitForSyntaxAsync(FileViewerWindow viewer) => WorkspaceProbe.WaitAsync(() => !viewer.Reader.IsLoading);

    public static void Select(FileViewerWindow viewer, string text)
    {
        viewer.Reader.TextContent.Focus();
        var start = viewer.Reader.TextContent.Text!.IndexOf(text, StringComparison.Ordinal);
        ProbeContext.Check(start >= 0, "The selected source exists");
        viewer.Reader.TextContent.CaretIndex = start + text.Length;
        viewer.Reader.TextContent.SelectionStart = start;
        viewer.Reader.TextContent.SelectionEnd = start + text.Length;
    }

    public static string ReportContext(ProbeContext context) => context.Window.Feedback.ActiveComment!
        .GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "FeedbackReportContext").Text!;

    public static async Task OpenContextReportAsync(ProbeContext context, FileViewerWindow viewer)
    {
        var presenter = viewer.Reader.TextContent.GetVisualDescendants().OfType<TextPresenter>().Single();
        var bounds = presenter.TextLayout.HitTestTextPosition(viewer.Reader.TextContent.SelectionStart);
        var point = presenter.TranslatePoint(new Point(bounds.X + 2, bounds.Y + 2), viewer)!.Value;
        viewer.MouseDown(point, MouseButton.Right); viewer.MouseUp(point, MouseButton.Right);
        await context.SettleAsync();
        var menu = viewer.Reader.TextContent.ContextMenu!;
        ProbeContext.Check(menu.IsOpen, "Right click opens the code context menu");
        await context.ClickAsync(menu.Items.OfType<MenuItem>().Single(item => item.Name == "viewer.syntax-report-context"), target: viewer);
    }

    public static async Task RenderScalesAsync(ProbeContext context, FileViewerWindow viewer)
    {
        foreach (var scale in new[] { 1d, 1.5d })
        {
            viewer.SetScaleForTest(scale);
            await context.ScreenshotAsync("syntax-scale-" + scale * 100, viewer);
            ViewerProbe.CheckReadingLayout(viewer);
            CheckRenderedColors(viewer);
        }
        viewer.SetScaleForTest(1);
    }

    public sealed record Sample(string File, string Language, string Text, string Marker, SyntaxKind Kind);
    public static IReadOnlyList<Sample> Samples { get; } =
    [
        new("sample.html", "html", "<div class=\"box\">Hello</div>\n<script>const n = 42;</script>", "div", SyntaxKind.Tag),
        new("sample.json", "json", "{\"answer\": 42, \"message\": \"hello\"}", "42", SyntaxKind.Number),
        new("sample.md", "markdown", "# Heading\n\n```js\nconst n = 42;\n```\n", "Heading", SyntaxKind.Heading),
        new("sample.js", "javascript", "const answer = 42; // note", "const", SyntaxKind.Keyword),
        new("sample.py", "python", "def hello():\n    return \"hello\" # note\n", "return", SyntaxKind.Keyword),
        new("sample.cs", "csharp", "public class Hello { string name = \"hello\"; } // note", "public", SyntaxKind.Keyword),
        new("sample.java", "java", "public class Hello { int count = 42; }", "public", SyntaxKind.Keyword),
        new("sample.kt", "kotlin", "fun main() { val name = \"hello\" } // note", "fun", SyntaxKind.Keyword),
        new("sample.toml", "toml", "[server]\nport = 8080\nname = \"demo\" # note", "8080", SyntaxKind.Number),
        new("sample.ini", "ini", "[server]\nport=8080\n; note", "note", SyntaxKind.Comment),
        new("sample.xml", "xml", "<root enabled=\"true\"><item /></root>", "root", SyntaxKind.Tag),
        new("sample.css", "css", "body { color: red; } /* note */", "note", SyntaxKind.Comment),
        new("sample.cpp", "cpp", "int main() { return 42; } // note", "return", SyntaxKind.Keyword),
        new("sample.csproj", "xml", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup /></Project>", "Project", SyntaxKind.Tag),
        new("sample.sh", "shellscript", "#!/bin/sh\n# note\necho \"hello\"", "note", SyntaxKind.Comment),
        new("sample.ps1", "powershell", "# note\nWrite-Host \"hello\"", "note", SyntaxKind.Comment),
        new("sample.ts", "typescript", "const answer: number = 42;", "const", SyntaxKind.Keyword),
        new("sample.yaml", "yaml", "name: demo\n# note", "note", SyntaxKind.Comment),
        new("Dockerfile", "dockerfile", "FROM alpine\n# note", "note", SyntaxKind.Comment),
        new("run-script", "python", "#!/usr/bin/env python3\ndef hello():\n    return 42", "return", SyntaxKind.Keyword)
    ];

    public static async Task<SyntaxDocument> ReadAsync(ProbeContext c, Sample sample, Encoding? encoding = null, long offset = 0)
    {
        var path = Path.Combine(c.LeftPath, sample.File);
        await File.WriteAllTextAsync(path, sample.Text, encoding ?? new UTF8Encoding(false));
        var files = new ViewerFiles();
        var page = await files.ReadAsync(path, offset, CancellationToken.None);
        var result = await new SyntaxHighlighter().ReadAsync(files, path, page, encoding ?? Encoding.UTF8, null, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(c.FixturePath, sample.File.Replace('.', '_') + "-tokens.json"), JsonSerializer.Serialize(result));
        return result;
    }

    public static void CheckMarker(SyntaxDocument document, string marker, SyntaxKind expected)
    {
        var start = document.Text.IndexOf(marker, StringComparison.Ordinal);
        ProbeContext.Check(start >= 0 && document.Spans.Any(span => span.Start <= start && span.Start + span.Length > start && span.Kind == expected),
            $"{document.Language?.Name}: '{marker}' is {expected}; {document.Notice}");
    }

    public static void CheckRenderedColors(FileViewerWindow viewer)
    {
        var presenter = viewer.Reader.TextContent.GetVisualDescendants().OfType<TextPresenter>().Single();
        var colors = presenter.TextLayout.TextLines.SelectMany(line => line.TextRuns)
            .Select(run => run.Properties?.ForegroundBrush).OfType<ISolidColorBrush>().Select(brush => brush.Color).Distinct().ToArray();
        ProbeContext.Check(colors.Length >= 3, "The actual text layout contains distinct syntax colors");
        var background = ((ISolidColorBrush)viewer.Reader.TextContent.Background!).Color;
        foreach (var color in colors)
        {
            var first = Luminance(color); var second = Luminance(background);
            ProbeContext.Check((Math.Max(first, second) + .05) / (Math.Min(first, second) + .05) >= 4.5, "Syntax colors meet the reader contrast target");
        }
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte value) => value / 255d <= .04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }

    public static async Task ChooseLanguageAsync(ProbeContext c, FileViewerWindow viewer, string id)
    {
        var view = (MenuItem)viewer.Reader.Commands.Items[1]!;
        await c.ClickAsync(view, target: viewer);
        var languages = view.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "Syntax language");
        await c.ClickAsync(languages, target: viewer);
        await c.ClickAsync(languages.Items.OfType<MenuItem>().Single(item => item.Name == "viewer.syntax." + id), target: viewer);
    }
}

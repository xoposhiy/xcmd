// @tests ChooseViewMode, ReadLargeFiles, FindViewerText, HighlightSource
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.StructuredJsonProbe;

namespace Xcmd.Probe.Scenarios;

public static class StructuredJsonScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("viewer-structured-source-position", "Entry is explicit, keeps the source selection, and preserves file navigation", async c =>
        {
            const string source = "{\r\n  \"name\": \"日本語 😀\",\r\n  \"info\": {\"city\":\"Berlin\"}\r\n}";
            c.AddFile("structured-b.json", "[42]");
            var viewer = await OpenAsync(c, "structured-a", source, enter: false);
            Equal(FileViewMode.Text, viewer.Reader.Mode, "JSON opens in the ordinary reader");
            await c.KeyAsync(Key.D5, target: viewer);
            Equal(FileViewMode.Text, viewer.Reader.Mode, "5 leaves the current view in place");
            var index = Text(viewer).IndexOf("日本語", StringComparison.Ordinal);
            SelectRange(viewer, index, index + "日本語 😀".Length);
            var specialized = ViewerProbe.Command(viewer, "viewer.mode.specialized");
            Check(specialized.IsEnabled, "A JSON object without a file extension has a specialized view");
            specialized.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await c.SettleAsync();
            Equal(FileViewMode.Structured, viewer.Reader.Mode, "The shared menu action opens structured JSON");
            Check(specialized.IsChecked, "The Specialized item is selected for structured JSON");
            CheckPath(viewer, "$.name");
            Check(viewer.Reader.ReadingStatus.Contains("utf-8", StringComparison.Ordinal), "Structured reading status shows the source encoding");
            Check(ToolTip.GetTip(specialized) is string help &&
                help.Contains("Ctrl+Alt+Up/Down", StringComparison.Ordinal), "The menu explains JSON peer navigation for this file");
            await c.KeyAsync(Key.C, RawInputModifiers.Control, viewer);
            Equal("日本語 😀", await viewer.Clipboard!.TryGetTextAsync(), "Copy preserves Unicode source text");
            await c.KeyAsync(Key.D1, target: viewer);
            Equal(index, viewer.Reader.TextContent.SelectionStart, "Returning to text restores the source selection");
            var bytes = await File.ReadAllBytesAsync(viewer.Reader.CurrentPath);
            Check(bytes.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(source)), "Reading leaves the file bytes intact");

            await c.KeyAsync(Key.D4, target: viewer);
            ViewerProbe.Command(viewer, "viewer.next-file").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await c.SettleAsync();
            Equal("structured-b.json", Path.GetFileName(viewer.Reader.CurrentPath), "The menu still changes files");
            Equal(FileViewMode.Text, viewer.Reader.Mode, "The next file starts in its default mode");
            await c.KeyAsync(Key.Left, RawInputModifiers.Alt, viewer);
            Equal("structured-a", Path.GetFileName(viewer.Reader.CurrentPath), "Alt+Left retains ordinary file navigation in text mode");
        }),
        new("viewer-structured-view-position-after-scroll", "Mode switches preserve the reading position after scrolling away from the caret", async c =>
        {
            var viewer = await OpenAsync(c, "scroll.json", LargeArray(600), enter: false);
            await ViewerProbe.WheelAsync(c, viewer, -14);
            var textTop = viewer.Reader.FirstVisibleByte;
            Check(textTop > 1000 && viewer.Reader.TextContent.CaretIndex == 0, "The viewport moves while the text caret stays at the start");
            await c.KeyAsync(Key.D4, target: viewer);
            Equal(FileViewMode.Structured, viewer.Reader.Mode, "The viewer opens the structured mode");
            Check(Math.Abs(viewer.Reader.FirstVisibleByte - textTop) < 100, "Structured mode keeps the same source record on screen");

            await ViewerProbe.WheelAsync(c, viewer, -14);
            var structuredTop = viewer.Reader.FirstVisibleByte;
            Check(structuredTop > textTop + 1000, "Scrolling in Structured moves the viewport away from its caret");
            await c.KeyAsync(Key.D1, target: viewer);
            Equal(FileViewMode.Text, viewer.Reader.Mode, "The viewer returns to Text mode");
            Check(Math.Abs(viewer.Reader.FirstVisibleByte - structuredTop) < 100, "Text mode keeps the same source record on screen");
        }),
        new("viewer-structured-identifies-unlabelled-json", "Specialized finds JSON documents without a file extension", async c =>
        {
            var samples = new[]
            {
                ("number", "42"), ("flag", "true"), ("quoted", "\"message\""), ("empty-value", "null"),
                ("commented", "// settings\n{\"mode\": 1}"),
                ("padding", new string(' ', FileSlice.PageSize + 20) + "{\"mode\": 2}")
            };
            foreach (var (name, source) in samples)
            {
                var viewer = await OpenAsync(c, name, source, enter: false);
                Equal(FileViewMode.Text, viewer.Reader.Mode, "The source opens in Text");
                Check(ViewerProbe.Command(viewer, "viewer.mode.specialized").IsEnabled, "The file contents make Specialized available");
                await c.KeyAsync(Key.D4, target: viewer);
                Equal(FileViewMode.Structured, viewer.Reader.Mode, "4 opens the JSON specialist");
                await c.KeyAsync(Key.Escape, target: viewer);
            }
        }),
        new("viewer-structured-folds-and-copy", "Sibling and parent movement, marker clicks, stable layout, and source-range copy", async c =>
        {
            var source = "{\"large\": {\"data\": " + LargeArray(500) + ", \"second\": 2}, \"empty\": [], \"tail\": true}";
            var viewer = await OpenAsync(c, "folds.json", source);
            CheckPath(viewer, "$");
            await MoveAsync(c, viewer, Key.Left);
            CheckPath(viewer, "$");
            var end = Text(viewer).IndexOf("true", StringComparison.Ordinal) + 4;
            SelectRange(viewer, 0, end);
            await c.KeyAsync(Key.C, RawInputModifiers.Control, viewer);
            Equal(source[..^1], await viewer.Clipboard!.TryGetTextAsync(), "Copy across a fold includes the hidden array and original spaces");
            await c.KeyAsync(Key.Home, RawInputModifiers.Control, viewer);
            await MoveAsync(c, viewer, Key.Right);
            CheckPath(viewer, "$.large");
            await ClickMarkerAsync(c, viewer);
            await MoveAsync(c, viewer, Key.Right);
            CheckPath(viewer, "$.large.data");
            await MoveAsync(c, viewer, Key.Right);
            CheckPath(viewer, "$.large.data[0]");
            await MoveAsync(c, viewer, Key.Left);
            await MoveAsync(c, viewer, Key.Left);

            foreach (var scale in new[] { 1d, 1.5d })
            {
                viewer.SetScaleForTest(scale);
                await c.SettleAsync();
                var before = TextPoint(viewer, viewer.Reader.TextContent.CaretIndex);
                await c.KeyAsync(Key.Space, target: viewer);
                Check(!Text(viewer).Contains("record-000000", StringComparison.Ordinal), "Collapsing the parent hides its expanded child");
                CheckPath(viewer, "$.large");
                await c.KeyAsync(Key.Space, target: viewer);
                Check(Text(viewer).Contains("record-000000", StringComparison.Ordinal), "Reopening the parent restores its child's expansion");
                var after = TextPoint(viewer, viewer.Reader.TextContent.CaretIndex);
                Check(Math.Abs(before.Y - after.Y) <= 16, "Expansion keeps the active row on screen");
                var status = viewer.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "ViewerReadingStatus");
                Check(status.Bounds.Height >= 22 && status.Bounds.Width > 200, "The path and group both have room in the rendered status");
                var start = Text(viewer).IndexOf("record-000000", StringComparison.Ordinal);
                SelectRange(viewer, start, start + "record-000000".Length);
                await c.ScreenshotAsync("structured-selection-" + scale * 100, viewer);
                await c.KeyAsync(Key.C, RawInputModifiers.Control, viewer);
                Equal("record-000000", await viewer.Clipboard!.TryGetTextAsync(), "The rendered selection has a source range at both scales");
                for (var up = 0; up < 3; up++) await MoveAsync(c, viewer, Key.Left);
                CheckPath(viewer, "$.large");
            }
            await MoveAsync(c, viewer, Key.Down);
            CheckPath(viewer, "$.empty");
            await MoveAsync(c, viewer, Key.Right);
            await c.KeyAsync(Key.Space, target: viewer);
            CheckPath(viewer, "$.empty");
            await MoveAsync(c, viewer, Key.Down);
            await MoveAsync(c, viewer, Key.Right);
            await MoveAsync(c, viewer, Key.Down);
            CheckPath(viewer, "$.tail");
        }),
        new("viewer-structured-jsonc-search", "Search reveals folded source and restores folds while preserving explicit changes", async c =>
        {
            var source = "// 日本語\r\n{\"outer\": {\"inner\": {\"value\": \"needle " + new string('x', 140) + "\",\"a.b\" /* key */ : -1.20e+09,\"a\\u002eb\": \"\\u2026\",},},}";
            var viewer = await OpenAsync(c, "search.jsonc", source);
            Check(Text(viewer).StartsWith("// 日本語\n{", StringComparison.Ordinal), "A line comment cannot consume the root token");
            Check(!Text(viewer).Contains("needle", StringComparison.Ordinal), "The match starts hidden");
            await c.KeyAsync(Key.F, RawInputModifiers.Control, viewer);
            await c.TextAsync("needle", viewer);
            CheckPath(viewer, "$.outer.inner.value");
            Equal("needle", viewer.Reader.TextContent.SelectedText, "Search selects the matching source");
            Check(Text(viewer).Contains("/* key */", StringComparison.Ordinal) && Text(viewer).Contains("-1.20e+09", StringComparison.Ordinal), "JSONC comments and number spelling remain in the layout");
            await c.KeyAsync(Key.Space, target: viewer);
            await c.TextAsync(" ", viewer);
            Equal("needle ", viewer.Reader.Search.Text, "Space stays in the search field");
            viewer.Reader.TextContent.Focus();
            await c.KeyAsync(Key.C, RawInputModifiers.Control, viewer);
            Equal("needle ", await viewer.Clipboard!.TryGetTextAsync(), "Copy uses the search selection's source range");
            await MoveAsync(c, viewer, Key.Down);
            CheckPath(viewer, "$.outer.inner[\"a.b\"]");
            await MoveAsync(c, viewer, Key.Down);
            CheckPath(viewer, "$.outer.inner[\"a.b\"]#2");
            await MoveAsync(c, viewer, Key.Left);
            await c.KeyAsync(Key.Space, target: viewer);
            await c.KeyAsync(Key.Space, target: viewer);
            await c.KeyAsync(Key.Escape, target: viewer);
            Check(!Text(viewer).Contains("needle", StringComparison.Ordinal), "Clearing search restores the outer fold");
            await c.KeyAsync(Key.Home, RawInputModifiers.Control, viewer);
            await MoveAsync(c, viewer, Key.Right);
            await c.KeyAsync(Key.Space, target: viewer);
            Check(Text(viewer).Contains("needle", StringComparison.Ordinal), "An explicit inner expansion survives clearing search");
            await c.KeyAsync(Key.A, RawInputModifiers.Control, viewer);
            await c.KeyAsync(Key.C, RawInputModifiers.Control, viewer);
            Equal(source, await viewer.Clipboard!.TryGetTextAsync(), "Select all copies the original comments, line endings, escapes, and duplicate keys");
        }),
        new("viewer-structured-large-cancel-and-refresh", "A virtual page crosses source pages and an obsolete index never replaces the reader", async c =>
        {
            const string name = "large.json";
            var source = LargeArray(25_000);
            var viewer = await MeasureLargeViewAsync(c, name, source);
            Check(source.Length > 64 * FileSlice.PageSize, "The fixture spans many source pages");
            Check(Text(viewer).Length < source.Length / 10, "The reader renders a small window of a large expanded array");
            await c.KeyAsync(Key.End, RawInputModifiers.Control, viewer);
            Check(Text(viewer).Contains("record-024999", StringComparison.Ordinal), "The actual tail is reachable");
            Equal(100d, viewer.Reader.ViewedPercent, "Reading at the tail reports 100 percent");
            await c.KeyAsync(Key.F, RawInputModifiers.Control, viewer);
            await c.TextAsync("record-012345", viewer);
            CheckPath(viewer, "$[12345].name");
            Check(Text(viewer).Contains("record-012345", StringComparison.Ordinal), "Search crosses source pages while rendering a bounded window");
            viewer.Reader.TextContent.Focus();
            await MoveAsync(c, viewer, Key.Left);
            await MeasureGroupExpansionAsync(c, viewer);
            CheckPath(viewer, "$[12345]");
            Check(Text(viewer).Length < source.Length / 10, "Expanding all peers still renders only a small window");

            var path = viewer.Reader.CurrentPath;
            var gate = new StructuredReadGate();
            using var controlled = new StructuredViewerScope(await SyntaxProbe.OpenAsync(c, path, gate));
            c.PressKey(Key.D4, target: controlled.Window);
            await gate.WaitForIndexAsync();
            Check(controlled.Window.Reader.ReadingStatus.Contains("Indexing JSON", StringComparison.Ordinal), "The original view remains readable while indexing");
            c.PressKey(Key.D1, target: controlled.Window);
            gate.Release();
            await WorkspaceProbe.WaitAsync(() => gate.WasCancelled);
            Equal(FileViewMode.Text, controlled.Window.Reader.Mode, "Returning to Text cancels the index");

            var changed = new StructuredReadGate();
            using var newer = new StructuredViewerScope(await SyntaxProbe.OpenAsync(c, path, changed));
            c.PressKey(Key.D4, target: newer.Window);
            await changed.WaitForIndexAsync();
            await File.WriteAllTextAsync(path, "{\"replacement\": 42}", Encoding.UTF8);
            changed.Release();
            await SyntaxProbe.WaitForSyntaxAsync(newer.Window);
            Equal(FileViewMode.Text, newer.Window.Reader.Mode, "An index built across a source change is rejected");
            Check(newer.Window.Reader.Error?.Contains("changed", StringComparison.OrdinalIgnoreCase) == true, "The reader explains the source change");
            await c.KeyAsync(Key.D4, target: newer.Window);
            await SyntaxProbe.WaitForSyntaxAsync(newer.Window);
            Check(Text(newer.Window).Contains("replacement", StringComparison.Ordinal), "Retry indexes the replacement source");
        }),
        new("viewer-structured-input-scope", "Reader commands leave menus and feedback input alone", async c =>
        {
            var viewer = await OpenAsync(c, "input.json", LargeArray(500));
            var content = Text(viewer);
            var firstByte = viewer.Reader.FirstVisibleByte;
            var menu = await OpenContextMenuAsync(c, viewer);
            await c.KeyAsync(Key.Space, target: viewer);
            Equal(content, Text(viewer), "Space in the context menu leaves folds alone");
            Equal(firstByte, viewer.Reader.FirstVisibleByte, "Space in the context menu leaves the viewport alone");
            menu.Close();
            viewer.Reader.TextContent.Focus();
            await c.KeyAsync(Key.F12, target: viewer);
            Check(c.Window.Feedback.IsActive, "Viewer feedback is active");
            await c.KeyAsync(Key.Right, RawInputModifiers.Alt, viewer);
            Equal(content, Text(viewer), "A structural shortcut leaves the reader alone during feedback");
            await c.KeyAsync(Key.Escape, target: viewer);
            CheckPath(viewer, "$");
        }),
        new("viewer-structured-invalid-source", "Invalid and unsupported input stays in the ordinary reader with an explanation", async c =>
        {
            foreach (var (name, text) in new[] { ("incomplete.json", "{\"a\": [1, 2"), ("leading-zero.json", "[01]") })
            {
                var viewer = await OpenAsync(c, name, text);
                Equal(FileViewMode.Text, viewer.Reader.Mode, "The viewer does not expose a partial structure");
                Equal(text, Text(viewer), "The original text stays available");
                Check(viewer.Reader.Error?.StartsWith("Could not open Structured view", StringComparison.Ordinal) == true, "The viewer explains why Structured mode could not open");
                await c.KeyAsync(Key.Escape, target: viewer);
            }
            foreach (var (name, text) in new[] { ("records.ndjson", "{\"n\":1}\n{\"n\":2}"), ("settings.ini", "[General]\nmode=full") })
            {
                var unsupported = await OpenAsync(c, name, text);
                Check(!ViewerProbe.Command(unsupported, "viewer.mode.specialized").IsEnabled, "Unsupported formats have no specialized view");
                Equal(FileViewMode.Text, unsupported.Reader.Mode, "Pressing 4 leaves unsupported input readable as text");
                Equal(text, Text(unsupported), "The original input stays available");
                await c.KeyAsync(Key.Escape, target: unsupported);
            }
        })
    ];
}

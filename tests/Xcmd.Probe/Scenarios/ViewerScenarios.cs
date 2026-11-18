// @tests DiscoverShortcuts, OpenViewers, ChooseViewMode, ReadLargeFiles, FindViewerText, OpenViewedFile, OpenExternalTool, ReportControl, TryLocalRelease, ExternalLaunch, SharedControls, ImageDecoding, ApplicationLifecycle
using System.Text;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;
using static Xcmd.Probe.Infrastructure.ViewerProbe;

namespace Xcmd.Probe.Scenarios;

public static class ViewerScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("viewer-text-search-edit-return", "UC08; UC10; F3 viewing and F4 editor handoff", async c =>
        {
            c.AddFile("viewer-notes.txt", "# Project notes\r\n\r\n## File access\r\n\r\nThe archive is read-only. Keep the original files unchanged.\r\nCopy working files into the review directory.\r\n\r\n## Open items\r\n\r\n- Review the exported data.\r\n- Compare the image sizes.\r\n- Check the missing translations.\r\n\r\n## Text samples\r\n\r\n\u041F\u0440\u0438\u0432\u0435\u0442 · \u65E5\u672C\u8A9E · €\r\n");
            await c.Window.Left.LoadAsync(c.LeftPath); await MeasureOpenAsync(c, "viewer-notes.txt");
            var viewer = c.Window.Viewer!;
            CheckReadingLayout(viewer);
            await c.ScreenshotAsync("reading-viewer", viewer);
            Equal(FileViewMode.Text, viewer.Reader.Mode, "Text mode is automatic");
            Check(viewer.Reader.TextContent.IsReadOnly, "Viewing does not edit the file");
            Check(viewer.Reader.TextContent.Text!.Contains("\u65E5\u672C\u8A9E", StringComparison.Ordinal), "Unicode text is decoded");
            await c.TextAsync("ordinary", viewer); Check(!viewer.Reader.IsSearchVisible, "Plain typing does not start search");
            await StartSearchAsync(c, viewer, "read-only");
            Equal("read-only", viewer.Reader.TextContent.SelectedText, "Alt plus a letter starts search and plain text continues it");
            await c.KeyAsync(Key.Tab, target: viewer);
            Check(viewer.Reader.SearchStatus.Contains("Wrapped", StringComparison.Ordinal), "Tab finds the next match and wraps");
            await c.KeyAsync(Key.Tab, RawInputModifiers.Shift, viewer);
            Equal("read-only", viewer.Reader.TextContent.SelectedText, "Shift+Tab finds the previous match");
            await c.KeyAsync(Key.F4, target: viewer);
            Equal(CommandLine.FormatArgument(viewer.Reader.CurrentPath), c.Launcher.Requests.Single().Arguments, "F4 sends the current file to the editor");
            await c.ScreenshotAsync("text-viewer", viewer);
            await c.KeyAsync(Key.Escape, target: viewer);
            Check(c.Window.Viewer == viewer && viewer.Reader.Search.Text == "", "The first Escape clears search");
            await c.KeyAsync(Key.Escape, target: viewer);
            Check(c.Window.Viewer == null && c.Window.Left.Table.IsKeyboardFocusWithin, "Escape restores panel focus");
            Equal("viewer-notes.txt", c.Window.Left.Current?.Name, "Closing keeps the original cursor");
            await c.KeyAsync(Key.F4); Equal(2, c.Launcher.Requests.Count, "F4 also works from the panel");
        }),
        new("viewer-encodings-and-page-boundaries", "UC10; real text encodings and Unicode page boundaries", async c =>
        {
            foreach (var encoding in new Encoding[] { new UTF8Encoding(true), Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.UTF32 })
            {
                var path = Path.Combine(c.LeftPath, "encoded.txt");
                await File.WriteAllTextAsync(path, "\u041F\u0440\u0438\u0432\u0435\u0442 \u65E5\u672C\u8A9E €", encoding);
                await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("encoded.txt"); await c.KeyAsync(Key.F3);
                var viewer = c.Window.Viewer!;
                Equal(FileViewMode.Text, viewer.Reader.Mode, "Unicode BOM files use text mode");
                Equal("\u041F\u0440\u0438\u0432\u0435\u0442 \u65E5\u672C\u8A9E €", viewer.Reader.TextContent.Text, "BOM text decodes without a visible BOM");
                await c.KeyAsync(Key.Escape, target: viewer);
            }
            var legacy = LegacySample(1251);
            await File.WriteAllBytesAsync(Path.Combine(c.LeftPath, "legacy.txt"), legacy);
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("legacy.txt"); await c.KeyAsync(Key.F3);
            var legacyViewer = c.Window.Viewer!;
            Check(legacyViewer.Reader.TextContent.Text!.StartsWith("\u042D\u0442\u043E", StringComparison.Ordinal), "Windows-1251 is detected automatically");
            await ChooseEncodingAsync(c, legacyViewer, 6);
            Check(legacyViewer.Reader.Information.Contains("manual", StringComparison.Ordinal), "The user can override the guessed encoding");
            Check(legacyViewer.Reader.ReadingStatus.Contains("Manual", StringComparison.Ordinal), "Status identifies a manually selected encoding");
            await c.KeyAsync(Key.Escape, target: legacyViewer);
            var text = new string('a', FileSlice.PageSize - 1) + "€tail";
            c.AddFile("boundary.txt", text); await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("boundary.txt"); await c.KeyAsync(Key.F3);
            var paged = c.Window.Viewer!; var first = paged.Reader.TextContent.Text;
            await c.KeyAsync(Key.PageDown, RawInputModifiers.Alt, paged);
            Check(text == first + paged.Reader.TextContent.Text, "A UTF-8 character crossing a page is neither lost nor duplicated");
        }),
        new("viewer-binary-large-and-empty", "UC10; bounded binary reading, mode switching and empty files", async c =>
        {
            WriteLargeFile(Path.Combine(c.LeftPath, "large.bin"), 1024L * 1024 * 1024);
            await c.Window.Left.LoadAsync(c.LeftPath); await MeasureOpenAsync(c, "large.bin");
            var viewer = c.Window.Viewer!;
            Equal(FileViewMode.Hex, viewer.Reader.Mode, "Binary data opens as hex");
            Check(viewer.Reader.TextContent.Text!.StartsWith("00000000  00 00", StringComparison.Ordinal), "Hex includes byte offsets and values");
            Check(viewer.Reader.TextContent.Text.Length < 100_000, "A large file loads a bounded page");
            await c.KeyAsync(Key.End, RawInputModifiers.Control, viewer);
            Equal(1024L * 1024 * 1024 - FileSlice.PageSize, viewer.Reader.ByteOffset, "Jump to the last page does not scan the file");
            await c.KeyAsync(Key.D2, target: viewer); Equal(FileViewMode.Binary, viewer.Reader.Mode, "2 selects fixed-width binary text");
            await c.ScreenshotAsync("binary-viewer", viewer);
            await c.KeyAsync(Key.D1, target: viewer); Equal(FileViewMode.Text, viewer.Reader.Mode, "1 forces text mode");
            await c.KeyAsync(Key.Escape, target: viewer);
            c.AddFile("empty.txt", ""); await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("empty.txt"); await c.KeyAsync(Key.F3);
            Equal("", c.Window.Viewer!.Reader.TextContent.Text, "An empty file is readable");
            Check(c.Window.Viewer.Reader.Information.Contains("Empty file", StringComparison.Ordinal), "The status identifies an empty file");
        }),
        new("viewer-images-and-fallback", "UC09; image preview, byte inspection and decode failures", async c =>
        {
            await File.WriteAllBytesAsync(Path.Combine(c.LeftPath, "picture.bin"), ImageSample());
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("picture.bin"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            Equal(FileViewMode.Image, viewer.Reader.Mode, "Image signatures take priority over the file extension");
            Check(viewer.Reader.HasImage && viewer.Reader.Error == null, "The real PNG sample decodes");
            CheckTextActions(viewer, false);
            await c.ScreenshotAsync("image-viewer", viewer);
            await c.KeyAsync(Key.D3, target: viewer); Check(viewer.Reader.TextContent.Text!.Contains("89 50 4E 47", StringComparison.Ordinal), "Image bytes remain accessible");
            CheckTextActions(viewer, true);
            await c.KeyAsync(Key.A, RawInputModifiers.Control, viewer);
            Check(viewer.Reader.TextContent.SelectedText.Length > 0, "A byte view can select the visible file data");
            await c.KeyAsync(Key.D4, target: viewer); Check(viewer.Reader.HasImage, "4 restores the image");
            CheckTextActions(viewer, false);
            await c.KeyAsync(Key.Escape, target: viewer);
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3); viewer = c.Window.Viewer!;
            await c.KeyAsync(Key.D4, target: viewer);
            Equal(FileViewMode.Text, viewer.Reader.Mode, "An unsupported mode does not replace the current view");
            Check(viewer.Reader.Error == null && !Command(viewer, "viewer.mode.image").IsEnabled, "Unavailable image mode is disabled without an error pane");
            Equal("abc", viewer.Reader.TextContent.Text, "An unavailable image mode preserves the text");
        }),
        new("viewer-whole-file-incremental-search", "UC10; type-to-search, next and previous matches across pages", async c =>
        {
            var contents = new string('a', FileSlice.PageSize - 3) + "Needle" + new string('b', FileSlice.PageSize) + "NEEDLE\r\n12345";
            c.AddFile("search.txt", contents); await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("search.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            await StartSearchAsync(c, viewer, "needle");
            Equal("Needle", viewer.Reader.TextContent.SelectedText, "The first match can cross a page boundary");
            await c.KeyAsync(Key.F3, target: viewer);
            Equal("NEEDLE", viewer.Reader.TextContent.SelectedText, "F3 finds a later match outside the original page");
            Check(viewer.Reader.ByteOffset >= FileSlice.PageSize, "The view scrolls to another part of the file");
            await c.KeyAsync(Key.F3, RawInputModifiers.Shift, viewer);
            Equal("Needle", viewer.Reader.TextContent.SelectedText, "Shift+F3 returns to the previous match");
            await c.KeyAsync(Key.Escape, target: viewer);
            await c.KeyAsync(Key.F, RawInputModifiers.Control, viewer);
            await c.TextAsync("12345", viewer);
            Equal("12345", viewer.Reader.TextContent.SelectedText, "Numbers belong to the query while searching");
            viewer.Reader.Search.SelectAll(); await c.TextAsync("missing", viewer);
            Equal("No matches in this file", viewer.Reader.SearchStatus, "An unsuccessful query has explicit feedback");
            viewer.Reader.Search.SelectAll(); await c.TextAsync("needle", viewer);
            Equal("Needle", viewer.Reader.TextContent.SelectedText, "Changing the query replaces the previous search");
        }),
        new("viewer-reading-menu-search-layout", "UC10; content-first reading and discoverable keyboard commands", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            CheckReadingLayout(viewer);
            Equal(Key.F4, Command(viewer, "viewer.edit").InputGesture!.Key, "The menu shows the editor shortcut");
            Equal(Key.F3, Command(viewer, "viewer.find-next").InputGesture!.Key, "The menu shows the search shortcut");
            await c.ClickAsync((Avalonia.Controls.MenuItem)viewer.Reader.Commands.Items[1]!, target: viewer);
            Check(viewer.Reader.Commands.IsOpen, "View commands can be found in the menu");
            await c.ScreenshotAsync("viewer-menu", viewer);
            await c.KeyAsync(Key.Escape, target: viewer);
            Check(c.Window.Viewer == viewer && !viewer.Reader.Commands.IsOpen, "Escape closes the menu before the viewer");
            await c.KeyAsync(Key.W, target: viewer);
            Equal(Avalonia.Media.TextWrapping.NoWrap, viewer.Reader.TextContent.TextWrapping, "Word wrap has a direct shortcut");
            Check(!Command(viewer, "viewer.wrap").IsChecked, "The menu reflects the wrap state");
            await c.KeyAsync(Key.I, RawInputModifiers.Control, viewer); Check(viewer.Reader.IsInformationVisible, "File details appear only when requested");
            await c.KeyAsync(Key.I, RawInputModifiers.Control, viewer); CheckReadingLayout(viewer);
            await c.KeyAsync(Key.F, RawInputModifiers.Control, viewer);
            Check(viewer.Reader.IsSearchVisible && viewer.Reader.Search.IsKeyboardFocusWithin, "Ctrl+F reveals and focuses search");
            await c.KeyAsync(Key.Escape, target: viewer); CheckReadingLayout(viewer);
            await c.KeyAsync(Key.F12, target: viewer);
            Check(c.Window.Feedback.IsPickingIn(viewer), "Feedback remains available without a permanent button");
            CheckFeedbackHint(viewer, true);
            await c.ClickAsync(viewer.Reader.TextContent, target: viewer);
            Check(c.Window.Feedback.ActiveComment != null, "Actual pointer input selects content for feedback");
            await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveComment);
            Check(c.Window.Viewer == viewer, "Cancelling feedback keeps the viewer open");
            CheckFeedbackHint(viewer, false);
        }),
        new("viewer-read-failure-and-keyboard-recovery", "UC09; UC10; recover from a missing file without leaving the keyboard", async c =>
        {
            c.AddFile("viewer-a.txt", "First file"); c.AddFile("viewer-b.txt", "Second file");
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("viewer-a.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            var missing = Path.Combine(c.LeftPath, "viewer-b.txt");
            File.Delete(missing);
            await c.KeyAsync(Key.Right, RawInputModifiers.Alt, viewer);
            Equal(missing, viewer.Reader.CurrentPath, "File navigation reports the unavailable file");
            Check(viewer.Reader.Error != null && !viewer.Reader.IsLoading, "A read failure completes with a visible error");
            Check(viewer.Reader.ReadingStatus.StartsWith("Could not load file", StringComparison.Ordinal) && viewer.Reader.Progress.Text != "…", "A read failure does not leave a loading status");
            CheckTextActions(viewer, false);
            await c.KeyAsync(Key.F4, target: viewer);
            Check(viewer.Reader.Error!.Contains("no longer exists", StringComparison.Ordinal), "F4 remains available and explains the missing file");
            await File.WriteAllTextAsync(missing, "Restored file");
            await c.KeyAsync(Key.F4, target: viewer);
            Equal(CommandLine.FormatArgument(missing), c.Launcher.Requests.Single().Arguments, "F4 works before the view is reloaded");
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, viewer);
            Equal(2, c.Launcher.Requests.Count, "Open externally works before the view is reloaded");
            await c.KeyAsync(Key.Left, RawInputModifiers.Alt, viewer);
            Equal("First file", viewer.Reader.TextContent.Text, "Alt+Left returns to the readable file");
            Check(viewer.Reader.Error == null, "Successful navigation clears the read error");
            CheckTextActions(viewer, true);
            await c.KeyAsync(Key.Right, RawInputModifiers.Alt, viewer);
            Equal("Restored file", viewer.Reader.TextContent.Text, "Alt+Right can retry a restored file");
        }),
        new("viewer-continuous-scroll-end-and-status", "UC10; real file boundaries, continuous scrolling and visible-file progress", async c =>
        {
            c.AddFile("reading.log", string.Concat(Enumerable.Range(0, 5000).Select(i => $"Row {i:D5}: encoded sample € with enough text to scroll.\r\n")) + "ACTUAL FILE END");
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("reading.log"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            Check(viewer.Reader.VisibleEnd > 0 && viewer.Reader.ViewedPercent < 10, "Progress uses the visible bottom, not the loaded window");
            Check(viewer.Reader.ReadingStatus.Contains("Text (Auto)", StringComparison.Ordinal) && viewer.Reader.ReadingStatus.Contains("UTF-8", StringComparison.Ordinal), "Status shows the detected mode and encoding");
            Check(viewer.Reader.ReadingStatus.Contains("Wrap on", StringComparison.Ordinal), "Status shows word wrap");
            var firstEnd = viewer.Reader.VisibleEnd;
            await WheelAsync(c, viewer, -5);
            Check(viewer.Reader.VisibleEnd > firstEnd, "The mouse wheel updates actual visible progress");
            for (var i = 0; i < 20; i++) await c.KeyAsync(Key.PageDown, target: viewer);
            Check(viewer.Reader.ByteOffset > 0 && viewer.Reader.ViewedPercent < 100, "PageDown crosses the initial read boundary without a false end");
            await c.KeyAsync(Key.End, target: viewer);
            Check(viewer.Reader.TextContent.Text!.EndsWith("ACTUAL FILE END", StringComparison.Ordinal), "End loads the actual file tail");
            Equal(100d, viewer.Reader.ViewedPercent, "The actual end reports 100 percent");
            await c.ScreenshotAsync("reading-end", viewer);
            await c.KeyAsync(Key.Home, target: viewer);
            Equal(0L, viewer.Reader.ByteOffset, "Home returns to the actual beginning");
            Check(viewer.Reader.ViewedPercent < 10, "Progress returns to the visible beginning");
            viewer.Reader.FileScroll.Value = viewer.Reader.FileScroll.Maximum / 2; await c.SettleAsync();
            Check(viewer.Reader.FirstVisibleByte > viewer.Reader.FileLength / 3 && viewer.Reader.FirstVisibleByte < viewer.Reader.FileLength * 2 / 3, "The scroll bar spans the whole file");
            await c.KeyAsync(Key.W, target: viewer);
            Check(viewer.Reader.ReadingStatus.Contains("Wrap off", StringComparison.Ordinal), "W toggles wrapping and updates status");
            await c.KeyAsync(Key.D3, target: viewer);
            Check(viewer.Reader.ReadingStatus.StartsWith("Hex", StringComparison.Ordinal), "Mode changes update status");
            await c.KeyAsync(Key.F, RawInputModifiers.Control, viewer); await c.TextAsync("w1234", viewer);
            Equal(FileViewMode.Hex, viewer.Reader.Mode, "Mode and wrap keys become text inside search");
            await c.KeyAsync(Key.Escape, target: viewer); await c.KeyAsync(Key.Escape, target: viewer);
            c.AddFile("wrapped.txt", string.Concat(Enumerable.Repeat("Word wrap sample. ", 2000)));
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("wrapped.txt"); await c.KeyAsync(Key.F3);
            viewer = c.Window.Viewer!;
            var wrappedHeight = TextHeight(viewer);
            await c.KeyAsync(Key.W, target: viewer);
            Check(TextHeight(viewer) < wrappedHeight / 4, "W changes the actual rendered line layout");
        }),
        new("viewer-feedback-header-and-image-availability", "UC09; UC10; compact window header and safe view-mode selection", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            CheckReadingLayout(viewer); CheckTitleBar(viewer);
            Check(!viewer.Reader.ReadingStatus.Contains('%') && viewer.Reader.Progress.Text!.Contains('%'), "Percentage is shown only in the header");
            await c.ScreenshotAsync("viewer-header", viewer);
            viewer.Width = 640; viewer.Height = 400; await c.SettleAsync();
            CheckTitleBar(viewer); await c.ScreenshotAsync("viewer-header-narrow", viewer);
            viewer.Width = 1120; viewer.Height = 760; await c.SettleAsync();
            await c.ClickAsync(Button(viewer, "ViewerMaximize"), target: viewer);
            Equal(Avalonia.Controls.WindowState.Maximized, viewer.WindowState, "The combined header can maximize the viewer");
            await c.ClickAsync(Button(viewer, "ViewerMaximize"), target: viewer);
            Equal(Avalonia.Controls.WindowState.Normal, viewer.WindowState, "The combined header can restore the viewer");
            await c.ClickAsync(Button(viewer, "ViewerCloseWindow"), target: viewer);
            Check(c.Window.Viewer == null, "The combined header closes its viewer");
            WriteOversizedImage(Path.Combine(c.LeftPath, "large-picture.png"));
            await File.WriteAllBytesAsync(Path.Combine(c.LeftPath, "broken-picture.png"), new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            await c.Window.Left.LoadAsync(c.LeftPath);
            foreach (var name in new[] { "large-picture.png", "broken-picture.png" })
            {
                await c.SelectAsync(name); await c.KeyAsync(Key.F3); viewer = c.Window.Viewer!;
                Equal(FileViewMode.Hex, viewer.Reader.Mode, "An unavailable automatic image view falls back to readable bytes");
                Check(viewer.Reader.Error == null && !Command(viewer, "viewer.mode.image").IsEnabled, "The unavailable mode is disabled");
                var text = viewer.Reader.TextContent.Text;
                await c.KeyAsync(Key.D4, target: viewer);
                Equal(FileViewMode.Hex, viewer.Reader.Mode, "The image shortcut cannot leave the working mode");
                Equal(text, viewer.Reader.TextContent.Text, "The working view remains unchanged");
                await c.KeyAsync(Key.Escape, target: viewer);
            }
        }),
        new("viewer-feedback-independent-windows", "UC09; independent viewers and concurrent panel navigation", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var first = c.Window.Viewer!;
            Check(c.Window.IsEnabled && !c.Window.HasOpenDialog, "Opening a viewer does not block the file manager");
            await c.ClickAsync(c.Row("folder1")); await c.KeyAsync(Key.Enter); await c.SelectAsync("inside.txt"); await c.KeyAsync(Key.F3);
            var second = c.Window.Viewers.Last();
            Equal(2, c.Window.Viewers.Count, "Another file opens in a separate viewer");
            Equal("abc", first.Reader.TextContent.Text, "The first viewer keeps its file");
            Equal("Nested file", second.Reader.TextContent.Text, "The second viewer has independent contents");
            await c.ClickAsync(first.Reader.TextContent, target: first); await c.KeyAsync(Key.F4, target: first);
            Equal(c.LeftPath, c.Launcher.Requests.Last().WorkingDirectory, "Editing uses the original source directory after panel navigation");
            Check(c.Window.Readiness.DialogOpen, "Open viewers delay automatic release replacement");
            await c.ClickAsync(second.Reader.TextContent, target: second); await c.KeyAsync(Key.Escape, target: second);
            Check(first.IsVisible && c.Window.Viewers.Count == 1, "Closing one viewer keeps the other open");
            c.Window.Close();
            Check(!first.IsVisible && c.Window.Viewers.Count == 0, "Closing Xcmd closes its remaining viewer windows");
        }),
        new("viewer-native-editor-boundary", "UC08; real Windows external editor launch contract", VerifyEditorBoundaryAsync)
    ];
}

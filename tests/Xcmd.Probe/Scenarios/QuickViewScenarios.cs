// @tests QuickViewFile, ChooseViewMode, ReadLargeFiles, FindViewerText, HighlightSource, FollowLog, ReportControl, InspectArchives
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.FileOperationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class QuickViewScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("quick-view-follows-cursor-and-restores-panel", "Preview follows the keyboard and restores the opposite panel", async c =>
        {
            c.AddFile("preview-a.txt", "First preview · 日本語\n\nText in the other panel.");
            await File.WriteAllBytesAsync(Path.Combine(c.LeftPath, "preview-b.png"), ViewerProbe.ImageSample());
            await c.Window.Left.LoadAsync(c.LeftPath);
            foreach (var number in Enumerable.Range(1, 40)) File.WriteAllText(Path.Combine(c.RightPath, $"file-{number:D2}.txt"), "Destination file");
            await c.Window.Right.LoadAsync(c.RightPath);
            await c.KeyAsync(Key.Tab);
            await c.KeyAsync(Key.F5, RawInputModifiers.Control);
            await c.KeyAsync(Key.End);
            await c.KeyAsync(Key.Up, RawInputModifiers.Shift);
            var before = c.Window.Right.CaptureState();
            Check(before.ScrollY > 0 && before.HasMarks, "The destination has a scrolled list and marked files");
            await c.SelectAsync("preview-a.txt", c.Window.Left);
            await ToggleAsync(c);
            Check(!c.Window.Right.IsVisible && Find<Border>(c, "QuickViewPane").IsVisible, "The quick view replaces the right panel");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin && c.Window.Viewers.Count == 0, "Opening quick view keeps focus in the file list");
            Check(PreviewText(c).Contains("First preview · 日本語", StringComparison.Ordinal), "Quick view decodes text");
            await c.ScreenshotAsync("quick-view-text-100");
            await c.KeyAsync(Key.Down);
            Equal("preview-b.png", c.Window.Left.Current?.Name, "Down moves the source cursor");
            Check(Reader(c).HasImage && Reader(c).Mode == FileViewMode.Image, "Moving the cursor previews an image");
            var width = c.Window.Width;
            var height = c.Window.Height;
            c.Window.Width = c.Window.MinWidth;
            c.Window.Height = c.Window.MinHeight;
            c.Window.SetScaleForTest(1.5);
            await c.ScreenshotAsync("quick-view-image-150");
            await c.KeyAsync(Key.F12);
            await c.ClickAsync(Find<Button>(c, "QuickViewClose"));
            Check(c.Window.Feedback.ActiveComment != null, "Preview controls are selectable for feedback");
            await c.KeyAsync(Key.Escape, target: c.Window.Feedback.ActiveComment);
            c.Window.SetScaleForTest(1);
            c.Window.Width = width;
            c.Window.Height = height;
            await ToggleAsync(c);
            var after = c.Window.Right.CaptureState();
            Check(c.Window.Right.IsVisible && !Find<Border>(c, "QuickViewPane").IsVisible, "A second Ctrl+Q restores the file list");
            Equal(before.DirectoryPath, after.DirectoryPath, "Restoration keeps the directory");
            Equal(before.Cursor, after.Cursor, "Restoration keeps the cursor");
            Equal(before.Sort, after.Sort, "Restoration keeps the sort");
            Equal(before.ScrollY, after.ScrollY, "Restoration keeps the scroll position");
            Check(before.Selected.SequenceEqual(after.Selected), "Restoration keeps the selection");
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Closing quick view keeps the source focused");
        }),
        new("quick-view-from-right-and-panel-switch", "Quick view works in both directions and closes on Escape or Tab", async c =>
        {
            await c.SelectAsync("destination.txt", c.Window.Right);
            await ToggleAsync(c);
            Check(!c.Window.Left.IsVisible && c.Window.Right.Table.IsKeyboardFocusWithin, "A right-side file previews on the left");
            Equal("Destination file", PreviewText(c), "The right-side file is shown");
            await c.KeyAsync(Key.Home);
            Equal("Select a file to preview.", Reader(c).ReadingStatus, "A directory clears the previous file");
            Equal("", PreviewText(c), "The old file contents are cleared");
            await c.KeyAsync(Key.Escape);
            Check(c.Window.Left.IsVisible && c.Window.Right.Table.IsKeyboardFocusWithin, "Escape restores the opposite list");
            await c.KeyAsync(Key.Down);
            await ToggleAsync(c);
            await c.KeyAsync(Key.Tab);
            Check(c.Window.Left.IsVisible && c.Window.Left.Table.IsKeyboardFocusWithin, "Tab restores and switches to the opposite list");
        }),
        new("quick-view-archive-and-cancelled-read", "Quick view reads archive entries and replaces pending reads when the cursor moves", async c =>
        {
            ArchiveProbe.Zip(Path.Combine(c.LeftPath, "preview.zip"), ("a.txt", "First archived file"), ("b.txt", "Second archived file"));
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync("preview.zip");
            await c.KeyAsync(Key.Enter);
            await c.SelectAsync("a.txt");
            var paused = c.Archives.PauseNextViewerRead(c.Window.Left.Current!.Entry.FullPath);
            c.PressKey(Key.Q, RawInputModifiers.Control);
            await WaitUntilAsync(() => paused.Entered.Task.IsCompleted);
            await c.KeyAsync(Key.Down);
            await WaitUntilAsync(() => paused.WasCancelled);
            Equal("Second archived file", PreviewText(c), "The new selection replaces the pending read");
            paused.Release.TrySetResult();
            await c.KeyAsync(Key.Up);
            Equal("First archived file", PreviewText(c), "Returning to the first entry loads its contents");
            await c.KeyAsync(Key.F3);
            Equal("First archived file", c.Window.Viewer?.Reader.TextContent.Text, "F3 still opens the file in the full viewer");
        }),
        new("quick-view-missing-file", "Quick view recovers from a missing file", async c =>
        {
            c.AddFile("preview-a-readable.txt", "Readable file");
            c.AddFile("preview-b-missing.txt", "This file is removed after listing.");
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync("preview-a-readable.txt");
            File.Delete(Path.Combine(c.LeftPath, "preview-b-missing.txt"));
            await ToggleAsync(c);
            await c.KeyAsync(Key.Down);
            Check(Reader(c).Error != null, "A read error appears in the preview pane");
            await c.KeyAsync(Key.Up);
            Equal("Readable file", PreviewText(c), "Moving to a readable file recovers without closing quick view");
        }),
        new("quick-view-shared-reader-features", "Quick view keeps reader modes, highlighting, full file navigation and search", async c =>
        {
            var contents = string.Concat(Enumerable.Repeat("public static string Sample = \"value\";\r\n", 1000)) + "// Marker at the end\r\n";
            c.AddFile("preview.cs", contents);
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync("preview.cs");
            await ToggleAsync(c);
            var reader = Reader(c);
            Equal("C#", reader.SyntaxStatus, "Quick view detects the syntax language");
            SyntaxProbe.CheckMarker(reader.TextContent.Syntax!, "public", SyntaxKind.Keyword);
            Check(reader.VisibleEnd > 0 && reader.VisibleEnd < reader.FileLength, "Progress starts at the visible portion of the file");
            Check(reader.FileScroll.Maximum > FileSlice.PageSize, "The scroll bar covers the full file");
            var navigate = (MenuItem)reader.Commands.Items[4]!;
            Check(navigate.Items.OfType<MenuItem>().All(item => item.Name is not ("viewer.previous-file" or "viewer.next-file")), "The embedded reader has only within-file navigation");

            await c.KeyAsync(Key.Tab, RawInputModifiers.Control);
            Check(reader.TextContent.IsKeyboardFocusWithin, "Ctrl+Tab focuses the embedded reader");
            await c.KeyAsync(Key.D3);
            Equal(FileViewMode.Hex, reader.Mode, "3 switches to hex in the embedded reader");
            await c.KeyAsync(Key.D2);
            Equal(FileViewMode.Binary, reader.Mode, "2 switches to binary in the embedded reader");
            await c.KeyAsync(Key.D1);
            Equal(FileViewMode.Text, reader.Mode, "1 returns to text");
            await c.KeyAsync(Key.End, RawInputModifiers.Control);
            Check(reader.ByteOffset > FileSlice.PageSize && reader.FirstVisibleByte > 0, "Ctrl+End reads beyond the initial page");
            Equal(reader.FileLength, reader.VisibleEnd, "Ctrl+End reaches the actual end");
            Equal(100d, reader.ViewedPercent, "Reading progress reaches 100 percent");
            await c.KeyAsync(Key.I, RawInputModifiers.Control);
            Check(reader.IsInformationVisible && reader.Information.Contains("Page", StringComparison.Ordinal), "File details include the current page");
            await c.KeyAsync(Key.F, RawInputModifiers.Control);
            await c.TextAsync("Marker at the end");
            Equal("Marker at the end", reader.TextContent.SelectedText, "Search finds text beyond the initial page");
            Equal("", c.Window.CommandLine.Editor.Text ?? "", "Search typing stays inside the reader");
            await c.ScreenshotAsync("quick-view-reader-search");
            await c.KeyAsync(Key.Escape);
            Check(!reader.IsSearchVisible && reader.IsEffectivelyVisible, "Escape clears the reader search first");
            await c.KeyAsync(Key.Right, RawInputModifiers.Alt);
            Equal("preview.cs", Path.GetFileName(reader.CurrentPath), "Alt+Right does not change files in quick view");
            await c.KeyAsync(Key.Tab, RawInputModifiers.Control);
            Check(c.Window.Left.Table.IsKeyboardFocusWithin, "Ctrl+Tab returns focus to the file list");
            await c.KeyAsync(Key.Tab, RawInputModifiers.Control);
            await c.ClickAsync(navigate);
            var returnToList = navigate.Items.OfType<MenuItem>().Single(item => item.Name == "viewer.focus-file-list");
            Equal(Key.Tab, returnToList.InputGesture!.Key, "The navigation menu shows the focus shortcut");
            await c.ClickAsync(returnToList);
            Check(c.Window.Left.Table.IsKeyboardFocusWithin && reader.IsEffectivelyVisible, "The menu returns focus and leaves quick view open");
            await c.KeyAsync(Key.Up);
            Check(reader.FirstVisibleByte == 0 && !reader.IsSearchVisible, "Moving in the list opens the next file at the beginning");
        }),
        new("quick-view-follows-growing-log", "Refreshing the file list keeps the embedded reader at the tail of a growing file", async c =>
        {
            c.AddFile("live.log", "First line\r\n");
            await c.Window.Left.LoadAsync(c.LeftPath);
            await c.SelectAsync("live.log");
            await ToggleAsync(c);
            var reader = Reader(c);
            await c.KeyAsync(Key.Tab, RawInputModifiers.Control);
            await c.KeyAsync(Key.F, RawInputModifiers.Control | RawInputModifiers.Shift);
            Check(reader.IsFollowing, "The embedded reader follows a log");

            File.AppendAllText(Path.Combine(c.LeftPath, "live.log"), "Second line\r\n");
            await c.Window.Left.RefreshAsync(() => true, () => false);
            await WaitUntilAsync(() => PreviewText(c).Contains("Second line", StringComparison.Ordinal));
            Check(reader.IsFollowing && reader.ReadingStatus.Contains("Following", StringComparison.Ordinal), "Changing file size does not interrupt following");
        })
    ];

    private static Task ToggleAsync(ProbeContext c) => c.KeyAsync(Key.Q, RawInputModifiers.Control);
    private static FileViewer Reader(ProbeContext c) => c.Window.GetVisualDescendants().OfType<FileViewer>().Single();
    private static string PreviewText(ProbeContext c) => Reader(c).TextContent.Text ?? "";
    private static T Find<T>(ProbeContext c, string name) where T : Control => c.Window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
}

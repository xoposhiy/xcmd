using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.PanelPresentationProbe;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class PanelCursorScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("cursor-navigation-preserves-marks", "UC20; All navigation keys keep marked files", async c =>
        {
            for (var index = 0; index < 80; index++) c.AddFile($"navigation-{index:D3}.txt", "navigation");
            await c.Window.Left.LoadAsync(c.LeftPath); await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var marks = c.Window.Left.SelectedNames().Order().ToArray();
            foreach (var (key, modifiers) in new[]
            {
                (Key.PageDown, RawInputModifiers.None), (Key.PageUp, RawInputModifiers.None),
                (Key.End, RawInputModifiers.None), (Key.Home, RawInputModifiers.None),
                (Key.End, RawInputModifiers.Control), (Key.Home, RawInputModifiers.Control),
                (Key.Down, RawInputModifiers.None), (Key.Up, RawInputModifiers.None)
            })
            {
                await c.KeyAsync(key, modifiers);
                Check(c.Window.Left.SelectedNames().Order().SequenceEqual(marks), $"{modifiers}+{key} preserves file marks.");
            }
            await c.KeyAsync(Key.Z, RawInputModifiers.Alt);
            Equal("zeta.md", c.Window.Left.Current?.Name, "Name jumping moves only the cursor");
            Check(c.Window.Left.SelectedNames().Order().SequenceEqual(marks), "Name jumping preserves file marks.");
            await c.KeyAsync(Key.Escape); await c.KeyAsync(Key.Escape);
            Equal(0, c.Window.Left.SelectedNames().Length, "Escape clears the marks after navigation");
        }),
        new("cursor-appearance-independent-of-marks", "UC20; Cursor and marks keep separate visual meanings", async c =>
        {
            await c.SelectAsync("alpha.txt"); var panel = c.Window.Left;
            var cursor = Background(panel, "alpha.txt");
            var plain = Background(panel, "file2.log");
            Check(cursor != plain, "The cursor is visible without file marks.");
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            Equal(cursor, Background(panel, panel.Current!.Name), "An unmarked cursor keeps the same background with file marks");
            Equal(plain, Background(panel, "alpha.txt"), "A marked row has no cursor background after the cursor leaves");
            var marked = panel.Rows.Single(row => row.Name == "alpha.txt");
            var color = ((ISolidColorBrush)marked.NameForeground).Color;
            Check(marked.MarkGlyph.Length > 0, "Marked files keep a check and amber text.");
            await c.KeyAsync(Key.Up);
            Equal(cursor, Background(panel, "alpha.txt"), "A marked cursor keeps the same background");
            Equal(color, NameColor(panel, "alpha.txt"), "The cursor preserves rendered marked text color");
            await c.ScreenshotAsync("cursor-on-marked-file");
            await c.KeyAsync(Key.Tab); Check(Background(panel, "alpha.txt") != cursor, "An inactive panel has a faint cursor.");
            await c.KeyAsync(Key.Tab); Equal(cursor, Background(panel, "alpha.txt"), "Returning restores the same cursor style");
            await c.KeyAsync(Key.Escape);
            Equal(cursor, Background(panel, "alpha.txt"), "Clearing marks keeps the cursor background");
            await c.ClickAsync(c.Row("alpha.txt"));
            Equal(cursor, Background(panel, "alpha.txt"), "Mouse and keyboard cursors share one style");
            await c.ClickAsync(c.Row("file2.log"), RawInputModifiers.Control);
            Equal(cursor, Background(panel, "file2.log"), "A mouse-selected marked cursor uses the same background");
            Equal(color, NameColor(panel, "file2.log"), "A mouse-selected marked cursor keeps amber text");
            Equal(plain, Background(panel, "alpha.txt"), "Other marked files keep their background");
            await c.ScreenshotAsync("mouse-marks-and-cursor");
        }),
        new("cursor-stable-layout-and-scrollbars", "UC20; Cursor and marks never shift cells or add scroll bars", async c =>
        {
            for (var index = 0; index < 80; index++) c.AddFile($"viewport-{index:D3}.txt", "viewport");
            await c.Window.Left.LoadAsync(c.LeftPath); c.Window.Height = 550;
            foreach (var scale in new[] { 1d, 1.5d })
            foreach (var width in new[] { 1000d, 1280d })
            {
                c.Window.SetScaleForTest(scale); c.Window.Width = width;
                await c.KeyAsync(Key.Escape); await c.SelectAsync("alpha.txt");
                var layout = Geometry(c.Window.Left, "alpha.txt"); NoHorizontalScroll(c.Window.Left);
                await c.KeyAsync(Key.Down); SameGeometry(layout, Geometry(c.Window.Left, "alpha.txt")); NoHorizontalScroll(c.Window.Left);
                await c.KeyAsync(Key.Up); SameGeometry(layout, Geometry(c.Window.Left, "alpha.txt"));
                await c.KeyAsync(Key.Down, RawInputModifiers.Shift); SameGeometry(layout, Geometry(c.Window.Left, "alpha.txt")); NoHorizontalScroll(c.Window.Left);
                await c.KeyAsync(Key.Up); SameGeometry(layout, Geometry(c.Window.Left, "alpha.txt")); NoHorizontalScroll(c.Window.Left);
                await c.KeyAsync(Key.End); NoHorizontalScroll(c.Window.Left);
                await c.KeyAsync(Key.Home); NoHorizontalScroll(c.Window.Left);
                await c.ScreenshotAsync($"stable-table-{width}-{scale * 100}");
            }
            c.Window.Left.Table.Columns[0].Width = new GridLength(900); await c.SettleAsync();
            await c.KeyAsync(Key.End); await c.KeyAsync(Key.Home);
            HorizontalScrollAvailable(c.Window.Left);
        }),
        new("cursor-single-mark-session-roundtrip", "UC38; Preserve one marked file under the cursor", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift); await c.KeyAsync(Key.Up);
            var store = new SessionStore(Path.Combine(c.FixturePath, "marked-session.json"));
            await store.SaveAsync(c.Window.CaptureWorkspace());
            await c.KeyAsync(Key.Escape); await c.Window.RestoreWorkspaceAsync((await store.ReadAsync())!); await c.SettleAsync();
            Check(c.Window.Left.Current is { Name: "alpha.txt", IsMarked: true }, "A single marked cursor survives session restore.");
            await c.KeyAsync(Key.End);
            Check(c.Window.Left.SelectedNames().SequenceEqual(new[] { "alpha.txt" }), "Navigation keeps the restored mark.");
        })
    ];
}

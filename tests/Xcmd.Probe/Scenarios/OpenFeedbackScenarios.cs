// @tests ReadablePanels, ControlSearch, OpenSearchMatch, SearchResultPanel, RestoreWorkspace
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class OpenFeedbackScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("open-feedback-resize-boundaries", "Continuous window resizing with fixed column widths remains responsive", async c =>
        {
            var layouts = 0;
            c.Window.Right.Table.Columns[0].Width = new GridLength(369.33333333333326);
            c.Window.Right.Table.LayoutUpdated += (_, _) =>
            {
                if (++layouts > 80) throw new InvalidOperationException("Column layout does not settle at window width " + c.Window.Width);
            };
            foreach (var scale in new[] { 1d, 1.5d })
            {
                c.Window.SetScaleForTest(scale);
                for (var width = 1280; width >= 840; width--)
                {
                    layouts = 0;
                    c.Window.Width = width;
                    await c.SettleAsync();
                }
                for (var width = 840; width <= 1280; width += 3)
                {
                    layouts = 0;
                    c.Window.Width = width;
                    await c.SettleAsync();
                }
            }
            await c.KeyAsync(Key.Down);
            Check(c.Window.Left.Current != null, "Keyboard navigation still works after resizing");
        }),
        new("open-feedback-search-actions", "UC23; underlined panel access key and context-sensitive primary action", async c =>
        {
            var search = await SearchProbe.OpenAsync(c, "alpha.txt");
            var go = FeedbackRemarksProbe.Button(search, "SearchGo");
            var start = FeedbackRemarksProbe.Button(search, "SearchStart");
            var panel = FeedbackRemarksProbe.Button(search, "SearchToPanel");
            Check(panel.Content is AccessText { AccessKey: "p", ShowAccessKey: true }, "Panel action shows an underlined p");
            Equal("Go to file", go.Content as string, "Go has no Enter suffix");
            Check(go.Classes.Contains("primary") && !start.Classes.Contains("primary"), "Go is primary while results have focus");
            await c.ScreenshotAsync("search-primary-go", search);
            search.NameEditor.Focus(); await c.SettleAsync();
            Check(start.Classes.Contains("primary") && !go.Classes.Contains("primary"), "Search is primary in query fields");
            await c.ScreenshotAsync("search-primary-query", search);
            search.NameEditor.Text = "no-such-file";
            await c.KeyAsync(Key.Enter, target: search);
            Check(!go.IsEnabled && !go.Classes.Contains("primary"), "Unavailable Go is not primary");
            search.NameEditor.Text = "alpha.txt"; search.NameEditor.Focus();
            await c.KeyAsync(Key.Enter, target: search);
            await c.KeyAsync(Key.P, RawInputModifiers.Alt, search);
            Check(c.Window.Left.IsSearchResults, "Alt+P still transfers results");
        }),
        new("open-feedback-responsive-columns", "Narrow panels keep every column reachable without changing saved widths or selection", async c =>
        {
            var panel = c.Window.Left;
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var cursor = panel.Current?.Name;
            var marks = panel.SelectedNames();
            var widths = panel.CaptureState().Columns;
            foreach (var scale in new[] { 1d, 1.5d })
            {
                c.Window.SetScaleForTest(scale);
                foreach (var width in new[] { 600d, 520d, 400d, 250d, 140d, 600d })
                {
                    panel.Width = width;
                    await c.SettleAsync();
                    Check(panel.Table.Columns.All(column => column.IsVisible), "Every column remains available");
                    var scroll = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().First();
                    if (width <= 400) Check(scroll.Extent.Width > scroll.Viewport.Width, "Narrow panels make overflow reachable");
                    Equal(cursor, panel.Current?.Name, "Resize preserves cursor");
                    Check(marks.SequenceEqual(panel.SelectedNames()), "Resize preserves marks");
                    Check(widths.SequenceEqual(panel.CaptureState().Columns), "Responsive layout preserves saved column widths");
                }
                panel.Width = 250; await c.SettleAsync();
                var narrow = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().First();
                narrow.Offset = new Vector(narrow.Extent.Width - narrow.Viewport.Width, narrow.Offset.Y);
                await c.SettleAsync();
                var modified = panel.Table.GetVisualDescendants().OfType<TableViewColumnHeader>().Last();
                var rightEdge = modified.TranslatePoint(new Point(modified.Bounds.Width, 0), panel.Table)!.Value.X;
                Check(narrow.Offset.X > 0 && rightEdge <= narrow.Viewport.Width + 2, "Horizontal scrolling brings the Modified header into view");
                await c.ScreenshotAsync("responsive-columns-" + scale * 100);
            }
            panel.Table.Columns[0].Width = new GridLength(900);
            await c.SettleAsync();
            var viewer = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().First();
            Check(viewer.Extent.Width > viewer.Viewport.Width, "An oversized name width can be scrolled");
            Equal(900d, panel.CaptureState().Columns[0].Width, "Preferred name width remains saved");
        })
    ];
}

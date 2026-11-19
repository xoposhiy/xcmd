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
            // Fractional columns can keep triggering layout near 1231 DIP.
            var widths = Enumerable.Range(0, 12).Select(step => 840 + 40 * step)
                .Concat(Enumerable.Range(1224, 15)).Order().ToArray();
            foreach (var scale in new[] { 1d, 1.5d })
            {
                c.Window.SetScaleForTest(scale);
                foreach (var width in widths.Reverse().Concat(widths.Skip(1)))
                {
                    layouts = 0;
                    c.Window.Width = width;
                    await c.UpdateLayoutAsync();
                    Check(Math.Abs(c.Window.Bounds.Width - width) < 1, "The window reached the requested width");
                }
            }
            var cursor = c.Window.Left.Current;
            await c.KeyAsync(Key.Down);
            Check(c.Window.Left.Current != null && c.Window.Left.Current != cursor, "Down moves the cursor after resizing");
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
        new("open-feedback-responsive-columns", "Narrow panels keep Name readable and hide metadata in order", async c =>
        {
            var panel = c.Window.Left;
            await c.SelectAsync("alpha.txt");
            await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
            var cursor = panel.Current?.Name;
            var marks = panel.SelectedNames();
            var widths = panel.Table.Columns.Skip(1).Select(column => column.Width).ToArray();
            (double Width, int[] Visible)[] layouts =
            [
                (600, [0, 1, 2, 3, 4]), (460, [0, 1, 2, 3]), (330, [0, 2, 3]), (270, [0, 2]), (145, [0])
            ];
            foreach (var scale in new[] { 1d, 1.5d })
            {
                c.Window.SetScaleForTest(scale);
                foreach (var (width, visible) in layouts.Concat(layouts.Reverse().Skip(1)))
                {
                    panel.Width = width;
                    await c.SettleAsync();
                    Check(visible.SequenceEqual(Enumerable.Range(0, panel.Table.Columns.Count)
                        .Where(index => panel.Table.Columns[index].IsVisible)), $"Expected columns at {width} DIP");
                    Check(panel.Table.Columns[0].ActualWidth >= 120, "Name keeps its minimum width");
                    var scroll = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().First();
                    Check(scroll.Extent.Width <= scroll.Viewport.Width + 1, "Visible columns fit without horizontal scrolling");
                    Equal(cursor, panel.Current?.Name, "Resize preserves cursor");
                    Check(marks.SequenceEqual(panel.SelectedNames()), "Resize preserves marks");
                    Check(widths.SequenceEqual(panel.Table.Columns.Skip(1).Select(column => column.Width)), "Responsive layout preserves metadata widths");
                    if (width == 145) await c.ScreenshotAsync("responsive-columns-" + scale * 100);
                }
            }
            panel.Table.Columns[0].Width = new GridLength(900);
            await c.SettleAsync();
            var viewer = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().First();
            Check(viewer.Extent.Width > viewer.Viewport.Width, "An oversized name width can be scrolled");
            Equal(900d, panel.CaptureState().Columns[0].Width, "Preferred name width remains saved");
        })
    ];
}

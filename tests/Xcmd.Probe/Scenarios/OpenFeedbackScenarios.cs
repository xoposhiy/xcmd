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
        new("open-feedback-resize-boundaries", "UC20; continuous window resizing across column visibility boundaries remains responsive", async c =>
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
        new("open-feedback-responsive-columns", "UC20; shrink names and hide rightmost columns without losing saved widths or selection", async c =>
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
                foreach (var (width, visible) in new[] { (520d, 4), (400d, 3), (250d, 2), (140d, 1), (520d, 4) })
                {
                    panel.Width = width;
                    await c.SettleAsync();
                    Equal(visible, panel.Table.Columns.Count(column => column.IsVisible), "Columns hide from the right at width " + width);
                    Check(panel.Table.Columns.Where((_, index) => index != 3).Take(visible).All(column => column.IsVisible), "Available columns hide from the right");
                    var scroll = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().First();
                    Check(scroll.Extent.Width <= scroll.Viewport.Width + 1, "No horizontal overflow while the name minimum fits");
                    Equal(cursor, panel.Current?.Name, "Resize preserves cursor");
                    Check(marks.SequenceEqual(panel.SelectedNames()), "Resize preserves marks");
                    Check(widths.SequenceEqual(panel.CaptureState().Columns), "Responsive layout preserves saved column widths");
                }
                panel.Width = 250; await c.SettleAsync();
                await c.ScreenshotAsync("responsive-columns-" + scale * 100);
            }
            panel.Table.Columns[0].Width = new GridLength(900);
            await c.SettleAsync();
            var viewer = panel.Table.GetVisualDescendants().OfType<ScrollViewer>().First();
            Check(viewer.Extent.Width <= viewer.Viewport.Width + 1, "An oversized restored name width shrinks to available space");
            Equal(900d, panel.CaptureState().Columns[0].Width, "Preferred name width remains saved");
        })
    ];
}

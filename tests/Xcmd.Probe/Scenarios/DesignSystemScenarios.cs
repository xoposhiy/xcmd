// @tests QueueOperations, FindViewerText, ReaderThemes, SharedControls
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class DesignSystemScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("design-system-shared-surfaces", "UI design system; consistent tables, forms, actions and offscreen layout at 100/150%", async c =>
        {
            foreach (var scale in new[] { 1d, 1.5d })
            {
                c.Window.SetScaleForTest(scale); await c.SettleAsync();
                DesignSystemProbe.CheckTable(c.Window.Left.Table);
                await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.Down, RawInputModifiers.Shift);
                await c.ScreenshotAsync("panels-" + scale * 100);
                await c.KeyAsync(Key.Escape);
                c.Window.CommandLine.FocusCommand();
                Equal("Command", c.Window.CommandLine.Editor.PlaceholderText, "Command input has a short hint");
                await c.KeyAsync(Key.Escape);
                await c.KeyAsync(Key.F7);
                var folder = c.Window.ActiveDialog!;
                DesignSystemProbe.CheckActions(folder);
                await c.ScreenshotAsync("new-folder-" + scale * 100, folder);
                await c.KeyAsync(Key.Escape, target: folder);
                await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F5);
                DesignSystemProbe.CheckActions(c.Window.OperationDialog!);
                await c.ScreenshotAsync("copy-" + scale * 100, c.Window.OperationDialog);
                await c.KeyAsync(Key.Escape, target: c.Window.OperationDialog);
                await c.KeyAsync(Key.Down, RawInputModifiers.Alt);
                DesignSystemProbe.CheckActions(c.Window.ActiveNavigationDialog!);
                await c.ScreenshotAsync("navigation-" + scale * 100, c.Window.ActiveNavigationDialog);
                await c.KeyAsync(Key.Escape, target: c.Window.ActiveNavigationDialog);
                var search = await SearchProbe.OpenAsync(c, "alpha.txt");
                DesignSystemProbe.CheckTable(search.Results); DesignSystemProbe.CheckActions(search);
                search.Width = 760; await c.SettleAsync();
                DesignSystemProbe.CheckActions(search);
                await c.ScreenshotAsync("search-narrow-" + scale * 100, search);
                search.Close(); await c.SettleAsync();
            }
        }),
        new("design-system-operation-details", "UI design system; operation history uses structured records and preserves results", async c =>
        {
            await c.SelectAsync("alpha.txt"); await FileOperationProbe.CopyAsync(c);
            c.Window.OperationsView.ToggleDetails(); await c.SettleAsync();
            var table = c.Window.OperationsView.GetVisualDescendants().OfType<TableView>().Single(item => item.Name == "OperationOutcomes");
            DesignSystemProbe.CheckTable(table);
            Check(table.Columns.Select(column => column.Header?.ToString()).SequenceEqual(new[] { "Result", "Source", "Destination", "Explanation" }), "Outcome fields have separate aligned columns");
            var outcome = ((IEnumerable<FileOutcome>)table.ItemsSource!).Single();
            Equal(Path.Combine(c.LeftPath, "alpha.txt"), outcome.Source, "The table retains the operation source");
            Equal(Path.Combine(c.RightPath, "alpha.txt"), outcome.Destination, "The table retains the operation destination");
            await c.ScreenshotAsync("operation-history");
        }),
        new("design-system-reader-contrast", "UI design system; all reader palettes retain readable selection and working search", async c =>
        {
            await c.SelectAsync("alpha.txt"); await c.KeyAsync(Key.F3);
            var viewer = c.Window.Viewer!;
            await ViewerProbe.StartSearchAsync(c, viewer, "a");
            foreach (var id in new[] { "light", "github", "solarized-light", "forest-light", "dark", "solarized-dark", "dracula", "nord", "forest-dark" })
            {
                await AppearanceProbe.ChooseThemeAsync(c, viewer, id);
                await c.ScreenshotAsync("reader-" + id, viewer);
                Equal(id, viewer.ThemeId, "The theme menu applies " + id);
                DesignSystemProbe.CheckSelection(viewer.TextContent);
                Equal("a", viewer.TextContent.SelectedText, "Changing the palette preserves the found text in " + id);
            }
        })
    ];
}

// @tests RefreshEntries, FileIcons
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class IconPresentationScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("icons-navigation-unchanged-refresh", "A background reread keeps visible folder icons when directory entries have not changed", async c =>
        {
            var panel = c.Window.Left;
            foreach (var scale in new[] { 1d, 1.25d, 1.5d })
            {
                c.Window.SetScaleForTest(scale);
                await c.SettleAsync();
                await panel.Icons.Completion;
                var icon = IconPresentationProbe.Icon(c, "folder1");
                Check(icon.Source != null, "The folder icon is visible before the background reread");
                var detached = false;
                icon.DetachedFromVisualTree += (_, _) => detached = true;
                await panel.RefreshAsync(() => true, () => true);
                await c.SettleAsync();
                Check(!detached, "An unchanged directory keeps its displayed icon attached");
                Check(ReferenceEquals(icon, IconPresentationProbe.Icon(c, "folder1")), "An unchanged directory retains the visible icon control");
            }
            c.AddFile("arriving.txt", "new content");
            await panel.RefreshAsync(() => true, () => true);
            Check(panel.Rows.Any(row => row.Name == "arriving.txt"), "A real directory change still appears");
            await c.SettleAsync();
            await panel.Icons.Completion;
            Equal(24, IconPresentationProbe.PixelSize(IconPresentationProbe.Icon(c, "folder1")), "Rebuilt rows retain the correct icon scale");
        }),
        new("icons-navigation-cached-scale", "New name cells reuse icons at the panel scale before attachment", async c =>
        {
            var panel = c.Window.Left;
            foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d, 1d })
            {
                c.Window.SetScaleForTest(scale);
                await c.SettleAsync();
                await panel.Icons.Completion;
                foreach (var name in new[] { "folder1", "alpha.txt" })
                {
                    var visible = IconPresentationProbe.Icon(c, name);
                    var created = IconPresentationProbe.BuildNameIcon(panel, name);
                    Equal((int)Math.Ceiling(16 * scale), IconPresentationProbe.PixelSize(created), "A new cell immediately has the cached icon at the current scale");
                    Check(ReferenceEquals(visible.Source, created.Source), "Cell recreation does not start a second icon load");
                }
            }
        })
    ];
}

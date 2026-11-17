// @implements HeadlessVerification
// @tests NavigatePanels, HeadlessVerification
using Avalonia.Headless;
using Avalonia.Input;

namespace Xcmd.Probe.Infrastructure;

public static class RenderingProbe
{
    public static void MeasurePaging(ProbeContext context)
    {
        var originalScale = context.Window.RenderScaling;
        try
        {
            foreach (var scale in new[] { 1d, 1.5d })
            {
                context.Window.SetScaleForTest(scale);
                context.Input.KeyDown(Key.Home, RawInputModifiers.Control, PhysicalKey.Home, null);
                context.Input.KeyUp(Key.Home, RawInputModifiers.Control, PhysicalKey.Home, null);
                context.Window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var samples = new List<double>();
                for (var page = 0; page < 20; page++)
                {
                    var before = context.Window.ActivePanel.Current?.Name;
                    var timer = ProbeTimer.Start();
                    context.Input.KeyDown(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
                    context.Input.KeyUp(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
                    context.Window.UpdateLayout();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    using var frame = context.Window.CaptureRenderedFrame();
                    ProbeContext.Check(frame != null, "Paging renders a complete software frame.");
                    samples.Add(timer.Elapsed.TotalMilliseconds);
                    ProbeContext.Check(context.Window.ActivePanel.Current?.Name != before, "Each measured page moves the file cursor.");
                }
                context.Measurements[scale == 1 ? "softwarePaging100PercentMs" : "softwarePaging150PercentMs"] = new
                {
                    median = samples.Order().ElementAt(samples.Count / 2),
                    maximum = samples.Max(),
                    samples
                };
            }
        }
        finally { context.Window.SetScaleForTest(originalScale); }
    }
}

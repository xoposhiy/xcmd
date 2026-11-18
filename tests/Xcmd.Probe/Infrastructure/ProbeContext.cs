// @implements HeadlessVerification
// @tests HeadlessVerification
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Xcmd.Probe.Infrastructure;

public sealed partial class ProbeContext : IDisposable
{
    public string OutputPath { get; }
    public InputDriver Input => new(Window);
    public Dictionary<string, object> Measurements { get; } = [];
    public List<string> InputTrace { get; } = [];
    private readonly HashSet<Window> verifiedWindows = [];

    public async Task SettleAsync()
    {
        var watch = Stopwatch.StartNew();
        do
        {
            await Task.Delay(5);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            foreach (var window in OpenWindows()) window.UpdateLayout();
            foreach (var window in OpenWindows()) VerifyHeadlessWindow(window);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            if (watch.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("The application did not become idle.");
        } while (IsBusy);
    }

    private void VerifyHeadlessWindow(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        Check(handle?.Handle == IntPtr.Zero && handle.HandleDescriptor == "STUB", "All application windows use headless stub handles.");
        verifiedWindows.Add(window);
        Measurements["headlessWindowsVerified"] = verifiedWindows.Count;
    }

    public async Task KeyAsync(Key key, RawInputModifiers modifiers = RawInputModifiers.None, Window? target = null)
    {
        PressKey(key, modifiers, target);
        await SettleAsync();
    }

    public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None, Window? target = null)
    {
        target ??= Window;
        InputTrace.Add($"key {modifiers}+{key}");
        var physical = Enum.TryParse<PhysicalKey>(key.ToString(), out var mapped) ? mapped : PhysicalKey.None;
        target.KeyPress(key, modifiers, physical, null);
        if (target.IsVisible) target.KeyRelease(key, modifiers, physical, null);
        else Window.KeyRelease(key, modifiers, physical, null);
    }

    public async Task TextAsync(string text, Window? target = null)
    {
        InputTrace.Add("text " + text);
        (target ?? Window).KeyTextInput(text);
        await SettleAsync();
    }

    public async Task ClickAsync(Control control, RawInputModifiers modifiers = RawInputModifiers.None, Window? target = null)
    {
        target ??= Window;
        target.UpdateLayout();
        var position = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), target)
            ?? throw new InvalidOperationException("Control is not attached to the window.");
        Check(control.Bounds.Width > 0 && control.Bounds.Height > 0, "The clicked control has a nonempty layout.");
        InputTrace.Add("click " + control.Name);
        target.MouseMove(position, modifiers);
        target.MouseDown(position, MouseButton.Left, modifiers);
        (target.IsVisible ? target : Window).MouseUp(position, MouseButton.Left, modifiers);
        await SettleAsync();
    }

    public async Task ClickPartAsync(Control control, double x, double y, Window? target = null)
    {
        target ??= Window; target.UpdateLayout();
        var point = control.TranslatePoint(new Point(control.Bounds.Width * x, control.Bounds.Height * y), target)!.Value;
        InputTrace.Add($"click part {control.Name} {x},{y}");
        target.MouseMove(point); target.MouseDown(point, MouseButton.Left);
        (target.IsVisible ? target : Window).MouseUp(point, MouseButton.Left);
        await SettleAsync();
    }

    public async Task ScreenshotAsync(string name, Window? target = null)
    {
        await SettleAsync();
        SaveFrame(name, target);
    }

    private void CaptureFailure(string name, Window? target = null)
    {
        (target ?? Window).UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        SaveFrame(name, target);
    }

    private void SaveFrame(string name, Window? target)
    {
        using var image = (target ?? Window).CaptureRenderedFrame() ?? throw new InvalidOperationException("Offscreen rendering returned no image.");
        Check(image.PixelSize.Width >= (target == null ? 840 : 400) && image.PixelSize.Height >= (target == null ? 400 : 120), "The rendered frame has the expected size.");
        image.Save(Path.Combine(OutputPath, name + ".png"), PngBitmapEncoderOptions.Default);
        Measurements["frameWidth"] = image.PixelSize.Width;
        Measurements["frameHeight"] = image.PixelSize.Height;
    }

    public void SaveSnapshot() => File.WriteAllText(Path.Combine(OutputPath, "state.json"),
        JsonSerializer.Serialize(Snapshot(), new JsonSerializerOptions { WriteIndented = true }));

    public static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: expected '{expected}', actual '{actual}'.");
}

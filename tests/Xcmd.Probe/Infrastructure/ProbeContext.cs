using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xcmd.Desktop;

namespace Xcmd.Probe.Infrastructure;

public sealed class ProbeContext : IDisposable
{
    public string FixturePath { get; }
    public string LeftPath => Path.Combine(FixturePath, "left");
    public string RightPath => Path.Combine(FixturePath, "right");
    public string OutputPath { get; }
    public ControlledFileSystem FileSystem { get; } = new();
    public RecordingExternalLauncher Launcher { get; } = new();
    public ControlledArchiveContents Archives { get; }
    public CommanderWindow Window { get; }
    public InputDriver Input => new(Window);
    public Dictionary<string, object> Measurements { get; } = [];
    public List<string> InputTrace { get; } = [];
    private readonly HashSet<Window> verifiedWindows = [];

    public ProbeContext(string outputPath)
    {
        Archives = new ControlledArchiveContents(Path.Combine(outputPath, "archive-cache"));
        Window = new CommanderWindow(FileSystem, Launcher, editorPath: Path.Combine(AppContext.BaseDirectory, "launch-probe", "Xcmd.LaunchProbe.exe"), archiveContents: Archives);
        OutputPath = outputPath;
        FixturePath = Path.Combine(outputPath, "fixture");
        Directory.CreateDirectory(LeftPath);
        Directory.CreateDirectory(RightPath);
        Directory.CreateDirectory(Path.Combine(LeftPath, "folder1"));
        Directory.CreateDirectory(Path.Combine(LeftPath, "folder2"));
        File.WriteAllText(Path.Combine(LeftPath, "folder1", "inside.txt"), "Nested file");
        AddFile("alpha.txt", "abc", new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddFile("file2.log", new string('b', 20), new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddFile("file10.bin", new string('c', 10), new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddFile("zeta.md", "notes", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        AddFile(".env", "a");
        AddFile("отчёт 日本語.txt", "Unicode file contents");
        AddFile(new string('a', 180) + ".txt", "Long file name");
        File.WriteAllText(Path.Combine(RightPath, "destination.txt"), "Destination file");
    }

    public void AddFile(string name, string contents, DateTime? modified = null)
    {
        var file = Path.Combine(LeftPath, name);
        File.WriteAllText(file, contents);
        if (modified != null) File.SetLastWriteTimeUtc(file, modified.Value);
    }

    public async Task StartAsync()
    {
        Window.Show();
        await Window.InitializeAsync(LeftPath, RightPath);
        await SettleAsync();
        Window.Left.FocusCurrent();
        await KeyAsync(Key.Down);
        await KeyAsync(Key.Tab); await KeyAsync(Key.Down); await KeyAsync(Key.Tab);
        await SettleAsync();
    }

    public async Task SettleAsync()
    {
        var watch = Stopwatch.StartNew();
        do
        {
            await Task.Delay(5);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Window.UpdateLayout();
            if (Window.ActiveDialog is { } dialog) dialog.UpdateLayout();
            if (Window.ActiveNavigationDialog is { } history) history.UpdateLayout();
            Window.OperationDialog?.UpdateLayout();
            foreach (var viewer in Window.Viewers) viewer.UpdateLayout();
            Window.Feedback.ActiveComment?.UpdateLayout();
            Window.Feedback.ActiveNotes?.UpdateLayout();
            VerifyHeadlessWindow(Window);
            if (Window.ActiveDialog is { } activeDialog) VerifyHeadlessWindow(activeDialog);
            if (Window.ActiveNavigationDialog is { } activeHistory) VerifyHeadlessWindow(activeHistory);
            if (Window.OperationDialog is { } operationDialog) VerifyHeadlessWindow(operationDialog);
            foreach (var viewer in Window.Viewers) VerifyHeadlessWindow(viewer);
            if (Window.Feedback.ActiveComment is { } comment) VerifyHeadlessWindow(comment);
            if (Window.Feedback.ActiveNotes is { } notes) VerifyHeadlessWindow(notes);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            if (watch.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("The application did not become idle.");
        } while ((Window.IsBusy && !Window.Operations.IsWaitingForDecision) || Window.Feedback.HasPendingSelection);
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

    public async Task SelectAsync(string name, PanelView? panel = null)
    {
        panel ??= Window.ActivePanel;
        if (Window.ActivePanel != panel) await KeyAsync(Key.Tab);
        await KeyAsync(Key.Home, RawInputModifiers.Control);
        var index = panel.Rows.FindIndex(row => row.Name == name);
        Check(index >= 0, "Fixture item exists: " + name);
        for (var i = 0; i < index; i++) await KeyAsync(Key.Down);
        Equal(name, panel.Current?.Name, "Keyboard cursor");
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

    public Task ClickHeaderAsync(string field, PanelView? panel = null)
    {
        panel ??= Window.ActivePanel;
        return ClickAsync(panel.Table.GetVisualDescendants().OfType<Button>().Single(button => button.Name == panel.Id + "Sort" + field));
    }

    public Control Row(string name, PanelView? panel = null)
    {
        panel ??= Window.ActivePanel;
        var index = panel.Rows.FindIndex(row => row.Name == name);
        panel.Table.ScrollIntoView(index);
        panel.Table.UpdateLayout();
        return panel.Table.ContainerFromIndex(index) ?? throw new InvalidOperationException("The row is not realized.");
    }

    public async Task BeginRenameAsync(string name)
    {
        await SelectAsync(name);
        await KeyAsync(Key.F6, RawInputModifiers.Shift);
        Check(Window.ActivePanel.EditingRow != null, "Rename mode is active.");
        var editor = Window.ActivePanel.RenameEditor;
        Check(editor?.IsFocused == true, "The inline editor has keyboard focus.");
        Equal(name, editor?.SelectedText, "The full name, including the extension, is selected.");
    }

    public async Task ScreenshotAsync(string name, Window? target = null)
    {
        await SettleAsync();
        SaveFrame(name, target);
    }

    public void CaptureFailure(string name, Window? target = null)
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

    public object Snapshot() => new
    {
        activePanel = Window.ActivePanel.Id,
        focused = (Window.FocusManager?.GetFocusedElement() as Control)?.GetType().Name,
        busy = Window.IsBusy,
        error = Window.LastError,
        dialog = Window.ActiveDialog?.Title ?? Window.ActiveNavigationDialog?.Title ?? Window.OperationDialog?.Title ?? Window.Viewer?.Title,
        viewer = Window.Viewer is { } viewer ? new { viewer.CurrentPath, viewer.Mode, viewer.ByteOffset, viewer.FileLength, viewer.Information, viewer.Error, viewer.IsLoading, viewer.HasImage, viewer.ReadingStatus, viewer.FirstVisibleByte, viewer.VisibleEnd, viewer.ViewedPercent } : null,
        operations = Window.Operations.Jobs.Select(job => new { job.Id, job.State, job.CurrentPath, job.BytesTransferred, job.TotalFiles, job.ProcessedFiles, job.SkippedFiles, job.FailedEntries, job.TotalBytes, job.ProcessedBytes, job.TotalsComplete, job.Outcomes }),
        historyDialog = Window.ActiveNavigationDialog is { } history ? new { selected = history.Directories.SelectedItem, history.IsOpening, history.Error } : null,
        left = PanelSnapshot(Window.Left),
        right = PanelSnapshot(Window.Right),
        input = InputTrace,
        actions = Window.Trace,
        measurements = Measurements
    };

    private static object PanelSnapshot(PanelView panel) => new
    {
        path = panel.Session.DirectoryPath,
        count = panel.Rows.Count,
        names = panel.Rows.Take(100).Select(row => row.Name),
        selected = panel.SelectedNames(),
        current = panel.Current?.Name,
        editing = panel.EditingRow?.DraftName,
        sort = panel.Session.SortOrder,
        error = panel.Session.Error,
        realizedRows = panel.Table.GetRealizedContainers().Count(),
        history = panel.Session.PreviousDirectories,
        columns = panel.Table.Columns.Select(column => column.ActualWidth)
    };

    public void SaveSnapshot() => File.WriteAllText(Path.Combine(OutputPath, "state.json"),
        JsonSerializer.Serialize(Snapshot(), new JsonSerializerOptions { WriteIndented = true }));

    public static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: expected '{expected}', actual '{actual}'.");

    public void Dispose()
    {
        Window.ActiveDialog?.Close();
        Window.ActiveNavigationDialog?.Close();
        Window.OperationDialog?.Close();
        foreach (var viewer in Window.Viewers.ToArray()) viewer.Close();
        Window.Feedback.ActiveComment?.Close();
        Window.Feedback.ActiveNotes?.Close();
        Window.Close();
    }
}

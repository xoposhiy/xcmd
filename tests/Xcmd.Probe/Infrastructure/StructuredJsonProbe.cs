// @tests ChooseViewMode, FindViewerText, ReadLargeFiles
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

public static class StructuredJsonProbe
{
    public const RawInputModifiers Peer = RawInputModifiers.Control | RawInputModifiers.Alt;

    public static string Text(FileViewerWindow viewer) => viewer.Reader.TextContent.Text!;

    public static void CheckPath(FileViewerWindow viewer, string path) => ProbeContext.Check(
        viewer.Reader.ReadingStatus.StartsWith("Structured · " + path + " · ", StringComparison.Ordinal), viewer.Reader.ReadingStatus);

    public static async Task MoveAsync(ProbeContext context, FileViewerWindow viewer, Key key, RawInputModifiers modifiers = RawInputModifiers.Alt)
    {
        await context.KeyAsync(key, modifiers, viewer);
        await WorkspaceProbe.WaitAsync(() => !viewer.Reader.ReadingStatus.Contains("Updating peers", StringComparison.Ordinal));
    }

    public static async Task<FileViewerWindow> OpenAsync(ProbeContext context, string name, string text, bool enter = true)
    {
        context.AddFile(name, text);
        await context.Window.Left.LoadAsync(context.LeftPath);
        await context.SelectAsync(name); await context.KeyAsync(Key.F3);
        var viewer = context.Window.Viewer!;
        if (enter) await context.KeyAsync(Key.D4, target: viewer);
        return viewer;
    }

    public static void SelectRange(FileViewerWindow viewer, int start, int end)
    {
        viewer.Reader.TextContent.Focus();
        viewer.Reader.TextContent.CaretIndex = end;
        viewer.Reader.TextContent.SelectionStart = start;
        viewer.Reader.TextContent.SelectionEnd = end;
    }

    public static async Task ClickMarkerAsync(ProbeContext context, FileViewerWindow viewer)
    {
        var index = Text(viewer).IndexOf('⟪');
        ProbeContext.Check(index >= 0, "The compact container has a clickable fold marker");
        var point = TextPoint(viewer, index + 2);
        viewer.MouseMove(point);
        viewer.MouseDown(point, MouseButton.Left); viewer.MouseUp(point, MouseButton.Left);
        await context.SettleAsync();
    }

    public static async Task<ContextMenu> OpenContextMenuAsync(ProbeContext context, FileViewerWindow viewer)
    {
        var point = TextPoint(viewer, viewer.Reader.TextContent.CaretIndex);
        viewer.MouseDown(point, MouseButton.Right); viewer.MouseUp(point, MouseButton.Right);
        await context.SettleAsync();
        var menu = viewer.Reader.TextContent.ContextMenu!;
        ProbeContext.Check(menu.IsOpen, "The reader context menu is open");
        return menu;
    }

    public static async Task<FileViewerWindow> MeasureLargeViewAsync(ProbeContext context, string name, string source)
    {
        var timer = Stopwatch.StartNew();
        var viewer = await OpenAsync(context, name, source);
        context.Measurements["openLargeStructuredMs"] = timer.Elapsed.TotalMilliseconds;
        return viewer;
    }

    public static async Task MeasureGroupExpansionAsync(ProbeContext context, FileViewerWindow viewer)
    {
        var timer = Stopwatch.StartNew();
        await MoveAsync(context, viewer, Key.Right, Peer);
        context.Measurements["expand25000PeersMs"] = timer.Elapsed.TotalMilliseconds;
    }

    public static Point TextPoint(FileViewerWindow viewer, int index)
    {
        var presenter = viewer.Reader.TextContent.GetVisualDescendants().OfType<TextPresenter>().Single();
        var bounds = presenter.TextLayout.HitTestTextPosition(index);
        return presenter.TranslatePoint(new Point(bounds.X + 1, bounds.Y + bounds.Height / 2), viewer)!.Value;
    }

    public static string LargeArray(int count) => "[\n" + string.Join(",\n", Enumerable.Range(0, count)
        .Select(index => $$$"""{"id":{{{index}}},"name":"record-{{{index:D6}}}","detail":{"city":"Berlin"}}""")) + "\n]";
}

public sealed record StructuredViewerScope(FileViewerWindow Window) : IDisposable
{
    public void Dispose() => Window.Close();
}

public sealed class StructuredReadGate : IViewerFiles
{
    private readonly ViewerFiles files = new();
    private readonly TaskCompletionSource opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool WasCancelled { get; private set; }
    public Task WaitForIndexAsync() => opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
    public void Release() => released.TrySetResult();

    public async Task<Stream> OpenTextAsync(string path, CancellationToken cancellationToken)
    {
        opened.TrySetResult();
        await released.Task;
        WasCancelled = cancellationToken.IsCancellationRequested;
        return await files.OpenTextAsync(path, CancellationToken.None);
    }

    public Task<ViewerFileVersion?> VersionAsync(string path, CancellationToken cancellationToken) => files.VersionAsync(path, cancellationToken);
    public Task<FileSlice> ReadAsync(string path, long offset, CancellationToken cancellationToken) => files.ReadAsync(path, offset, cancellationToken);
    public Task<byte[]> ReadImageAsync(string path, CancellationToken cancellationToken) => files.ReadImageAsync(path, cancellationToken);
    public Task<FileTextMatch?> FindAsync(FileTextQuery query, CancellationToken cancellationToken) => files.FindAsync(query, cancellationToken);
}

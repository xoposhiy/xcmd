// @tests OpenViewers, ReleaseHandoff, RestoreWorkspace, ReadWorkingFileDiff, InspectAgentSession
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer;
using Xcmd.Desktop.Workspace;

namespace Xcmd.Probe.Infrastructure;

internal static class ViewerHandoffProbe
{
    public const string Flag = "XCMD_TEST_OPEN_VIEWERS";
    public const string LongFile = "handoff-long.txt";
    public const string MissingFile = "handoff-missing.txt";
    public const string LockedFile = "handoff-locked.txt";
    public const string DiffFile = "handoff-diff.txt";
    private const string DiffStart = "First line\n";

    public static async Task<IDisposable> PrepareFilesAsync(ProbeContext context)
    {
        var directory = context.LeftPath;
        File.WriteAllLines(Path.Combine(directory, LongFile), Enumerable.Repeat("A line long enough to build several viewer pages.", 1600));
        File.WriteAllText(Path.Combine(directory, MissingFile), "This file disappears before the update.");
        File.WriteAllText(Path.Combine(directory, LockedFile), "This file is locked while the next release opens.");
        var end = string.Concat(Enumerable.Range(0, 90).Select(row => $"Kept row {row:D3}\n"));
        var removed = string.Concat(Enumerable.Range(0, 150).Select(row => $"Deleted row {row:D3}\n"));
        context.AddFile(DiffFile, DiffStart + removed + end);
        var repository = await ViewerDiffProbe.InitializeAsync(context, DiffFile);
        context.AddFile(DiffFile, DiffStart + end);
        return repository;
    }

    public static async Task OpenViewersAsync(CommanderWindow window)
    {
        var longView = await OpenAsync(window, LongFile);
        longView.Position = new PixelPoint(125, 155);
        Press(longView, Key.D3, PhysicalKey.Digit3);
        await WorkspaceProbe.WaitAsync(() => !longView.Reader.IsLoading);
        Press(longView, Key.PageDown, PhysicalKey.PageDown);
        Press(longView, Key.PageDown, PhysicalKey.PageDown);
        longView.UpdateLayout();
        ProbeContext.Check(longView.Reader.FirstVisibleByte > 0, "The fixture scrolls away from the start of the long file.");
        var minimized = await OpenAsync(window, "alpha.txt");
        minimized.WindowState = WindowState.Minimized;
        await OpenAsync(window, MissingFile);
        await OpenAsync(window, LockedFile);
        await OpenDiffAsync(window);
        OpenInspectors(window);
        foreach (var viewer in window.Viewers) InputDriver.Deactivate(viewer);
        InputDriver.Deactivate(window);
        ProbeContext.Check(window.Readiness.CanRestart, "The background fixture allows updates with its viewers open.");
    }

    private static void OpenInspectors(CommanderWindow window)
    {
        var directory = window.Left.Session.DirectoryPath;
        var transcript = Path.Combine(directory, "inspection.jsonl");
        File.WriteAllText(transcript, """{"timestamp":"2026-01-01T00:00:00Z","type":"event_msg","payload":{"type":"user_message","message":"Inspect the sample"}}""");
        var session = new AgentSession(SessionAgent.Codex, Guid.NewGuid(), "First inspection", directory, DateTimeOffset.UtcNow) { TranscriptPath = transcript };
        window.RestoreInspectors([
            new(session, new(40, 60, 720, 530, false), null),
            new(session with { Id = Guid.NewGuid(), Topic = "Second inspection" }, new(100, 120, 680, 480, false, Minimized: true), null)
        ]);
        foreach (var inspector in window.SessionInspectors) InputDriver.Deactivate(inspector);
    }

    public static object Describe(CommanderWindow window) => window.Viewers.Select(viewer => new
    {
        viewer.Reader.CurrentPath,
        viewer.Reader.Mode,
        viewer.Reader.FirstVisibleByte,
        viewer.Reader.Error,
        DiffPosition = viewer.Reader.FileScroll.Value,
        viewer.Reader.ReadingStatus,
        viewer.Position.X,
        viewer.Position.Y,
        viewer.WindowState,
        viewer.ShowActivated
    }).ToArray();

    private static async Task<FileViewerWindow> OpenAsync(CommanderWindow window, string name)
    {
        window.Left.SelectRow(window.Left.Rows.Single(row => row.Name == name));
        Press(window, Key.F3, PhysicalKey.F3);
        var viewer = window.Viewers.Last();
        await WorkspaceProbe.WaitAsync(() => !viewer.Reader.IsLoading);
        viewer.UpdateLayout();
        return viewer;
    }

    private static async Task OpenDiffAsync(CommanderWindow window)
    {
        var viewer = await OpenAsync(window, DiffFile);
        Press(viewer, Key.D, PhysicalKey.D, RawInputModifiers.Control);
        await WorkspaceProbe.WaitAsync(() => viewer.Reader.ReadingStatus.StartsWith("Diff", StringComparison.Ordinal));
        viewer.Reader.FileScroll.Value = viewer.Reader.FileScroll.Maximum / 3;
        ProbeContext.Equal((long)DiffStart.Length, viewer.Reader.FirstVisibleByte, "The viewer lands inside the deleted block.");
    }

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
    }
}

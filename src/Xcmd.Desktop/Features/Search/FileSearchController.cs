// @implements FindFiles, OpenSearchMatch, SearchResultPanel, RestoreWorkspace
using Xcmd.Core;
using Avalonia.Controls;
using Xcmd.Desktop.Features.Feedback;
using Xcmd.Desktop.Shared.Platform;

namespace Xcmd.Desktop.Features.Search;

public sealed class FileSearchController
{
    private readonly Window owner;
    private readonly FeedbackController feedback;
    private readonly Action<string> trace;
    private readonly string? cacheRoot;
    private FileSearchState state = new();
    public FileSearchWindow? Window { get; private set; }
    public bool IsOpen => Window != null;
    internal event Action? OpenRequested;

    internal FileSearchController(Window owner, FeedbackController feedback, Action<string> trace, string? cacheRoot)
    {
        this.owner = owner;
        this.feedback = feedback;
        this.trace = trace;
        this.cacheRoot = cacheRoot;
    }

    public void Close() => Window?.Close();
    public FileSearchState Capture() => Window?.CaptureState() ?? state;
    public void Restore(FileSearchState? saved) => state = saved ?? new();

    public void Open()
    {
        if (Window is { } existing)
        {
            if (existing.WindowState == Avalonia.Controls.WindowState.Minimized) existing.WindowState = Avalonia.Controls.WindowState.Normal;
            existing.Activate(); existing.FocusQuery(); return;
        }
        OpenRequested?.Invoke();
    }

    internal void Show(string directory, Action<FileSearchWindow> connect, Action closed)
    {
        var dialog = new FileSearchWindow(directory, feedback, state, cacheRoot) { Background = owner.Background }.WithFeedbackId("FileSearchController.dialog");
        Window = dialog;
        feedback.Attach(dialog, showButton: false);
        connect(dialog);
        dialog.Opened += (_, _) => TaskbarIdentity.Configure(dialog, trace);
        dialog.Closed += (_, _) =>
        {
            state = dialog.CaptureState();
            Window = null;
            closed();
        };
        dialog.Show();
        dialog.FocusQuery();
    }
}

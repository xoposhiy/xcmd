// @implements ReadCommitMessage
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.Panels.Git;

internal sealed class GitCommitMessageView : Border
{
    private readonly RowDefinition row = new() { Height = new GridLength(0) };
    private readonly TextBlock title = new TextBlock
    {
        Text = "Commit message",
        FontSize = UiPalette.MetaSize,
        Foreground = UiPalette.Secondary,
        Margin = new Thickness(8, 6, 8, 0),
        TextTrimming = TextTrimming.CharacterEllipsis
    }.WithFeedbackId("PanelView.commitMessageTitle");
    public TextBox Editor { get; } = new TextBox
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        IsUndoEnabled = false,
        TextWrapping = TextWrapping.Wrap,
        FontSize = UiPalette.BodySize,
        FontFamily = new FontFamily("Consolas"),
        Padding = new Thickness(8, 4),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent
    }.WithFeedbackId("PanelView.commitMessage");
    private CancellationTokenSource? request;
    private string? messagePath;
    private GridLength preferredHeight = new(0.35, GridUnitType.Star);
    private GridSplitter divider = null!;
    private RowDefinition listRow = null!;
    public bool IsLoading { get; private set; }

    public GitCommitMessageView(string id, Action activated)
    {
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*") }.WithFeedbackId("PanelView.commitMessageBody");
        body.Children.Add(title);
        Grid.SetRow(Editor, 1); body.Children.Add(Editor);
        ScrollViewer.SetVerticalScrollBarVisibility(Editor, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(Editor, ScrollBarVisibility.Disabled);
        AutomationProperties.SetAutomationId(Editor, id + ".git.commit-message");
        AutomationProperties.SetName(Editor, "Full commit message");
        Child = body; BorderThickness = new Thickness(0, 1, 0, 0); BorderBrush = UiPalette.Divider; IsVisible = false;
        Editor.GotFocus += (_, _) => activated();
        DetachedFromVisualTree += (_, _) => request?.Cancel();
    }

    public void Attach(Grid content)
    {
        content.RowDefinitions[2] = row;
        listRow = content.RowDefinitions[1];
        Grid.SetRow(this, 2); content.Children.Add(this);
        divider = new GridSplitter
        {
            Name = "GitMessageDivider",
            Height = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ResizeDirection = GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndCurrent,
            Focusable = false,
            Background = Brushes.Transparent,
            IsVisible = false
        }.WithFeedbackId("GitCommitMessageView.divider");
        ToolTip.SetTip(divider, "Drag to resize the commit message.");
        divider.DragCompleted += (_, _) => preferredHeight = row.Height;
        Grid.SetRow(divider, 2); content.Children.Add(divider);
    }

    public void Update(PanelSession session, FileEntry? entry)
    {
        var directory = GitPath.IsEntry(session.DirectoryPath) ? GitPath.Parse(session.DirectoryPath) : null;
        var snapshot = directory != null && directory.Revision.Length > 0;
        var visible = snapshot || directory?.Kind == "log";
        UpdateVisibility(visible);
        var path = MessagePath(session.DirectoryPath, directory, entry, snapshot, visible);
        if (messagePath == path) return;
        messagePath = path;
        request?.Cancel();
        title.Text = "Commit message";
        ToolTip.SetTip(title, null);
        Editor.Foreground = UiPalette.Text;
        if (!visible || path == null || !GitPath.IsEntry(path) || !snapshot && entry?.IsParent == true)
        {
            ShowNavigationMessage(session, visible);
            return;
        }
        var location = GitPath.Parse(path);
        if (location.Kind != "tree")
        {
            title.Text = entry?.Name ?? "Commit message";
            Editor.Text = location.Kind == "working" ? "Uncommitted changes in this history scope. Press F3 to view the changed files."
                : "Press Enter to load more commits.";
            return;
        }
        if (session.FileSystem is not GitFileSystem files) return;
        var commit = files.Commit(path);
        title.Text = commit.ShortId + " · " + commit.Author + " · " + commit.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        ToolTip.SetTip(title, title.Text);
        var next = new CancellationTokenSource();
        request = next;
        _ = LoadAsync(files, path, next);
    }

    private void UpdateVisibility(bool visible)
    {
        if (IsVisible == visible) return;
        IsVisible = visible;
        divider.IsVisible = visible;
        row.Height = visible ? preferredHeight : new GridLength(0);
        row.MinHeight = visible ? 96 : 0;
        listRow.MinHeight = visible ? 84 : 0;
    }

    private static string? MessagePath(string path, GitLocation? directory, FileEntry? entry, bool snapshot, bool visible)
    {
        if (snapshot) return GitPath.Tree(directory!.Repository, directory.Context, directory.Revision);
        if (!visible) return null;
        return entry?.IsParent == true ? path : entry?.FullPath;
    }

    private void ShowNavigationMessage(PanelSession session, bool visible)
    {
        var empty = visible && session.FileSystem is GitFileSystem source ? source.EmptyLogMessage(session.DirectoryPath) : null;
        Editor.Text = empty ?? "Select a commit to read its full message.";
    }

    private async Task LoadAsync(GitFileSystem files, string path, CancellationTokenSource next)
    {
        IsLoading = true;
        Editor.Text = "Reading commit message…";
        try
        {
            var message = await files.CommitMessageAsync(path, next.Token);
            if (next.IsCancellationRequested || messagePath != path) return;
            Editor.Text = message; Editor.CaretIndex = 0;
            Editor.SelectionStart = 0; Editor.SelectionEnd = 0;
        }
        catch (OperationCanceledException) when (next.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (!next.IsCancellationRequested && messagePath == path)
            { Editor.Text = error.Message; Editor.Foreground = UiPalette.Error; }
        }
        finally
        {
            if (request == next) { request = null; IsLoading = false; }
            next.Dispose();
        }
    }

    public void HandleKey(KeyEventArgs e, Action focusList)
    {
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None) return;
        focusList(); e.Handled = true;
    }
}

// @implements BrowseHistory, BrowseSnapshot, InspectCommitChanges, InspectWorkingChanges, ReadDiff
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.Git;

public sealed partial class GitHistoryController
{
    private readonly Func<PanelView> activePanel;
    private readonly Func<PanelView> oppositePanel;
    private readonly Action<PanelView> activate;
    private readonly UiActions ui;
    private readonly GitHistoryReader reader = new();
    private readonly GitHistoryDocuments documents = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Action<ViewerDocument> viewFile;
    private readonly GitActionRunner actions;
    private GitFileSystem files = null!;
    private readonly List<(MenuItem Item, bool Allowed)> menuItems = [];
    private readonly HashSet<MenuItem> sortItems = [];
    private readonly HashSet<MenuItem> changesItems = [];

    internal GitHistoryController(Window window, Func<PanelView> activePanel, Func<PanelView> oppositePanel,
        Action<PanelView> activate, UiActions ui, Func<bool> hasError, Action cancelPanelLoads, Action<ViewerDocument> viewFile)
    {
        this.activePanel = activePanel;
        this.oppositePanel = oppositePanel;
        this.activate = activate;
        this.ui = ui;
        this.viewFile = viewFile;
        actions = new GitActionRunner(ui, hasError, lifetime.Token);
        window.Closing += (_, _) => { lifetime.Cancel(); cancelPanelLoads(); };
        window.Closed += (_, _) => { _ = Task.Run(documents.Dispose); };
    }

    internal IFileSystem Wrap(IFileSystem fileSystem) => files = new GitFileSystem(fileSystem, reader);

    public void RegisterMenuItem(MenuItem item, bool historyAllowed) { menuItems.Add((item, historyAllowed)); UpdateCommands(); }
    public void MarkSort(MenuItem item) { sortItems.Add(item); UpdateCommands(); }
    public void MarkChanges(MenuItem item) { changesItems.Add(item); UpdateCommands(); }

    public void UpdateCommands()
    {
        var panel = activePanel();
        foreach (var (item, allowed) in menuItems)
        {
            var enabled = !panel.HasHistory || allowed;
            if (sortItems.Contains(item) && GitPath.IsLog(panel.Session.DirectoryPath)) enabled = false;
            if (changesItems.Contains(item)) enabled = panel.HasHistory && GitPath.Parse(panel.Session.DirectoryPath).Kind == "tree";
            item.IsEnabled = enabled;
            ToolTip.SetTip(item, enabled ? null : sortItems.Contains(item) ? "Git history is ordered newest first. File sorting is unchanged."
                : changesItems.Contains(item) ? "Open a commit snapshot first." : "Unavailable in Git history. Choose a regular folder.");
        }
    }

    public void Open(bool selectedItem)
    {
        var source = activePanel();
        if (source.HasHistory) { ui.SetError("Close this log before opening another history."); return; }
        var path = selectedItem && source.Current is { Entry.IsParent: false } row ? row.Entry.FullPath : source.Session.DirectoryPath;
        if (ArchivePath.IsEntry(path)) { ui.SetError("Git history is available for repository files and folders."); return; }
        var destination = oppositePanel();
        actions.Start("Opening Git log", async token =>
        {
            var log = await files.OpenLogAsync(path, token);
            token.ThrowIfCancellationRequested();
            if (!await destination.OpenHistoryAsync(log, token)) { if (!token.IsCancellationRequested) ui.SetError(destination.Session.Error); return; }
            token.ThrowIfCancellationRequested();
            activate(destination); destination.FocusCurrent();
        });
    }

    public Task OpenCurrentAsync(PanelView panel)
    {
        var entry = panel.Current?.Entry;
        return entry == null ? Task.CompletedTask : actions.RunAsync("Opening Git location", token => NavigateAsync(panel, entry, token), opensViewer: !entry.IsDirectory);
    }

    private async Task NavigateAsync(PanelView panel, FileEntry entry, CancellationToken token)
    {
        if (entry.IsParent) { await panel.BackHistoryAsync(token); return; }
        var location = GitPath.Parse(entry.FullPath);
        if (location.Kind == "more")
        {
            await files.LoadMoreAsync(entry.FullPath, token);
            await panel.LoadAsync(panel.Session.DirectoryPath, preserveSelection: true, cancellationToken: token);
        }
        else if (entry.IsDirectory)
        {
            if (!await panel.LoadAsync(entry.FullPath, selectName: "..", cancellationToken: token)) ui.SetError(panel.Session.Error);
        }
        else { await ViewAsync(panel, panel.Session.DirectoryPath, entry, token); return; }
        if (!token.IsCancellationRequested) panel.FocusCurrent();
    }

    public void ShowChanges()
    {
        var panel = activePanel(); var path = panel.Session.DirectoryPath;
        if (!GitPath.HasRevision(path)) { ui.SetError("Open a commit snapshot first."); return; }
        actions.Start("Opening changed files", token => OpenChangesAsync(panel, path, token));
    }

    private async Task OpenChangesAsync(PanelView panel, string path, CancellationToken token)
    {
        var location = GitPath.Parse(path);
        var target = GitPath.Changes(location.Repository, location.Context, location.Revision, location.RelativePath);
        if (!await panel.LoadAsync(target, selectName: "..", cancellationToken: token)) ui.SetError(panel.Session.Error);
        if (!token.IsCancellationRequested) panel.FocusCurrent();
    }

    public bool HandleKey(KeyEventArgs e)
    {
        if (CancelPending(e)) return true;
        if (BlocksViewerNavigation(e.Key)) return true;
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Alt))
        {
            if (e.Key == Key.L) { Open(selectedItem: true); return true; }
            if (e.Key == Key.D) { DiffCurrent(); return true; }
        }
        var panel = activePanel();
        if (!panel.HasHistory) return false;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.F3 or Key.F4 or Key.F5 or Key.F6)
        {
            if (!GitPath.IsLog(panel.Session.DirectoryPath)) return false;
            ui.SetStatus("Git history is ordered newest first. File sorting is unchanged."); return true;
        }
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None) ui.Execute(panel.ExitHistoryAsync);
        else if (e.Key == Key.F3 && e.KeyModifiers == KeyModifiers.None) ViewCurrent();
        else if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control) ui.SetError("Copying files from Git history is not available. F3 views the selected file.");
        else if (e.Key is Key.F4 or Key.F5 or Key.F6 or Key.F7 or Key.F8 or Key.Delete || e.KeyModifiers == KeyModifiers.Control && e.Key is Key.X or Key.V)
            ui.SetError(ArchivePath.GitReadOnly);
        else return false;
        return true;
    }

    public bool CancelPending(KeyEventArgs e) => e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && actions.Cancel();

    private bool BlocksViewerNavigation(Key key) => actions.IsOpeningViewer &&
        key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End or Key.Enter or Key.Back;
}

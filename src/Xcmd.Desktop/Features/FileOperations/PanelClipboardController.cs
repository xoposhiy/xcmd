// @implements ClipboardTransfer, ReusePaths
using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Xcmd.Core;
using Avalonia.Controls;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Desktop.Features.FileOperations;

public sealed class PanelClipboardController
{
    public static DataFormat<byte[]> PreferredDropEffect { get; } = DataFormat.CreateBytesPlatformFormat("Preferred DropEffect");
    private readonly Window owner;
    private readonly FileOperationQueue operations;
    private readonly IReadOnlyList<PanelView> panels;
    private readonly UiActions ui;
    private readonly IFileOperationStore store;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<int, ClipboardFiles> moves = [];
    private bool busy;
    private bool checking;
    private bool hasCutFiles;
    private bool closed;
    private int completingMoves;
    public bool IsBusy => busy || completingMoves > 0;
    private IClipboard Clipboard => owner.Clipboard ?? throw new IOException("The clipboard is unavailable.");

    internal PanelClipboardController(Window owner, IFileOperationStore store, FileOperationQueue operations,
        IReadOnlyList<PanelView> panels, UiActions ui)
    {
        this.owner = owner;
        this.store = store;
        this.operations = operations;
        this.panels = panels;
        this.ui = ui;
        owner.Opened += (_, _) => timer.Start();
        owner.Activated += OnActivated;
        owner.Closed += (_, _) => { closed = true; timer.Stop(); };
        timer.Tick += OnTick;
        operations.Finished += OnFinished;
    }

    internal void Copy(FileEntry[] entries, bool cut)
    {
        if (entries.Length == 0) return;
        ui.Execute(() => RunAsync(async () =>
        {
            if (entries.Any(entry => GitPath.IsEntry(entry.FullPath)))
                throw new IOException("Copying files from Git history is not available. F3 views the selected file.");
            if (entries.Any(entry => ArchivePath.IsEntry(entry.FullPath)))
                throw new IOException("Use F5 to copy files out of an archive before using the clipboard.");
            var files = new ClipboardFiles(entries.Select(entry => entry.FullPath).ToArray(), cut);
            await WriteAsync(files);
            SetCutFiles(files);
            ui.SetStatus($"{entries.Length:N0} item(s) {(cut ? "cut · Ctrl+V to move" : "copied")}");
        }));
    }

    internal void CopyPath(FileRow? row)
    {
        if (row == null) return;
        if (GitPath.IsEntry(row.Entry.FullPath)) { ui.SetError("Copying paths from Git history is not available. F3 views the selected file."); return; }
        var path = ArchivePath.Display(row.Entry.FullPath);
        ui.Execute(() => RunAsync(async () =>
        {
            await Clipboard.SetTextAsync(path);
            await Clipboard.FlushAsync();
            SetCutFiles(null);
            ui.SetStatus("Full path copied");
        }));
    }

    internal void Paste(PanelView panel)
    {
        var destination = panel.Session.DirectoryPath;
        if (panel.IsSearchResults) { ui.SetError("Open a destination folder first (Escape leaves search results)."); return; }
        if (ArchivePath.IsEntry(destination)) { ui.SetError(ArchivePath.ReadOnlyMessage(destination, "Archives are read-only. Choose a regular destination folder.")); return; }
        ui.Execute(() => RunAsync(async () =>
        {
            var files = await ReadAsync();
            if (files == null) { ui.SetError("There are no files on the clipboard."); return; }
            if (files.Cut && moves.Count > 0) { ui.SetError("The clipboard move is already in progress."); return; }
            var sources = new List<FileEntry>();
            foreach (var path in files.Paths)
                sources.Add(await store.InspectAsync(path, CancellationToken.None) ?? throw new IOException("The clipboard item is no longer available: " + path));
            var requests = await ClipboardTransferPlanner.PlanAsync(store, sources.ToArray(), destination, files.Cut, CancellationToken.None);
            foreach (var request in requests)
            {
                var job = operations.Enqueue(request);
                if (files.Cut)
                {
                    if (job.IsFinished) await CompleteMoveAsync(files);
                    else moves.Add(job.Id, files);
                }
            }
            if (requests.Count == 0) ui.SetStatus("The items are already in this folder.");
        }));
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        try { await action(); }
        catch (Exception error) when (error is COMException or InvalidOperationException or NotSupportedException)
        { ui.SetError("Clipboard: " + error.Message); }
        finally { busy = false; }
    }

    private async Task<ClipboardFiles?> ReadAsync()
    {
        using var data = await Clipboard.TryGetDataAsync();
        if (data == null) return null;
        var items = await data.TryGetFilesAsync();
        var paths = items?.Select(item => item.TryGetLocalPath() ?? throw new IOException("Only local clipboard files are supported."))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        var effect = await data.TryGetValueAsync(PreferredDropEffect);
        return paths.Length == 0 ? null : new(paths, effect is { Length: >= 4 } && BitConverter.ToUInt32(effect) == 2);
    }

    private async Task WriteAsync(ClipboardFiles files)
    {
        var data = new DataTransfer();
        try
        {
            foreach (var path in files.Paths)
            {
                var uri = new Uri(path);
                var item = (IStorageItem?)await owner.StorageProvider.TryGetFileFromPathAsync(uri)
                    ?? await owner.StorageProvider.TryGetFolderFromPathAsync(uri)
                    ?? throw new IOException("The clipboard item is no longer available: " + path);
                data.Add(DataTransferItem.CreateFile(item));
            }
            data.Add(DataTransferItem.Create(PreferredDropEffect, BitConverter.GetBytes(files.Cut ? 2u : 1u)));
        }
        catch { ((IDisposable)data).Dispose(); throw; }
        await Clipboard.SetDataAsync(data);
        await Clipboard.FlushAsync();
    }

    private async Task RefreshAsync()
    {
        if (checking || busy || closed) return;
        checking = true;
        try { SetCutFiles(await ReadAsync()); }
        catch (Exception error) when (error is IOException or COMException or InvalidOperationException or NotSupportedException)
        { ui.Trace("Clipboard refresh: " + error.Message); }
        finally { checking = false; }
    }

    private async void OnActivated(object? sender, EventArgs e) => await RefreshAsync();
    private async void OnFinished(FileOperationJob job)
    {
        if (!moves.Remove(job.Id, out var files)) return;
        completingMoves++;
        try { await CompleteMoveAsync(files); }
        finally { completingMoves--; }
    }
    private async void OnTick(object? sender, EventArgs e)
    {
        if (hasCutFiles) await RefreshAsync();
    }

    private void SetCutFiles(ClipboardFiles? files)
    {
        hasCutFiles = files?.Cut == true;
        var paths = hasCutFiles ? files!.Paths : [];
        foreach (var panel in panels) panel.SetCutPaths(paths);
    }

    private async Task CompleteMoveAsync(ClipboardFiles files)
    {
        try
        {
            var remaining = new List<string>();
            foreach (var path in files.Paths)
                if (await store.InspectAsync(path, CancellationToken.None) != null) remaining.Add(path);
            var current = await ReadAsync();
            if (current?.Cut != true || !current.Paths.SequenceEqual(files.Paths, StringComparer.OrdinalIgnoreCase)) return;
            if (remaining.Count == 0) await Clipboard.ClearAsync();
            else if (remaining.Count != files.Paths.Length) await WriteAsync(new(remaining.ToArray(), true));
            SetCutFiles(remaining.Count == 0 ? null : new(remaining.ToArray(), true));
        }
        catch (Exception error) when (error is IOException or COMException or InvalidOperationException or NotSupportedException)
        { ui.SetError("Files were processed, but the clipboard could not be updated: " + error.Message); }
    }

    private sealed record ClipboardFiles(string[] Paths, bool Cut);
}

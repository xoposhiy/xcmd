using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed class PanelClipboardController
{
    public static DataFormat<byte[]> PreferredDropEffect { get; } = DataFormat.CreateBytesPlatformFormat("Preferred DropEffect");
    private readonly CommanderWindow owner;
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

    public PanelClipboardController(CommanderWindow owner, IFileOperationStore store)
    {
        this.owner = owner; this.store = store;
        owner.Opened += (_, _) => timer.Start();
        owner.Activated += OnActivated;
        owner.Closed += (_, _) => { closed = true; timer.Stop(); };
        timer.Tick += OnTick;
        owner.Operations.Finished += OnFinished;
    }

    public void Copy(bool cut)
    {
        var entries = owner.SelectedOperationEntries();
        if (entries.Length == 0) return;
        owner.Execute(() => RunAsync(async () =>
        {
            if (entries.Any(entry => ArchivePath.IsEntry(entry.FullPath)))
                throw new IOException("Use F5 to copy files out of an archive before using the clipboard.");
            var files = new ClipboardFiles(entries.Select(entry => entry.FullPath).ToArray(), cut);
            await WriteAsync(files);
            SetCutFiles(files);
            owner.SetStatusMessage($"{entries.Length:N0} item(s) {(cut ? "cut · Ctrl+V to move" : "copied")}");
        }));
    }

    public void CopyPath()
    {
        if (owner.ActivePanel.Current is not { } row) return;
        var path = row.Entry.FullPath;
        owner.Execute(() => RunAsync(async () =>
        {
            await Clipboard.SetTextAsync(path);
            await Clipboard.FlushAsync();
            SetCutFiles(null);
            owner.SetStatusMessage("Full path copied");
        }));
    }

    public void Paste()
    {
        var panel = owner.ActivePanel;
        var destination = panel.Session.DirectoryPath;
        if (panel.IsSearchResults) { owner.SetError("Open a destination folder first (Escape leaves search results)."); return; }
        if (ArchivePath.IsEntry(destination)) { owner.SetError("Archives are read-only. Choose a regular destination folder."); return; }
        owner.Execute(() => RunAsync(async () =>
        {
            var files = await ReadAsync();
            if (files == null) { owner.SetError("There are no files on the clipboard."); return; }
            if (files.Cut && moves.Count > 0) { owner.SetError("The clipboard move is already in progress."); return; }
            var sources = new List<FileEntry>();
            foreach (var path in files.Paths)
                sources.Add(await store.InspectAsync(path, CancellationToken.None) ?? throw new IOException("The clipboard item is no longer available: " + path));
            var requests = await ClipboardTransferPlanner.PlanAsync(store, sources.ToArray(), destination, files.Cut, CancellationToken.None);
            foreach (var request in requests)
            {
                var job = owner.Operations.Enqueue(request);
                if (files.Cut)
                {
                    if (job.IsFinished) await CompleteMoveAsync(files);
                    else moves.Add(job.Id, files);
                }
            }
            if (requests.Count == 0) owner.SetStatusMessage("The items are already in this folder.");
        }));
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        try { await action(); }
        catch (Exception error) when (error is COMException or InvalidOperationException or NotSupportedException)
        { owner.SetError("Clipboard: " + error.Message); }
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
        { owner.Trace.Add("Clipboard refresh: " + error.Message); }
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
        owner.Left.SetCutPaths(paths); owner.Right.SetCutPaths(paths);
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
        { owner.SetError("Files were processed, but the clipboard could not be updated: " + error.Message); }
    }

    private sealed record ClipboardFiles(string[] Paths, bool Cut);
}

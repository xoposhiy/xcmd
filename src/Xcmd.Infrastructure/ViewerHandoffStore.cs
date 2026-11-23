// @implements ReleaseHandoff, WorkspaceStorage
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class ViewerHandoffStore(string directory)
{
    private string SnapshotPath => Path.Combine(directory, "viewers.json");
    private string DocumentsPath => Path.Combine(directory, "viewer-documents");
    private string InspectorsPath => Path.Combine(directory, "inspectors.json");

    public async Task SaveInspectorsAsync(InspectorHandoff[] inspectors, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        await using var stream = File.Create(InspectorsPath);
        await JsonSerializer.SerializeAsync(stream, inspectors, SessionJsonContext.Default.InspectorHandoffArray, token);
    }

    public async Task<InspectorHandoff[]> ReadInspectorsAsync()
    {
        if (!File.Exists(InspectorsPath)) return [];
        await using var stream = File.OpenRead(InspectorsPath);
        return await JsonSerializer.DeserializeAsync(stream, SessionJsonContext.Default.InspectorHandoffArray) ?? [];
    }

    public static ViewerHandoffStore? ForRestart(string directory, string sessionPath)
    {
        var root = Path.Combine(Path.GetDirectoryName(sessionPath)!, "handoff");
        return Path.GetDirectoryName(directory)?.Equals(root, StringComparison.OrdinalIgnoreCase) == true &&
            Guid.TryParseExact(Path.GetFileName(directory), "N", out _) ? new(directory) : null;
    }

    public async Task SaveAsync(ViewerHandoff[] viewers, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        for (var index = 0; index < viewers.Length; index++)
        {
            var viewer = viewers[index];
            if (viewer.Document is not { IsTemporary: true } document) continue;
            Directory.CreateDirectory(DocumentsPath);
            var path = Path.Combine(DocumentsPath, index + Path.GetExtension(document.Path));
            try { await CopyAsync(document.Path, path, token); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { }
            var paths = (string[])viewer.Paths.Clone();
            paths[viewer.Index] = path;
            viewers[index] = viewer with { Paths = paths, Document = document with { Path = path } };
        }
        await using var stream = File.Create(SnapshotPath);
        await JsonSerializer.SerializeAsync(stream, viewers, SessionJsonContext.Default.ViewerHandoffArray, token);
    }

    public async Task<ViewerHandoff[]> ReadAsync()
    {
        if (!File.Exists(SnapshotPath)) return [];
        await using var stream = File.OpenRead(SnapshotPath);
        return await JsonSerializer.DeserializeAsync(stream, SessionJsonContext.Default.ViewerHandoffArray)
            ?? throw new InvalidDataException("The viewer handoff is empty.");
    }

    public void Delete()
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            if (Directory.Exists(DocumentsPath))
            {
                foreach (var path in Directory.EnumerateFiles(DocumentsPath)) File.Delete(path);
                Directory.Delete(DocumentsPath);
            }
            File.Delete(SnapshotPath);
            File.Delete(InspectorsPath);
            File.Delete(Path.Combine(directory, "ready"));
            Directory.Delete(directory);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private static async Task CopyAsync(string source, string destination, CancellationToken token)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, useAsync: true);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await input.CopyToAsync(output, token);
    }
}

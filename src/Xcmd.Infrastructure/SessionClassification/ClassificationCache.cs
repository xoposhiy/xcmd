// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class ClassificationCache(string path)
{
    private sealed record Snapshot(string Version, Dictionary<string, ToolClassification> Rows);
    private Dictionary<string, ToolClassification> rows = [];

    internal async Task<FileStream> LockAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException e) when ((e.HResult & 0xffff) is 32 or 33) { await Task.Delay(100, token).ConfigureAwait(false); }
        }
    }

    internal Dictionary<int, ToolClassification> Load(ClassificationCall[] calls)
    {
        try
        {
            using var file = File.OpenRead(path);
            var saved = JsonSerializer.Deserialize<Snapshot>(file);
            if (saved?.Version == ClassificationPacket.Version && saved.Rows != null) rows = saved.Rows;
        }
        catch (Exception e) when (e is FileNotFoundException or JsonException) { }
        var valid = new Dictionary<int, ToolClassification>();
        foreach (var call in calls)
            if (rows.TryGetValue(call.Fingerprint, out var entry) && Valid(entry))
                valid.Add(call.Call.Id, entry);
        return valid;
    }

    private static bool Valid(ToolClassification? row) => row is { Pairs.Length: > 0, Resources: not null } && !string.IsNullOrWhiteSpace(row.Summary)
        && row.Resources.All(r => r is { IsValid: true })
        && row.Pairs.All(p => p is { Label.IsValid: true, Objects.Length: > 0 } && p.Objects.All(o => !string.IsNullOrWhiteSpace(o)));

    internal void Store(ClassificationCall[] calls, Dictionary<int, ToolClassification> answers)
    {
        foreach (var call in calls)
            if (answers.TryGetValue(call.Call.Id, out var row)) rows[call.Fingerprint] = row;
        Save();
    }

    private void Save()
    {
        var temp = path + ".tmp";
        try
        {
            using (var stream = File.Create(temp)) JsonSerializer.Serialize(stream, new Snapshot(ClassificationPacket.Version, rows));
            File.Move(temp, path, true);
        }
        finally { File.Delete(temp); }
    }
}

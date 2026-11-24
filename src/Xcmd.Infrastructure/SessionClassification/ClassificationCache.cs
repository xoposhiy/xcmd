// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed record CachedClassification(ToolClassification Classification, string PromptVersion)
{
    internal bool IsCurrent => PromptVersion == ClassificationPacket.PromptVersion;
}

internal sealed class ClassificationCache(string path)
{
    internal const int FormatVersion = 1;
    private const int LegacyCompatibleFormat = 1;
    private sealed record Snapshot(int FormatVersion, Dictionary<string, CachedClassification> Rows);
    private sealed record LegacySnapshot(string Version, Dictionary<string, ToolClassification> Rows);
    private Dictionary<string, CachedClassification> rows = [];

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

    internal Dictionary<int, CachedClassification> Load(ClassificationCall[] calls)
    {
        rows = [];
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var document = JsonDocument.Parse(file);
            var data = document.RootElement;
            if (data.ValueKind == JsonValueKind.Object) rows = ReadRows(data);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or JsonException) { }
        var valid = new Dictionary<int, CachedClassification>();
        foreach (var call in calls)
            if (rows.TryGetValue(call.Fingerprint, out var entry) && entry != null && Valid(entry.Classification))
                valid.Add(call.Call.Id, entry);
        return valid;
    }

    private static Dictionary<string, CachedClassification> ReadRows(JsonElement data)
    {
        if (data.TryGetProperty(nameof(Snapshot.FormatVersion), out var version))
            return version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var format) && format == FormatVersion
                ? data.Deserialize<Snapshot>()?.Rows ?? [] : [];
        if (FormatVersion == LegacyCompatibleFormat &&
            data.Deserialize<LegacySnapshot>() is { Version.Length: 64, Rows: not null } legacy && legacy.Version.All(Uri.IsHexDigit))
            return legacy.Rows.ToDictionary(row => row.Key, row => new CachedClassification(row.Value, legacy.Version));
        return [];
    }

    private static bool Valid(ToolClassification? row) => row is { Pairs.Length: > 0, Resources: not null } && !string.IsNullOrWhiteSpace(row.Summary)
        && row.Resources.All(r => r is { IsValid: true })
        && row.Pairs.All(p => p is { Label.IsValid: true, Objects.Length: > 0 } && p.Objects.All(o => !string.IsNullOrWhiteSpace(o)));

    internal void Store(ClassificationCall[] calls, Dictionary<int, CachedClassification> answers)
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
            using (var stream = File.Create(temp)) JsonSerializer.Serialize(stream, new Snapshot(FormatVersion, rows));
            File.Move(temp, path, true);
        }
        finally { File.Delete(temp); }
    }
}

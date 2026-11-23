// @implements ViewRecentAgentSessions
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class ClassificationStatus(string folder, AgentSession session)
{
    private sealed record Stamp(string Path, bool Directory, long Length, long Modified)
    {
        internal static Stamp Read(string path, bool directory)
        {
            FileSystemInfo item = directory ? new DirectoryInfo(path) : new FileInfo(path);
            return new(path, directory, item is FileInfo file && file.Exists ? file.Length : -1,
                item.Exists ? item.LastWriteTimeUtc.Ticks : 0);
        }

        internal bool IsCurrent() => this == Read(Path, Directory);
    }

    private sealed record Snapshot(string Version, string Project, string Directory, string? Transcript, bool Ready, Stamp[] Inputs);
    private readonly List<Stamp> inputs = [];
    private readonly string path = Path.Combine(folder, "classification.status");

    internal bool? Read()
    {
        if (!Directory.Exists(folder)) return false;
        if (!File.Exists(path)) return null;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var saved = JsonSerializer.Deserialize<Snapshot>(file);
            if (saved != null && saved.Version == ClassificationPacket.Version && saved.Project == session.ProjectDirectory &&
                saved.Directory == session.Directory && saved.Transcript == session.TranscriptPath &&
                saved.Inputs is { Length: > 0 } && saved.Inputs.All(s => s is { Path.Length: > 0 } && s.IsCurrent()))
                return saved.Ready;
        }
        catch (JsonException) { }
        return null;
    }

    internal void Watch(string input, bool directory = false) => inputs.Add(Stamp.Read(input, directory));

    internal bool Save(bool ready)
    {
        if (!inputs.All(s => s.IsCurrent())) return false;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(new Snapshot(ClassificationPacket.Version, session.ProjectDirectory,
                session.Directory, session.TranscriptPath, ready, [.. inputs])));
            File.Move(temp, path, true);
        }
        finally { File.Delete(temp); }
        return ready;
    }
}

// @implements WorkspaceStorage
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class SessionStore(string path)
{
    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public async Task<WorkspaceState?> ReadAsync()
    {
        if (!File.Exists(Path)) return null;
        await using var stream = File.OpenRead(Path);
        var state = await JsonSerializer.DeserializeAsync(stream, SessionJsonContext.Default.WorkspaceState)
            ?? throw new InvalidDataException("The saved session is empty.");
        state.Validate();
        return state;
    }

    public async Task SaveAsync(WorkspaceState state)
    {
        state.Validate();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state, SessionJsonContext.Default.WorkspaceState));
            File.Move(temporary, Path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

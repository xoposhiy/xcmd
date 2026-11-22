// @implements RecentLocations, NavigatePanels
namespace Xcmd.Core;

public sealed class DirectoryHistory
{
    private const int Capacity = 100;
    private readonly List<string> visited = [];
    private readonly Dictionary<string, string> driveDirectories = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> PreviousDirectories => visited.Skip(1).ToArray();
    public string[] CaptureDriveDirectories() => driveDirectories.Values.ToArray();
    public string DirectoryForDrive(string root) => driveDirectories.GetValueOrDefault(root, root);

    public void Restore(IEnumerable<string> previous, string current, string[]? savedDriveDirectories = null)
    {
        visited.Clear();
        driveDirectories.Clear();
        foreach (var path in savedDriveDirectories ?? []) RememberDrive(path);
        foreach (var path in previous.Reverse()) Visit(path);
        Visit(current);
    }

    public void Visit(string path)
    {
        RememberDrive(path);
        visited.RemoveAll(previous => previous.Equals(path, StringComparison.OrdinalIgnoreCase));
        visited.Insert(0, path);
        if (visited.Count > Capacity) visited.RemoveAt(Capacity);
    }

    private void RememberDrive(string path)
    {
        if (!ArchivePath.IsEntry(path) && Path.GetPathRoot(path) is { Length: > 0 } root)
            driveDirectories[root] = path;
    }
}

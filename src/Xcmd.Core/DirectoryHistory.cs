// @implements RecentLocations
namespace Xcmd.Core;

public sealed class DirectoryHistory
{
    private const int Capacity = 100;
    private readonly List<string> visited = [];

    public IReadOnlyList<string> PreviousDirectories => visited.Skip(1).ToArray();

    public void Restore(IEnumerable<string> previous, string current)
    {
        visited.Clear();
        foreach (var path in previous.Reverse()) Visit(path);
        Visit(current);
    }

    public void Visit(string path)
    {
        visited.RemoveAll(previous => previous.Equals(path, StringComparison.OrdinalIgnoreCase));
        visited.Insert(0, path);
        if (visited.Count > Capacity) visited.RemoveAt(Capacity);
    }
}

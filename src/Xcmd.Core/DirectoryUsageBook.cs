namespace Xcmd.Core;

public sealed record DirectoryUsage(string Path, long Visits, double Weight, DateTimeOffset LastVisited);
public sealed record DirectorySuggestion(string Path, long Visits, double Score);

public sealed class DirectoryUsageBook
{
    private const int Capacity = 512;
    private readonly Dictionary<string, DirectoryUsage> locations = new(StringComparer.OrdinalIgnoreCase);

    public DirectoryUsage[] Capture() => locations.Values.ToArray();

    public void Restore(IEnumerable<DirectoryUsage> saved)
    {
        locations.Clear();
        foreach (var location in saved.Where(location => !IsRoot(location.Path)).Take(Capacity)) locations[location.Path] = location;
    }

    public void Seed(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (IsRoot(path)) continue;
            if (locations.Count == Capacity) break;
            locations.TryAdd(path, new(path, 0, 0, DateTimeOffset.MinValue));
        }
    }

    public void Visit(string path, DateTimeOffset now)
    {
        if (IsRoot(path)) return;
        var previous = locations.GetValueOrDefault(path);
        var weight = previous == null ? 0 : Score(previous, now);
        var visits = previous == null ? 1 : Math.Min(previous.Visits, long.MaxValue - 1) + 1;
        locations[path] = new(path, visits, weight + 1, now);
        if (locations.Count <= Capacity) return;
        var lowest = locations.Values.OrderBy(location => Score(location, now)).ThenBy(location => location.LastVisited).First();
        locations.Remove(lowest.Path);
    }

    public DirectorySuggestion[] Suggest(string current, DateTimeOffset now, int count = 30) => locations.Values
        .Where(location => !IsRoot(location.Path) && !location.Path.Equals(current, StringComparison.OrdinalIgnoreCase))
        .Select(location => new DirectorySuggestion(location.Path, location.Visits, Score(location, now)))
        .OrderByDescending(location => location.Score)
        .ThenBy(location => location.Path, StringComparer.OrdinalIgnoreCase)
        .Take(count).ToArray();

    private static double Score(DirectoryUsage location, DateTimeOffset now) =>
        location.Weight * Math.Pow(0.5, Math.Max(0, (now - location.LastVisited).TotalDays) / 3);

    private static bool IsRoot(string path) => Path.TrimEndingDirectorySeparator(path).Equals(
        Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path) ?? ""), StringComparison.OrdinalIgnoreCase);
}

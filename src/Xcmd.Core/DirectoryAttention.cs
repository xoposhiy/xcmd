// @implements RecentLocations
namespace Xcmd.Core;

public sealed class DirectoryAttention
{
    private string? current;
    private DateTimeOffset previous;
    private TimeSpan activeTime;
    private bool wasActive;
    private bool counted;

    public string? Observe(string path, DateTimeOffset now, bool active)
    {
        if (!string.Equals(path, current, StringComparison.OrdinalIgnoreCase))
        { current = path; activeTime = TimeSpan.Zero; counted = false; previous = now; wasActive = active; return null; }
        var elapsed = now - previous; previous = now;
        if (active && wasActive && elapsed > TimeSpan.Zero && elapsed <= TimeSpan.FromSeconds(2)) activeTime += elapsed;
        wasActive = active;
        if (counted || activeTime < TimeSpan.FromSeconds(10)) return null;
        counted = true;
        return path;
    }
}

// @implements JumpToName
namespace Xcmd.Core;

public sealed class NameJump
{
    public bool Active { get; private set; }
    public string Prefix { get; private set; } = "";
    private string[] queries = [];
    public void Begin() { Active = true; Prefix = ""; queries = []; }
    public void Reset() { Active = false; Prefix = ""; queries = []; }

    public (int Start, int Length)? Match(string name)
    {
        (int Start, int Length)? best = null;
        if (!Active) return null;
        foreach (var query in queries)
        {
            var start = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (start >= 0 && (best == null || start < best.Value.Start)) best = (start, query.Length);
        }
        return best;
    }

    public string? Move(string? current, bool down, IEnumerable<string> names)
    {
        var matches = names.Where(name => Match(name) != null).ToArray();
        if (matches.Length == 0) return null;
        var index = Array.IndexOf(matches, current);
        return matches[(index + (down ? 1 : matches.Length - 1) + matches.Length) % matches.Length];
    }

    public string? Append(string text, IEnumerable<string> candidates, IEnumerable<string> names)
    {
        if (!Active) return null;
        var queries = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string? match = null;
        var bestPosition = int.MaxValue;
        foreach (var name in names)
            foreach (var query in queries)
            {
                var position = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (position < 0 || position >= bestPosition) continue;
                match = name; bestPosition = position;
            }
        if (match != null) { Prefix += text; this.queries = queries; }
        return match;
    }
}

namespace Xcmd.Core;

public sealed class NameJump
{
    public bool Active { get; private set; }
    public string Prefix { get; private set; } = "";
    public void Begin() { Active = true; Prefix = ""; }
    public void Reset() { Active = false; Prefix = ""; }

    public string? Append(string text, IEnumerable<string> candidates, IEnumerable<string> names)
    {
        if (!Active) return null;
        var available = names.ToArray();
        var match = candidates.Select(prefix => available.FirstOrDefault(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(name => name != null);
        if (match != null) Prefix += text;
        return match;
    }
}

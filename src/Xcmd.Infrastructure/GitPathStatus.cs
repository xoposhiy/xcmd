// @implements RepositoryStatus
namespace Xcmd.Infrastructure;

[Flags]
public enum GitChange { None = 0, Modified = 1, Added = 2, Deleted = 4, Renamed = 8, Copied = 16, TypeChanged = 32, Untracked = 64, Conflict = 128 }

public sealed record GitPathStatus(string Path, GitChange Staged, GitChange Unstaged, string? OriginalPath = null);

internal static class GitStatusPaths
{
    public static (GitPathStatus[] Changes, string[] Ignored) Parse(string[] records)
    {
        var changes = new List<GitPathStatus>();
        var ignored = new List<string>();
        for (var i = 0; i < records.Length; i++)
        {
            var line = records[i];
            if (line.StartsWith("? ", StringComparison.Ordinal)) changes.Add(new(line[2..], GitChange.None, GitChange.Untracked));
            else if (line.StartsWith("! ", StringComparison.Ordinal)) ignored.Add(line[2..]);
            else if (line.Length > 4 && line[1] == ' ' && line[0] is '1' or '2' or 'u')
            {
                var source = line[0] == '2' && i + 1 < records.Length ? records[++i] : null;
                if (Tracked(line, source) is { } entry) changes.Add(entry);
            }
        }
        return (changes.ToArray(), ignored.ToArray());
    }

    private static GitPathStatus? Tracked(string line, string? source)
    {
        var count = line[0] switch { '1' => 9, '2' => 10, _ => 11 };
        var parts = line.Split(' ', count);
        if (parts.Length != count) return null;
        if (line[0] == 'u') return new(parts[^1], GitChange.Conflict, GitChange.None);
        return new(parts[^1], Change(line[2]), Change(line[3]), source);
    }

    private static GitChange Change(char code) => code switch
    {
        'M' => GitChange.Modified,
        'A' => GitChange.Added,
        'D' => GitChange.Deleted,
        'R' => GitChange.Renamed,
        'C' => GitChange.Copied,
        'T' => GitChange.TypeChanged,
        'U' => GitChange.Conflict,
        _ => GitChange.None
    };
}

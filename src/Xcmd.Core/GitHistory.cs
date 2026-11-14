namespace Xcmd.Core;

public sealed record GitHistoryScope(string Root, string Path, string Revision)
{
    public string Label => Path.Length == 0 ? Root : System.IO.Path.Combine(Root, Path);
}

public sealed record GitHistoryCommit(string Id, string Author, DateTimeOffset Date, string Subject)
{
    public string ShortId => Id[..Math.Min(10, Id.Length)];
}

public sealed record GitCommitChange(string Path, string Status, string? PreviousPath = null)
{
    public string Description => Status[0] switch
    {
        'A' => "Added",
        'D' => "Deleted",
        'R' => "Renamed",
        'C' => "Copied",
        'T' => "Type changed",
        _ => "Modified"
    };
}

public sealed record GitTreeItem(string Path, string Name, bool IsDirectory, long Size, string ObjectType);

public sealed record GitWorkingChange(string Path, string StagedStatus, string WorkingStatus, string? PreviousPath = null);

public static class GitHistoryRules
{
    public static bool InScope(string scope, string path, string? previousPath = null) =>
        scope.Length == 0 || Matches(scope, path) || previousPath != null && Matches(scope, previousPath);

    private static bool Matches(string scope, string path) =>
        path.Equals(scope, StringComparison.Ordinal) || path.StartsWith(scope + "/", StringComparison.Ordinal);

    public static string StatusFor(string path, bool directory, IReadOnlyList<GitCommitChange> changes)
    {
        var change = changes.FirstOrDefault(change => change.Path.Equals(path, StringComparison.Ordinal));
        if (change != null) return change.Description;
        return directory && changes.Any(change => change.Path.StartsWith(path + "/", StringComparison.Ordinal)) ? "Changed" : "";
    }

    public static string Parent(string path) => path.LastIndexOf('/') is var index && index >= 0 ? path[..index] : "";
}

// @implements RepositoryLocations
namespace Xcmd.Core;

public sealed record GitLocation(string Repository, string Context, string Kind, string Revision, string RelativePath);

public static class GitPath
{
    private const string Marker = "|git|";
    public static bool IsEntry(string path) => path.Contains(Marker, StringComparison.Ordinal);
    public static string Log(string root, string context) => root + Marker + context + "\\log";
    public static string Tree(string root, string context, string revision, string relative = "") =>
        root + Marker + context + "\\tree\\" + revision + (relative.Length == 0 ? "" : "\\" + relative.Replace('/', '\\'));
    public static string Working(string root, string context) => root + Marker + context + "\\working";
    public static string Changes(string root, string context, string revision, string scope = "") =>
        root + Marker + context + "\\changes\\" + revision + (scope.Length == 0 ? "" : "\\" + scope.Replace('/', '\\'));
    public static string ChangedFile(string root, string context, string revision, string path) =>
        root + Marker + context + "\\change\\" + revision + "\\" + path.Replace('/', '\\');
    public static string More(string root, string context) => root + Marker + context + "\\more";

    public static GitLocation Parse(string path)
    {
        var index = path.IndexOf(Marker, StringComparison.Ordinal);
        if (index < 0) throw new ArgumentException("This is not a Git location.", nameof(path));
        var parts = path[(index + Marker.Length)..].Replace('\\', '/').Split('/');
        if (parts.Length < 2) throw new ArgumentException("The Git location is incomplete.", nameof(path));
        var tree = parts[1] is "tree" or "changes" or "change";
        return new(path[..index], parts[0], parts[1], tree && parts.Length > 2 ? parts[2] : "",
            string.Join('/', parts.Skip(tree ? 3 : 2)));
    }

    public static bool IsLog(string path) => IsEntry(path) && Parse(path).Kind == "log";
    public static bool HasRevision(string path) => IsEntry(path) && Parse(path).Revision.Length > 0;
    public static bool IsLive(string path) => IsEntry(path) && Parse(path).Kind is "log" or "working";

    public static string Parent(string path)
    {
        var location = Parse(path);
        if (location.Kind == "log") return location.Repository;
        if (location.Kind == "changes") return Tree(location.Repository, location.Context, location.Revision, location.RelativePath);
        if (location.Kind == "change") return Changes(location.Repository, location.Context, location.Revision);
        if (location.Kind != "tree" || location.RelativePath.Length == 0) return Log(location.Repository, location.Context);
        return Tree(location.Repository, location.Context, location.Revision, GitHistoryRules.Parent(location.RelativePath));
    }

    public static string Display(string path)
    {
        var location = Parse(path);
        var label = location.Revision.Length > 0 ? location.Revision[..Math.Min(10, location.Revision.Length)] + (location.Kind == "changes" ? " · Changed files" : "")
            : location.Kind == "log" ? "Git log" : "Working tree";
        return location.Repository + " · " + label + (location.RelativePath.Length == 0 ? "" : " · /" + location.RelativePath);
    }
}

public sealed record GitEntryStatus(char Staged, char Working);

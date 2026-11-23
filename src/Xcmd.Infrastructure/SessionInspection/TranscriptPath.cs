// @implements InspectAgentSession
namespace Xcmd.Infrastructure;

internal static class TranscriptPath
{
    internal static bool IsUnixRoot(string value) => IsUnix(Decode(value));
    private static bool IsUnix(string path) => path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal);

    internal static string Resolve(string value, string cwd, string project)
    {
        var path = Decode(value);
        if (path.Length == 0) return "";
        var directory = Decode(cwd);
        var root = Decode(project);
        if (IsUnix(path) || IsUnix(directory) && !Path.IsPathFullyQualified(path))
            return IsUnix(directory) ? UnixPath(path, directory, root) : "";
        return WindowsPath(path, directory, root);
    }

    private static string UnixPath(string path, string directory, string root)
    {
        var parts = new List<string>();
        var absolute = path.StartsWith('/') ? path : directory + "/" + path;
        foreach (var part in absolute.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else if (part != ".") parts.Add(part);
        }
        var full = "/" + string.Join('/', parts);
        var prefix = root.TrimEnd('/') + "/";
        return IsUnix(root) && full.StartsWith(prefix, StringComparison.Ordinal) ? full[prefix.Length..] : full;
    }

    private static string WindowsPath(string path, string directory, string root)
    {
        if (path.AsSpan(Math.Min(2, path.Length)).Contains(':') || path.IndexOfAny(['<', '>', '|', '"', '*', '?']) >= 0) return "";
        try
        {
            if (!Path.IsPathFullyQualified(path) && (!Path.IsPathFullyQualified(directory) || Path.IsPathRooted(path))) return "";
            var full = Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : Path.GetFullPath(path, directory);
            if (!Path.IsPathFullyQualified(root)) return full.Replace('\\', '/');
            var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
            return relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) ? full.Replace('\\', '/') : relative;
        }
        catch (Exception error) when (error is ArgumentException or IOException or NotSupportedException) { return ""; }
    }

    private static string Decode(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl)) return "";
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            path = FromFileUri(path);
        }
        path = path.Replace('\\', '/');
        if (path.StartsWith("//?/UNC/", StringComparison.OrdinalIgnoreCase)) path = "//" + path[8..];
        else if (path.StartsWith("//?/") && path.Length >= 7 && char.IsAsciiLetter(path[4]) && path[5..7] == ":/") path = path[4..];
        if (path.StartsWith("//./") || path.StartsWith("//?/") || path.Contains(':') && !HasDrive(path)) return "";
        return path;
    }

    private static bool HasDrive(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1..3] == ":/" && path.LastIndexOf(':') == 1;

    private static string FromFileUri(string path) =>
        path.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(path, UriKind.Absolute, out var file) &&
        file.IsFile && file.Query.Length == 0 && file.Fragment.Length == 0 && file.UserInfo.Length == 0 ? file.LocalPath : "";
}

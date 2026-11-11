namespace Xcmd.Core;

public static class ArchivePath
{
    public static bool IsEntry(string path) => path.Contains('|');
    public static string Root(string file) => file + "|";
    public static string Source(string path) => path[..path.LastIndexOf('|')];
    public static string EntryName(string path) => path[(path.LastIndexOf('|') + 1)..].TrimStart('\\').Replace('\\', '/');
    public static string PhysicalFile(string path) => path.Split('|')[0];
    public static string WorkingDirectory(string path) => IsEntry(path) ? Path.GetDirectoryName(PhysicalFile(path))! : path;
    public static string Display(string path) => path.Replace("|", "!");

    public static string? Parent(string path)
    {
        if (!IsEntry(path)) return Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path));
        var name = EntryName(path);
        if (name.Length == 0) return Parent(Source(path));
        var separator = name.LastIndexOf('/');
        return separator < 0 ? Root(Source(path)) : Path.Combine(Root(Source(path)), name[..separator].Replace('/', '\\'));
    }

    public static bool OpensOnEnter(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".zip" or ".zipx" or ".rar" or ".7z" or ".tar" or ".gz" or ".tgz" or ".bz2" or ".tbz" or ".tbz2" or
        ".xz" or ".txz" or ".lz" or ".tlz" or ".zst" or ".tzst" or ".jar" or ".war" or ".ear" or
        ".nupkg" or ".whl" or ".apk" or ".epub" or ".cbz" or ".cbr" or ".arj" or ".arc" or ".ace" or ".z";

    public static void RequireWritable(string path)
    {
        if (IsEntry(path)) throw new IOException("Archives are read-only. Copy files to a regular folder first.");
    }
}

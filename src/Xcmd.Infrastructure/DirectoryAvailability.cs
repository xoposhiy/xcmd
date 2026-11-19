// @implements FindSavedLocation
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class DirectoryAvailability
{
    public static Task<HashSet<string>> MissingAsync(IEnumerable<string> paths) => Task.Run(() =>
        paths.Where(IsMissing).ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static bool IsMissing(string path)
    {
        if (ArchivePath.IsEntry(path)) return false;
        try { return !File.GetAttributes(path).HasFlag(FileAttributes.Directory); }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
}

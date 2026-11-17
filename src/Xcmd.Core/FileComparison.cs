// @implements ComparePanelFiles
namespace Xcmd.Core;

public sealed record FileComparison(string LeftPath, string RightPath)
{
    public static FileComparison FromEntries(FileEntry? left, FileEntry? right) =>
        new(RequireFile(left, "left"), RequireFile(right, "right"));

    private static string RequireFile(FileEntry? entry, string panel)
    {
        if (entry == null || entry.IsParent || entry.IsDirectory)
            throw new IOException($"Choose a file in the {panel} panel to compare.");
        if (ArchivePath.IsEntry(entry.FullPath) || GitPath.IsEntry(entry.FullPath))
            throw new IOException($"Choose a file on disk in the {panel} panel. Archive members and Git history cannot be compared in VS Code.");
        return entry.FullPath;
    }
}

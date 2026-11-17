// @implements ReleaseHandoff
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class ReleaseCatalog(string root, string currentId, string executableName = "Xcmd.Desktop.exe")
{
    public string CurrentId { get; } = currentId;
    public string CurrentDirectory => Path.Combine(root, CurrentId);
    public string ApplicationIcon => Path.Combine(CurrentDirectory, "app.ico");
    public string LauncherExecutable => Path.GetFullPath(Path.Combine(root, "..", "Xcmd.exe"));

    public async Task<ReleaseInfo?> FindNextAsync()
    {
        var next = await ReleaseFiles.FindLatestAsync(root, executableName, CurrentId);
        return next == null ? null : new ReleaseInfo(next.Id, next.Executable);
    }

    public static ReleaseCatalog? ForApplication()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        return directory.Parent?.Name == "releases" ? new ReleaseCatalog(directory.Parent.FullName, directory.Name) : null;
    }
}

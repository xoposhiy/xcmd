using System.Diagnostics;
using Xcmd.Core;

namespace Xcmd.Probe.Infrastructure;

public static class DirectorySizeProbe
{
    public static long AddNestedFiles(ProbeContext context)
    {
        var nested = Path.Combine(context.LeftPath, "folder1", "nested"); Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "data.bin"), new byte[7]);
        return new FileInfo(Path.Combine(context.LeftPath, "folder1", "inside.txt")).Length + 7;
    }

    public static DateTime ChangeEmptyFolder(ProbeContext context)
    {
        var path = Path.Combine(context.LeftPath, "folder2");
        var changed = Directory.GetLastWriteTimeUtc(path).AddMinutes(1);
        File.WriteAllText(Path.Combine(path, "new.txt"), "Changed folder contents");
        Directory.SetLastWriteTimeUtc(path, changed);
        return Directory.GetLastWriteTimeUtc(path);
    }

    public static async Task SupplyExcludedLinksAsync(ProbeContext context)
    {
        var folder = Path.Combine(context.LeftPath, "folder1");
        var entries = await context.FileSystem.ReadDirectoryAsync(folder, CancellationToken.None);
        var link = new FileEntry(Path.Combine(folder, "back"), "back", true, 0, DateTime.UtcNow, IsLink: true);
        context.FileSystem.SupplyDirectory(folder, [.. entries, link, link with { Name = "file-link", FullPath = Path.Combine(folder, "file-link"), IsDirectory = false, Size = 999 }]);
        context.FileSystem.DenyRead(link.FullPath);
        var rootEntries = await context.FileSystem.ReadDirectoryAsync(context.LeftPath, CancellationToken.None);
        context.FileSystem.SupplyDirectory(context.LeftPath, [.. rootEntries, link with { Name = "root-link", FullPath = Path.Combine(context.LeftPath, "root-link") }]);
    }

    public static async Task CreateJunctionAsync(ProbeContext context)
    {
        var fixture = Path.GetFullPath(context.FixturePath) + Path.DirectorySeparatorChar;
        var link = Path.GetFullPath(Path.Combine(context.LeftPath, "folder1", "back"));
        var target = Path.GetFullPath(context.LeftPath);
        if (!link.StartsWith(fixture, StringComparison.OrdinalIgnoreCase) || !target.StartsWith(fixture, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The junction fixture must stay inside its test directory.");
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("The junction fixture did not start.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (process.ExitCode != 0) throw new IOException("The junction fixture failed: " + await output + await error);
    }
}

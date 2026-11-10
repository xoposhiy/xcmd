using System.Diagnostics;

namespace Xcmd.Probe.Infrastructure;

public static class DirectorySizeProbe
{
    public static long AddNestedFiles(ProbeContext context)
    {
        var nested = Path.Combine(context.LeftPath, "folder1", "nested"); Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "data.bin"), new byte[7]);
        return new FileInfo(Path.Combine(context.LeftPath, "folder1", "inside.txt")).Length + 7;
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

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using static Xcmd.Probe.Infrastructure.WorkspaceProbe;

namespace Xcmd.Probe.Infrastructure;

public sealed record LaunchReport(string WorkingDirectory, string[] Arguments, string? Executable = null, Dictionary<string, string?>? Environment = null, int ProcessId = 0);
public sealed record LaunchFixture(string Directory, string Executable, string Report);

public static class CommandProbe
{
    public static LaunchFixture Prepare(ProbeContext context)
    {
        var target = Path.Combine(context.FixturePath, "application & 日本語"); Directory.CreateDirectory(target);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "launch-probe")))
            File.Copy(path, Path.Combine(target, Path.GetFileName(path)));
        var executable = Path.Combine(target, "tool with spaces.exe"); File.Move(Path.Combine(target, "Xcmd.LaunchProbe.exe"), executable);
        return new(target, executable, Path.Combine(target, "launch-report.json"));
    }

    public static async Task<LaunchReport> ReadReportAsync(string path)
    {
        await WaitAsync(() => File.Exists(path));
        var report = JsonSerializer.Deserialize<LaunchReport>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        if (report.ProcessId > 0)
        {
            try
            {
                using var process = Process.GetProcessById(report.ProcessId);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (ArgumentException) { }
        }
        return report;
    }

    public static void CreateShortcut(string path, LaunchFixture fixture, string workingDirectory)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The shortcut fixture requires Windows.");
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("Windows Script Host is unavailable.");
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic shortcut = shell.CreateShortcut(path);
        try { shortcut.TargetPath = fixture.Executable; shortcut.WorkingDirectory = workingDirectory; shortcut.Save(); }
        finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
    }

}

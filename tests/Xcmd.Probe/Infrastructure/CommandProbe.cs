using System.Diagnostics;
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
        var report = JsonSerializer.Deserialize<LaunchReport>(await ReadReportTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
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

    private static async Task<string> ReadReportTextAsync(string path)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try { return await File.ReadAllTextAsync(path); }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 && timer.Elapsed < TimeSpan.FromSeconds(5))
            { await Task.Delay(25); }
        }
    }

    public static async Task<LaunchReport> ReadLockedReportAsync(ProbeContext context)
    {
        var path = Path.Combine(context.FixturePath, "locked-launch-report.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new LaunchReport(context.LeftPath, [])));
        Task<LaunchReport> reading;
        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            reading = ReadReportAsync(path);
            ProbeContext.Check(!reading.IsCompleted, "The report reader waits while another process holds the file.");
        }
        return await reading;
    }

    public static void CreateShortcut(string path, LaunchFixture fixture, string workingDirectory) =>
        ShortcutProbe.Create(path, fixture.Executable, workingDirectory);

}

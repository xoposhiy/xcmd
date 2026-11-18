// @tests RunCommand, ExternalLaunch
using Microsoft.Win32;
using System.Text;

namespace Xcmd.Probe.Infrastructure;

public sealed class EnvironmentFixture : IDisposable
{
    public string Name { get; } = "XCMD_PROBE_" + Guid.NewGuid().ToString("N");

    public EnvironmentFixture(string? userValue = null, string? processValue = null)
    {
        SetUser(userValue);
        SetProcess(processValue);
    }

    public void SetUser(string? value, bool expandable = false)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The environment fixture requires Windows.");
        using var key = Registry.CurrentUser.OpenSubKey("Environment", writable: true)
            ?? throw new IOException("The user environment registry key is unavailable.");
        if (value == null) key.DeleteValue(Name, throwOnMissingValue: false);
        else key.SetValue(Name, value, expandable ? RegistryValueKind.ExpandString : RegistryValueKind.String);
    }

    public void SetProcess(string? value) => Environment.SetEnvironmentVariable(Name, value);
    public string? ProcessValue => Environment.GetEnvironmentVariable(Name);

    public static string InspectThroughCmd(LaunchFixture fixture, params string[] names) =>
        "cmd /d /q /v:off /c \"\"" + fixture.Executable + "\" --environment \"" + fixture.Report + "\" " + string.Join(' ', names) + "\"";

    public static string InspectThroughPowerShell(LaunchFixture fixture, params string[] names)
    {
        var script = "& '" + fixture.Executable.Replace("'", "''", StringComparison.Ordinal) + "' --environment '" +
            fixture.Report.Replace("'", "''", StringComparison.Ordinal) + "' " + string.Join(' ', names);
        return "powershell.exe -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }

    public void Dispose()
    {
        SetUser(null);
        SetProcess(null);
    }
}

public sealed class LaunchEnvironmentFixture : IDisposable
{
    private readonly string? originalPath = Environment.GetEnvironmentVariable("Path");
    public Dictionary<string, string> Values { get; } = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
        .Where(entry => entry.Key is string && entry.Value is string).ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.OrdinalIgnoreCase);

    public void SetStalePath() => Environment.SetEnvironmentVariable("Path", "C:\\xcmd-missing-path");
    public string? ProcessPath => Environment.GetEnvironmentVariable("Path");
    public void Dispose() => Environment.SetEnvironmentVariable("Path", originalPath);
}

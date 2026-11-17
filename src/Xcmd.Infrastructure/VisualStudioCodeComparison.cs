// @implements ComparePanelFiles, ExternalLaunch
using Microsoft.Win32;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class VisualStudioCodeComparison(string? executable = null)
{
    public Task<ExternalLaunch> PrepareAsync(FileComparison files) => Task.Run(() =>
    {
        foreach (var path in new[] { files.LeftPath, files.RightPath })
            if (!File.Exists(path)) throw new IOException("Cannot compare: the file is no longer available: " + path);
        var program = executable ?? FindExecutable();
        if (program == null || !File.Exists(program))
            throw new IOException("Visual Studio Code was not found. Install VS Code to compare files.");
        return new ExternalLaunch(ExternalLaunchKind.Command, program, Path.GetDirectoryName(files.LeftPath)!,
            "--new-window --diff " + CommandLine.FormatArgument(files.LeftPath) + " " + CommandLine.FormatArgument(files.RightPath));
    });

    private static string? FindExecutable()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("VS Code comparison requires Windows.");
        const string appKey = @"\Software\Microsoft\Windows\CurrentVersion\App Paths\Code.exe";
        var registered = new[]
        {
            Registry.GetValue("HKEY_CURRENT_USER" + appKey, "", null) as string,
            Registry.GetValue("HKEY_LOCAL_MACHINE" + appKey, "", null) as string
        };
        var installed = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code", "Code.exe")
        };
        var fromPath = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(folder => folder.Trim().Trim('"')).Where(Path.IsPathFullyQualified)
            .SelectMany(folder => new[] { Path.Combine(folder, "Code.exe"), Path.GetFullPath(Path.Combine(folder, "..", "Code.exe")) });
        return registered.Concat(installed).Concat(fromPath).FirstOrDefault(File.Exists);
    }
}

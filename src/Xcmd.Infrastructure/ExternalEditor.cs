// @implements CreateFile, OpenExternalTool, ExternalLaunch
using Microsoft.Win32;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class ExternalEditor(string? executable = null)
{
    public async Task<ExternalLaunch> CreateAsync(string path, string directory)
    {
        ArchivePath.RequireWritable(path);
        return await Task.Run(() =>
        {
            var editor = RequireEditor();
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            return new ExternalLaunch(ExternalLaunchKind.Command, editor, directory, CommandLine.FormatArgument(path));
        });
    }
    public Task<ExternalLaunch> PrepareAsync(string path, string directory) => Task.Run(() =>
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The file no longer exists.", path);
        var editor = RequireEditor();
        return new ExternalLaunch(ExternalLaunchKind.Command, editor, directory, CommandLine.FormatArgument(path));
    });

    private string RequireEditor()
    {
        var editor = executable ?? FindNotepad();
        if (editor == null || !File.Exists(editor)) throw new IOException("Notepad++ was not found. Install Notepad++ to edit files.");
        return editor;
    }

    private static string? FindNotepad()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The external editor requires Windows.");
        const string key = @"Software\Microsoft\Windows\CurrentVersion\App Paths\notepad++.exe";
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var app = hive.OpenSubKey(key);
            if (app?.GetValue(null) is string path && File.Exists(path)) return path;
        }
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var path = Path.Combine(Environment.GetFolderPath(folder), "Notepad++", "notepad++.exe");
            if (File.Exists(path)) return path;
        }
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var path = Path.Combine(folder.Trim('"'), "notepad++.exe");
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

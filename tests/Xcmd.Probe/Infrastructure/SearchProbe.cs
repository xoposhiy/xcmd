using System.Diagnostics;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Desktop;

namespace Xcmd.Probe.Infrastructure;

public static class SearchProbe
{
    public static async Task<FileSearchWindow> OpenAsync(ProbeContext context, string name, string text = "")
    {
        if (!Directory.Exists(Path.Combine(context.LeftPath, ".git")))
        {
            await GitAsync(context.LeftPath, "init", "--quiet");
            await GitAsync(context.LeftPath, "config", "core.excludesFile", Path.Combine(context.FixturePath, "empty-excludes"));
        }
        if (context.Window.FileSearch.Window == null) await context.KeyAsync(Key.F7, RawInputModifiers.Alt);
        var window = context.Window.FileSearch.Window ?? throw new InvalidOperationException("Alt+F7 opens file search.");
        window.NameEditor.Text = name; window.TextEditor.Text = text;
        window.NameEditor.Focus();
        context.PressKey(Key.Enter, target: window);
        await context.SettleAsync();
        return window;
    }

    public static FileSearchResult[] Results(FileSearchWindow window) => window.Results.ItemsSource!.Cast<FileSearchResult>().ToArray();

    public static async Task GitAsync(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        ProbeContext.Equal(0, process.ExitCode, "Git fixture command: " + await error + await output);
        var objects = Path.Combine(root, ".git", "objects");
        if (Directory.Exists(objects))
            foreach (var file in new DirectoryInfo(objects).EnumerateFiles("*", SearchOption.AllDirectories))
                file.Attributes &= ~FileAttributes.ReadOnly;
    }

    public static void TextFile(ProbeContext context, string name, string text)
    {
        var path = Path.Combine(context.LeftPath, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}

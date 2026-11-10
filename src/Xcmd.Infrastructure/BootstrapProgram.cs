using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Xcmd.Infrastructure;

public static class BootstrapProgram
{
    [STAThread]
    public static Task<int> Main(string[] args) => RunAsync(AppContext.BaseDirectory, args,
        args.FirstOrDefault() == "--create-shortcut" ? Console.Error.WriteLine : ShowError);

    public static async Task<int> RunAsync(string root, string[] args, Action<string> reportError)
    {
        try
        {
            if (args is ["--create-shortcut", ..])
            {
                var (path, launcher, icon) = args switch
                {
                    [_, var linkPath] => (linkPath, Path.Combine(root, "Xcmd.exe"), (string?)null),
                    [_, var linkPath, var target, var image] => (linkPath, target, image),
                    _ => throw new ArgumentException("Shortcut setup requires a path and an optional launcher and icon.")
                };
                WindowsShortcut.Create(path, launcher, icon);
                return 0;
            }
            var release = await ReleaseFiles.FindLatestAsync(Path.Combine(root, "releases"), "Xcmd.Desktop.exe")
                ?? throw new IOException("No verified release is available. Keep Xcmd.exe next to the releases folder and publish a release.");
            var start = new ProcessStartInfo(release.Executable)
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory
            };
            foreach (var argument in args) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new IOException("The verified release could not be started.");
            return 0;
        }
        catch (Exception error)
        {
            reportError("Xcmd could not start.\n\n" + error.Message);
            return 1;
        }
    }

    private static void ShowError(string message) => MessageBoxW(0, message, "Xcmd", 0x10);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(nint owner, string text, string caption, uint type);
}

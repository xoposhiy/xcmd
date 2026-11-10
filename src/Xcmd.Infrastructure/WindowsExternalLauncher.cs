using System.ComponentModel;
using System.Diagnostics;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class WindowsExternalLauncher(bool showWindows = true, Func<IReadOnlyDictionary<string, string>>? readEnvironment = null) : IExternalLauncher
{
    private readonly WindowsLaunchEnvironment environment = new(readEnvironment);

    public Task LaunchAsync(ExternalLaunch request) => Task.Run(() =>
    {
        try { environment.Run(() => Launch(request)); }
        catch (Exception error) when (error is Win32Exception or System.Security.SecurityException)
        { throw new IOException("Windows could not open the file or start the application: " + error.Message, error); }
    });

    private void Launch(ExternalLaunch request)
    {
        if (!Directory.Exists(request.WorkingDirectory)) throw new DirectoryNotFoundException("The working directory is unavailable.");
        if (request.Kind == ExternalLaunchKind.File && !File.Exists(request.Target)) throw new FileNotFoundException("The file no longer exists.", request.Target);
        var target = request.Kind == ExternalLaunchKind.Command ? Environment.ExpandEnvironmentVariables(request.Target) : request.Target;
        var start = new ProcessStartInfo(target)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = true,
            CreateNoWindow = !showWindows,
            WindowStyle = showWindows ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
            ErrorDialog = false,
            Arguments = request.Arguments
        };
        using var process = Process.Start(start);
    }
}

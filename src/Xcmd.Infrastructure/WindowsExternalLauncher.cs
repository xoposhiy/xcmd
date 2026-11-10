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
        { throw new IOException(LaunchError(request.Target, error), error); }
    });

    private static string LaunchError(string target, Exception error)
    {
        var reason = error is Win32Exception native ? native.NativeErrorCode switch
        {
            2 or 3 => "not found",
            5 => "access denied",
            32 => "the file is in use",
            193 => "invalid program format",
            1155 => "no app is associated with this file type",
            1223 => "canceled",
            _ => new Win32Exception(native.NativeErrorCode).Message
        } : "access denied";
        return $"Cannot open '{target}': {reason}.";
    }

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

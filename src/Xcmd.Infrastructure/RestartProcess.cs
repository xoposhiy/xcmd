// @implements ReleaseHandoff
using System.Diagnostics;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class RestartProcess(string? hostArgument = null)
{
    public async Task PrepareAsync(ReleaseInfo release, string sessionPath, string handoffPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(handoffPath);
        var start = new ProcessStartInfo(release.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = System.IO.Path.GetDirectoryName(release.Executable)!
        };
        if (hostArgument != null) start.ArgumentList.Add(hostArgument);
        foreach (var argument in new[] { "--session", sessionPath, "--handoff", handoffPath, "--wait-for", Environment.ProcessId.ToString() })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("The new release could not be started.");
        var ready = System.IO.Path.Combine(handoffPath, "ready");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            while (!File.Exists(ready))
            {
                if (process.HasExited) throw new IOException("The new release exited before it was ready.");
                await Task.Delay(50, timeout.Token);
            }
        }
        catch
        {
            if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(CancellationToken.None); }
            throw;
        }
    }

    public static async Task WaitForPreviousAsync(LaunchSettings settings)
    {
        if (settings.WaitForProcess is not { } processId) return;
        var state = await new SessionStore(settings.SessionPath).ReadAsync();
        if (state == null) throw new InvalidDataException("The restart session is missing.");
        Directory.CreateDirectory(settings.HandoffPath!);
        await File.WriteAllTextAsync(System.IO.Path.Combine(settings.HandoffPath!, "ready"), "ready");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            using var previous = Process.GetProcessById(processId);
            await previous.WaitForExitAsync(timeout.Token);
        }
        catch (ArgumentException) { }
    }
}

public sealed record LaunchSettings(string SessionPath, string? HandoffPath, int? WaitForProcess, string[] Directories)
{
    public static LaunchSettings Parse(string[] arguments)
    {
        var session = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xcmd", "session.json");
        string? handoff = null;
        int? waitFor = null;
        var directories = new List<string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] is "--session" or "--handoff" or "--wait-for")
            {
                var key = arguments[i];
                if (++i >= arguments.Length) throw new ArgumentException("A startup option is missing its value.");
                if (key == "--session") session = System.IO.Path.GetFullPath(arguments[i]);
                else if (key == "--handoff") handoff = System.IO.Path.GetFullPath(arguments[i]);
                else waitFor = int.Parse(arguments[i], System.Globalization.CultureInfo.InvariantCulture);
            }
            else directories.Add(System.IO.Path.GetFullPath(arguments[i]));
        }
        if (waitFor != null && handoff == null) throw new ArgumentException("A restart needs a handoff directory.");
        return new(session, handoff, waitFor, directories.ToArray());
    }
}

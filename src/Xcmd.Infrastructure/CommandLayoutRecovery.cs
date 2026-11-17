using System.ComponentModel;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class CommandLayoutRecovery
{
    public static async Task<ExternalLaunch> LaunchAsync(ExternalLaunch request, Func<ExternalLaunch, Task> launch,
        Func<IReadOnlyDictionary<string, string>>? readEnvironment = null)
    {
        try { await launch(request); return request; }
        catch (IOException error) when (request.Kind == ExternalLaunchKind.Command && IsMissing(error))
        {
            var candidates = await Task.Run(() => FindCandidates(request, readEnvironment)).ConfigureAwait(true);
            if (candidates.Length != 1) throw;
            var corrected = request with { Target = candidates[0] };
            await launch(corrected);
            return corrected;
        }
    }

    private static string[] FindCandidates(ExternalLaunch request, Func<IReadOnlyDictionary<string, string>>? readEnvironment)
    {
        string[] candidates = [];
        new WindowsLaunchEnvironment(readEnvironment).Run(() => candidates = KeyboardLayouts.PrefixCandidates(request.Target)
            .Where(value => !value.Equals(request.Target, StringComparison.OrdinalIgnoreCase))
            .Where(value => IsProgram(value, request.WorkingDirectory)).ToArray());
        return candidates;
    }

    private static bool IsProgram(string name, string directory)
    {
        var extensions = Path.HasExtension(name) ? new[] { "" } : [".exe", ".com", ".cmd", ".bat"];
        var folders = new[] { directory, Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            foreach (var extension in extensions)
            {
                try
                {
                    var path = Path.GetFullPath(Path.Combine(folder.Trim('"'), name + extension));
                    if (new[] { ".exe", ".com", ".cmd", ".bat" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) && File.Exists(path)) return true;
                }
                catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { }
            }
        }
        return false;
    }

    private static bool IsMissing(Exception error) => error is Win32Exception { NativeErrorCode: 2 or 3 }
        || error is FileNotFoundException
        || error.InnerException is { } inner && IsMissing(inner);
}

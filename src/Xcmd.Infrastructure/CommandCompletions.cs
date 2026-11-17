// @implements CompleteCommand, ExternalLaunch
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class CommandCompletions
{
    public static Task<CommandSuggestion[]> FindAsync(CommandToken token, string directory, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var values = new List<CommandSuggestion>();
        var prefix = token.Prefix;
        if (prefix.StartsWith('%') && prefix.IndexOf('%', 1) < 0)
        {
            foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>())
                if (name.StartsWith(prefix[1..], StringComparison.OrdinalIgnoreCase)) values.Add(new("%" + name + "%"));
        }
        else
        {
            var separator = prefix.LastIndexOfAny(['\\', '/']);
            var parent = separator >= 0 ? prefix[..(separator + 1)] : "";
            var leaf = prefix[(separator + 1)..];
            var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(parent.Length == 0 ? "." : parent), directory);
            AddFiles(path, leaf, parent, false, values, cancellationToken);
            if (token.Program && separator < 0)
            {
                foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Path.IsPathFullyQualified(folder.Trim('"'))) AddFiles(folder.Trim('"'), leaf, "", true, values, cancellationToken);
                    if (values.Count >= 300) break;
                }
            }
        }
        return values.DistinctBy(item => item.Value, StringComparer.OrdinalIgnoreCase).OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
    }, cancellationToken);

    private static void AddFiles(string path, string leaf, string prefix, bool programs, List<CommandSuggestion> values, CancellationToken token)
    {
        try
        {
            foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                token.ThrowIfCancellationRequested();
                if (!entry.Name.StartsWith(leaf, StringComparison.OrdinalIgnoreCase)) continue;
                var folder = entry.Attributes.HasFlag(FileAttributes.Directory);
                if (programs && (folder || !new[] { ".exe", ".com", ".cmd", ".bat" }.Contains(entry.Extension, StringComparer.OrdinalIgnoreCase))) continue;
                values.Add(new(prefix + entry.Name + (folder ? "\\" : ""), folder));
                if (values.Count >= 300) break;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
    }
}

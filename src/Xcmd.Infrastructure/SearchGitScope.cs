using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Xcmd.Infrastructure;

internal sealed class SearchGitScope
{
    private readonly Dictionary<string, string?> roots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> tracked = new(StringComparer.OrdinalIgnoreCase);

    public async Task<bool> IsIgnoredRootAsync(string path, CancellationToken token)
    {
        var root = FindRoot(path);
        if (root == null || root.Equals(path, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var ignored = await RunAsync(root, ["check-ignore", "--no-index", "--stdin", "-z"], path + '\0', token).ConfigureAwait(false);
            return ignored.Length > 0;
        }
        catch (Win32Exception error) { throw new IOException("Git ignore rules could not be read. " + error.Message, error); }
    }

    public async Task<(Dictionary<string, string> Ignored, HashSet<string> Tracked)> ClassifyAsync(string directory, string[] paths, CancellationToken token)
    {
        var root = FindRoot(directory);
        if (root == null) return (new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));
        try
        {
            if (!tracked.TryGetValue(root, out var known))
            {
                var output = await RunAsync(root, ["ls-files", "--cached", "--full-name", "-z"], null, token).ConfigureAwait(false);
                known = new(StringComparer.OrdinalIgnoreCase);
                foreach (var name in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
                {
                    var path = Path.GetFullPath(name, root);
                    known.Add(path);
                    while (Path.GetDirectoryName(path) is { } parent && parent.Length >= root.Length)
                    { known.Add(parent); path = parent; }
                }
                tracked[root] = known;
            }
            var ignored = paths.Length == 0 ? "" : await RunAsync(root, ["check-ignore", "--verbose", "--stdin", "-z"], string.Join('\0', paths) + '\0', token).ConfigureAwait(false);
            var fields = ignored.Split('\0');
            var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var offset = 0; offset + 3 < fields.Length; offset += 4)
                if (!fields[offset + 2].StartsWith('!')) rules[fields[offset + 3]] = Path.GetFullPath(fields[offset], root) + ":" + fields[offset + 1] + " · " + fields[offset + 2];
            return (rules, known);
        }
        catch (Win32Exception error) { throw new IOException("Git ignore rules could not be read. Install Git or search ignored files too. " + error.Message, error); }
    }

    private string? FindRoot(string directory)
    {
        if (roots.TryGetValue(directory, out var cached)) return cached;
        var root = File.Exists(Path.Combine(directory, ".git")) || Directory.Exists(Path.Combine(directory, ".git")) ? directory
            : Path.GetDirectoryName(directory) is { } parent ? FindRoot(parent) : null;
        roots[directory] = root;
        return root;
    }

    private static async Task<string> RunAsync(string root, string[] arguments, string? input, CancellationToken token)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(false)
        };
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Git could not be started.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var cancellation = deadline.Token.Register(() => { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            if (input != null) await process.StandardInput.WriteAsync(input.AsMemory(), deadline.Token).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            var result = await output.ConfigureAwait(false);
            var error = await errors.ConfigureAwait(false);
            if (process.ExitCode > 1) throw new IOException("Git ignore rules could not be read: " + error.Trim());
            return result;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException("Reading Git ignore rules timed out."); }
    }
}

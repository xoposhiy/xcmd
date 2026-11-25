// @implements SeeRepositoryState, RepositoryStatus
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class GitHubWorkflows
{
    internal static async Task<WorkflowTarget?> TargetAsync(string remotes, string branch, string? upstream, string? detachedCommit,
        Func<string, Task<string?>> readTrackingCommit)
    {
        var candidates = remotes.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd().Split('\t', 2)).Where(parts => parts.Length == 2 && parts[1].EndsWith(" (fetch)", StringComparison.Ordinal))
            .OrderByDescending(parts => upstream?.StartsWith(parts[0] + "/", StringComparison.Ordinal) == true)
            .ThenByDescending(parts => parts[0] == "origin");
        foreach (var parts in candidates)
        {
            var repo = Repository(parts[1][..^8]);
            if (repo == null) continue;
            if (detachedCommit != null) return new(repo, detachedCommit, true);
            var remoteBranch = upstream?.StartsWith(parts[0] + "/", StringComparison.Ordinal) == true ? upstream[(parts[0].Length + 1)..] : branch;
            var sha = await readTrackingCommit($"refs/remotes/{parts[0]}/{remoteBranch}").ConfigureAwait(false);
            return new(repo, remoteBranch, false, sha?.Trim());
        }
        return null;
    }

    private static string? Repository(string url)
    {
        if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase)) url = "ssh://git@github.com/" + url[15..];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return null;
        var path = uri.AbsolutePath.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
        var parts = path.Split('/');
        return parts.Length == 2 && parts.All(part => part.Length > 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            ? "github.com/" + path : null;
    }

    public static Task<WorkflowStatus> ReadAsync(WorkflowTarget target) => ReadAsync(target, RunAsync);

    public static async Task<WorkflowStatus> ReadAsync(WorkflowTarget target, Func<string[], Task<string?>> run)
    {
        var commit = target.Ref;
        List<string> args = ["run", "list", "--repo", target.Repository];
        if (!target.IsCommit)
        {
            var repo = target.Repository["github.com/".Length..];
            var tip = await run(["api", "--hostname", "github.com", $"repos/{repo}/branches/{Uri.EscapeDataString(target.Ref)}", "--jq", ".commit.sha"]).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tip)) return WorkflowStatus.Unavailable;
            commit = tip.Trim();
            args.AddRange(["--branch", target.Ref]);
        }
        args.AddRange(["--commit", commit, "--limit", "100", "--json", "workflowDatabaseId,workflowName,headSha,status,conclusion,url"]);
        try
        {
            var json = await run([.. args]).ConfigureAwait(false);
            if (json == null) return WorkflowStatus.Unavailable;
            using var data = JsonDocument.Parse(json);
            var runs = data.RootElement.EnumerateArray().Select(row => new WorkflowRun(
                row.GetProperty("workflowDatabaseId").ToString(), row.GetProperty("workflowName").GetString() ?? "Workflow",
                row.GetProperty("headSha").GetString()!, row.GetProperty("status").GetString()!, row.GetProperty("conclusion").GetString() ?? "", RunUrl(row))).ToArray();
            if (runs.Length > 0) return WorkflowStatus.FromRuns(commit, runs);
            var workflows = await run(["workflow", "list", "--all", "--repo", target.Repository, "--limit", "1", "--json", "id"]).ConfigureAwait(false);
            if (workflows == null) return WorkflowStatus.Unavailable;
            using var list = JsonDocument.Parse(workflows);
            return WorkflowStatus.Empty(list.RootElement.GetArrayLength() > 0);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) { return WorkflowStatus.Unavailable; }
    }

    private static Uri? RunUrl(JsonElement row) =>
        row.TryGetProperty("url", out var value) && value.ValueKind == JsonValueKind.String
        && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var url)
        && url.Scheme == Uri.UriSchemeHttps && url.Host == "github.com" ? url : null;

    private static Task<string?> RunAsync(string[] arguments) => StatusCommand.ReadAsync("gh.exe", arguments, TimeSpan.FromSeconds(10), CancellationToken.None);
}

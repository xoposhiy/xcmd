// @implements SeeRepositoryState, RepositoryStatus
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public static class GitHubWorkflows
{
    public static WorkflowTarget? Target(string remotes, string branch, string? upstream, string? detachedCommit)
    {
        var candidates = remotes.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd().Split('\t', 2)).Where(parts => parts.Length == 2 && parts[1].EndsWith(" (fetch)", StringComparison.Ordinal))
            .OrderByDescending(parts => upstream?.StartsWith(parts[0] + "/", StringComparison.Ordinal) == true)
            .ThenByDescending(parts => parts[0] == "origin");
        foreach (var parts in candidates)
        {
            var repo = Repository(parts[1][..^8]);
            if (repo == null) continue;
            var remoteBranch = upstream?.StartsWith(parts[0] + "/", StringComparison.Ordinal) == true ? upstream[(parts[0].Length + 1)..] : branch;
            return new(repo, detachedCommit ?? remoteBranch, detachedCommit != null);
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

    public static async Task<WorkflowStatus> ReadAsync(WorkflowTarget target)
    {
        var args = new[] { "run", "list", "--repo", target.Repository, target.IsCommit ? "--commit" : "--branch", target.Ref,
            "--limit", "100", "--json", "workflowDatabaseId,workflowName,headSha,status,conclusion" };
        try
        {
            var json = await RunAsync(args).ConfigureAwait(false);
            if (json == null) return WorkflowStatus.Unavailable;
            using var data = JsonDocument.Parse(json);
            var runs = data.RootElement.EnumerateArray().Select(row => new WorkflowRun(
                row.GetProperty("workflowDatabaseId").ToString(), row.GetProperty("workflowName").GetString() ?? "Workflow",
                row.GetProperty("headSha").GetString()!, row.GetProperty("status").GetString()!, row.GetProperty("conclusion").GetString() ?? "")).ToArray();
            if (runs.Length > 0) return WorkflowStatus.FromRuns(runs);
            var workflows = await RunAsync(["workflow", "list", "--all", "--repo", target.Repository, "--limit", "1", "--json", "id"]).ConfigureAwait(false);
            if (workflows == null) return WorkflowStatus.Unavailable;
            using var list = JsonDocument.Parse(workflows);
            return WorkflowStatus.Empty(list.RootElement.GetArrayLength() > 0);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) { return WorkflowStatus.Unavailable; }
    }

    private static Task<string?> RunAsync(string[] arguments) => StatusCommand.ReadAsync("gh.exe", arguments, TimeSpan.FromSeconds(10), CancellationToken.None);
}

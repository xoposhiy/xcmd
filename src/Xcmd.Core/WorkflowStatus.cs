// @implements SeeRepositoryState
namespace Xcmd.Core;

public sealed record WorkflowTarget(string Repository, string Ref, bool IsCommit, string? TrackingCommit = null);
public sealed record WorkflowRun(string Workflow, string Name, string Commit, string Status, string Conclusion);
public sealed record WorkflowStatus(string Label, string Details, TimeSpan RefreshAfter)
{
    public static WorkflowStatus Unavailable => new("Unavailable", "Could not read GitHub Actions. Check the connection and GitHub CLI sign-in.", TimeSpan.FromMinutes(5));
    public static WorkflowStatus Empty(bool hasWorkflows) => hasWorkflows
        ? new("Unknown", "No workflow runs for this commit yet.", TimeSpan.FromSeconds(30))
        : new("", "This repository has no workflows.", TimeSpan.FromMinutes(15));

    public static WorkflowStatus FromRuns(string commit, IEnumerable<WorkflowRun> runs)
    {
        var latest = runs.Where(run => run.Commit == commit).DistinctBy(run => run.Workflow).ToArray();
        if (latest.Length == 0) return Empty(true);
        var pending = latest.Any(run => run.Status != "completed");
        var failed = latest.Any(run => run.Conclusion is "failure" or "timed_out" or "action_required" or "startup_failure");
        var label = failed ? "Failed" : pending ? PendingLabel(latest) : CompletedLabel(latest);
        var details = string.Join("\n", latest.Select(run => run.Name + ": " + (run.Status == "completed" ? run.Conclusion : run.Status).Replace('_', ' ')));
        return new(label, commit[..Math.Min(8, commit.Length)] + "\n" + details, pending ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5));
    }

    private static string PendingLabel(WorkflowRun[] runs) => runs.Any(run => run.Status == "in_progress") ? "Running" : "Pending";

    private static string CompletedLabel(WorkflowRun[] runs) => runs.Select(run => run.Conclusion).Distinct().Take(2).ToArray() switch
    {
        ["success"] => "Passed",
        ["cancelled"] => "Cancelled",
        ["skipped"] => "Skipped",
        _ => "Completed"
    };
}

public sealed class WorkflowStatusCache(Func<WorkflowTarget, Task<WorkflowStatus>> read, TimeProvider clock)
{
    private sealed class Entry
    {
        public Task<WorkflowStatus>? Read;
        public DateTimeOffset NextRead;
    }

    private readonly Dictionary<WorkflowTarget, Entry> entries = [];

    public Task<WorkflowStatus> ReadAsync(WorkflowTarget target)
    {
        lock (entries)
        {
            if (!entries.TryGetValue(target, out var entry))
            {
                if (entries.Count >= 64 && entries.Where(pair => pair.Value.Read?.IsCompleted == true)
                    .OrderBy(pair => pair.Value.NextRead).Select(pair => pair.Key).FirstOrDefault() is { } oldest)
                    entries.Remove(oldest);
                entries[target] = entry = new();
            }
            if (entry.Read == null || entry.Read.IsCompleted && clock.GetUtcNow() >= entry.NextRead)
                entry.Read = Task.Run(() => RefreshAsync(target, entry));
            return entry.Read;
        }
    }

    private async Task<WorkflowStatus> RefreshAsync(WorkflowTarget target, Entry entry)
    {
        var result = await read(target).ConfigureAwait(false);
        lock (entries) entry.NextRead = clock.GetUtcNow() + result.RefreshAfter;
        return result;
    }
}

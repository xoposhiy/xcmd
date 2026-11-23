// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed record PendingTool(string Name, string Input, long Start, string Directory);

internal sealed class TraceRecords(string project, bool fileCoverageStudy)
{
    internal long First { get; private set; } = long.MaxValue;
    internal long Last { get; private set; } = long.MinValue;
    internal List<TraceCall> Calls { get; } = [];
    internal List<TraceMarker> Tasks { get; } = [];
    internal List<TraceMarker> Steps { get; } = [];
    internal List<TraceMarker> Progress { get; } = [];
    internal Dictionary<Guid, string> Children { get; } = [];
    internal int Skipped { get; set; }
    internal int Unfinished { get; set; }
    internal int MissingAgents { get; set; }
    internal string Project { get; } = project;
    internal bool FileCoverageStudy { get; } = fileCoverageStudy;
    internal Dictionary<string, ClassificationEvidence> Evidence { get; } = [];
    internal string? ClassificationError { get; set; }

    internal ClassificationEvidence ForAgent(string name, string stream) => Evidence[name] = new(Project, stream);

    internal void Observe(long at) { First = Math.Min(First, at); Last = Math.Max(Last, at); }

    internal SessionTrace Finish(AgentSession session)
    {
        var notes = new List<string>();
        if (Skipped > 0) notes.Add($"Skipped {Skipped:N0} unreadable records.");
        if (Unfinished > 0) notes.Add($"Awaiting results for {Unfinished:N0} tool calls.");
        if (MissingAgents > 0) notes.Add($"Local transcripts unavailable for {MissingAgents:N0} linked agents.");
        return new(First == long.MaxValue ? session.Updated.ToUnixTimeMilliseconds() : First, Last, Calls, Tasks, Steps.Count > 0 ? Steps : Progress,
            notes.Count == 0 ? null : string.Join(" ", notes), TranscriptPath.IsUnixRoot(Project))
        { ClassificationError = ClassificationError };
    }

    internal void MarkProgress(string agent, string text, long at)
    {
        if (agent != SessionTrace.MainAgent || string.IsNullOrWhiteSpace(text)) return;
        var title = TraceJson.Caption(text);
        if (Progress.LastOrDefault()?.Title != title) Progress.Add(new(title, at));
    }

    internal static TraceCall Call(string agent, PendingTool tool, long end, string command, long characters,
        TraceFile[] files, bool failed)
    {
        var title = TraceJson.Caption(command.Length == 0 ? tool.Name : command);
        return new(agent, title, Math.Min(tool.Start, end), end,
            Math.Max(1, (tool.Input.Length + characters + 3) / 4), failed ? [] : files, failed,
            FileEvidence(tool, command));
    }

    private static string FileEvidence(PendingTool tool, string command)
    {
        var label = command;
        if (label.Length == 0)
        {
            var input = TraceJson.Object(tool.Input);
            var path = input.Str("file_path") is { Length: > 0 } file ? file : input.Str("notebook_path");
            label = path.Length > 0 ? tool.Name + " " + path : tool.Name;
        }
        return tool.Directory.Length > 0 ? label + "\nWorking directory: " + tool.Directory : label;
    }

    internal void MarkTask(string agent, string text, long at)
    {
        if (agent != SessionTrace.MainAgent || string.IsNullOrWhiteSpace(text)) return;
        var line = text.TrimStart();
        if (line.StartsWith("<environment_context>", StringComparison.Ordinal) || line.StartsWith("<turn_aborted>", StringComparison.Ordinal) ||
            line.StartsWith("# AGENTS.md instructions", StringComparison.Ordinal) || line.StartsWith("<local-command-caveat>", StringComparison.Ordinal)) return;
        Tasks.Add(new(TraceJson.Caption(text), at));
    }

    internal void MarkStep(string agent, JsonElement plans, long at)
    {
        if (agent != SessionTrace.MainAgent) return;
        foreach (var item in plans.Items().Where(item => item.Str("status") == "in_progress"))
        {
            var title = item.Str("step") is { Length: > 0 } step ? step : item.Str("content");
            if (title.Length > 0 && Steps.LastOrDefault()?.Title != title) Steps.Add(new(TraceJson.Caption(title), at));
            return;
        }
    }
}

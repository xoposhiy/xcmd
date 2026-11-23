// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class CodexTraceReader(TraceRecords trace, string agent, Guid thread, string directory, ClassificationEvidence evidence)
{
    private readonly Dictionary<string, PendingTool> pending = [];
    private readonly List<TraceCall> structured = [];
    private readonly HashSet<string> runtimeIds = [];
    private readonly List<(string Id, string Tool, TraceCall Call)> raw = [];
    private readonly List<TraceMarker> messages = [];
    private readonly List<TraceMarker> userEvents = [];
    private readonly List<TraceMarker> legacyMessages = [];
    private string cwd = directory;
    private bool hasRuntimeItems;

    internal void Record(JsonElement record, long at, int line)
    {
        evidence.Line = line;
        var p = record.Field("payload");
        if (Guid.TryParse(p.Str("thread_id"), out var owner) && owner != thread) return;
        trace.Observe(at);
        switch (record.Str("type"))
        {
            case "session_meta":
            case "turn_context":
                if (p.Str("cwd") is { Length: > 0 } location) cwd = location;
                break;
            case "response_item":
                Response(p, at);
                break;
            case "event_msg" when p.Str("type") == "item_completed":
                Item(p, at);
                break;
            case "event_msg" when p.Str("type") == "user_message":
                userEvents.Add(new(p.Str("message"), at));
                evidence.Message("UserMessage", p.Str("message"));
                break;
        }
    }

    private void Response(JsonElement p, long at)
    {
        var id = p.Str("call_id");
        switch (p.Str("type"))
        {
            case "function_call":
            case "custom_tool_call":
                var input = p.Str("arguments") is { Length: > 0 } args ? args : p.Str("input");
                pending[id] = new(p.Str("name"), input, at, cwd);
                if (p.Str("name").EndsWith("update_plan", StringComparison.Ordinal))
                    trace.MarkStep(agent, TraceJson.Object(input).Field("plan"), at);
                break;
            case "function_call_output":
            case "custom_tool_call_output":
                if (pending.Remove(id, out var tool)) Result(id, tool, p.Field("output").Words(), at);
                break;
            case "message" when p.Str("role") == "user":
                legacyMessages.Add(new(p.Field("content").Words(), at));
                evidence.Message("UserMessage", p.Field("content").Words());
                break;
            case "message" when p.Str("role") == "assistant":
                evidence.Message("AgentMessage", p.Field("content").Words());
                if (p.Str("phase") == "commentary") trace.MarkProgress(agent, p.Field("content").Words(), at);
                break;
        }
    }

    private void Result(string id, PendingTool tool, string output, long at)
    {
        var toolName = tool.Name.Split('.').Last();
        if (hasRuntimeItems && toolName is "exec" or "wait") return;
        var args = TraceJson.Object(tool.Input);
        var command = args.Str("cmd") is { Length: > 0 } cmd ? cmd : args.Str("command");
        var location = args.Str("workdir") is { Length: > 0 } dir ? dir : tool.Directory;
        var envelope = TraceJson.Object(output);
        var (captured, exit) = CodexCommandResult.Read(output, envelope);
        var shell = args.Str("shell") is { Length: > 0 } explicitShell ? explicitShell : location.StartsWith('/') ? "sh" : "pwsh";
        TraceFile[] files = !trace.FileCoverageStudy ? [] : toolName switch
        {
            "apply_patch" when exit == 0 || envelope.ValueKind == JsonValueKind.Undefined =>
                ToolEvidence.PatchResult(tool.Input, captured.Length > 0 ? captured : output, location, trace.Project),
            "exec_command" or "shell_command" when exit == 0 => ShellReadEvidence.Read(shell, command, captured, location, trace.Project),
            _ => []
        };
        if (tool.Name.EndsWith("spawn_agent", StringComparison.Ordinal))
        {
            var spawned = TraceJson.Object(output);
            Child(spawned.Str("agent_id"), args.Str("task_name"));
        }
        var failed = exit is not (null or 0) || output.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
            output.StartsWith("apply_patch verification failed", StringComparison.OrdinalIgnoreCase);
        var outcome = failed ? "failed" : exit == 0 ? "exit 0" : "exit unknown";
        var review = !trace.FileCoverageStudy ? null : toolName is "exec_command" or "shell_command" ? CoverageRouting.Shell(shell, command, captured, outcome, location, trace.Project, files)
            : CoverageRouting.Other(tool.Name, tool.Input, output, outcome, location, trace.Project);
        var line = evidence.Line;
        var details = envelope.ValueKind == JsonValueKind.Object && envelope.Field("output").ValueKind == JsonValueKind.Undefined ? envelope : default;
        evidence.Result(line, tool with { Directory = location }, command, new(captured, outcome, failed, exit, details), id);
        raw.Add((id, toolName, TraceRecords.Call(agent, tool with { Directory = location }, at, command, output.Length, files, failed) with { Review = review, Id = line }));
    }

    private void Child(string id, string name)
    {
        if (Guid.TryParse(id, out var child) && child != thread)
            trace.Children.TryAdd(child, string.IsNullOrWhiteSpace(name) ? "Agent " + child.ToString("N")[^8..] : TraceJson.Caption(name));
    }

    private void Item(JsonElement p, long at)
    {
        hasRuntimeItems = true;
        var item = p.Field("item");
        var end = p.Number("completed_at_ms") is > 0 and var complete ? complete : at;
        var start = p.Number("started_at_ms") is > 0 and var began ? Math.Min(began, end) : end;
        var type = item.Str("type");
        if (type is "UserMessage" or "AgentMessage")
        {
            var words = item.Field("content").Words();
            evidence.Message(type, words);
            if (type == "UserMessage") messages.Add(new(words, end));
            else if (item.Str("phase") == "commentary") trace.MarkProgress(agent, words, end);
            return;
        }
        if (type == "SubAgentActivity")
        {
            if (item.Str("kind") is "spawned" or "started") Child(item.Str("agent_thread_id"), item.Str("agent_path"));
            return;
        }
        if (type == "CollabAgentToolCall" && item.Str("tool").Contains("spawn", StringComparison.OrdinalIgnoreCase))
            foreach (var receiver in item.Field("receiver_thread_ids").Items()) Child(receiver.Words(), "");
        if (CodexToolItem.Read(item, agent, cwd, trace, start, end) is not { } call) return;
        trace.Observe(start);
        trace.Observe(end);
        runtimeIds.Add(item.Str("id"));
        structured.Add(call with { Id = evidence.Line });
        evidence.Record(evidence.Line, item, cwd, call.Failed);
    }

    internal void Finish()
    {
        var events = structured.OrderBy(c => c.End).ToArray();
        var ends = events.Select(c => c.End).ToArray();
        trace.Calls.AddRange(events);
        foreach (var (id, tool, call) in raw)
        {
            if (runtimeIds.Contains(id) || hasRuntimeItems && tool is "exec" or "wait") continue;
            if (tool is "exec_command" or "apply_patch" && HasNestedCall(call)) continue;
            trace.Calls.Add(call);
        }
        var prompts = messages.Count > 0 ? messages : userEvents.Count > 0 ? userEvents : legacyMessages;
        foreach (var message in prompts) trace.MarkTask(agent, message.Title, message.At);
        trace.Unfinished += pending.Count;

        bool HasNestedCall(TraceCall call)
        {
            var i = Array.BinarySearch(ends, call.Start);
            if (i < 0) i = ~i;
            return i < ends.Length && ends[i] <= call.End;
        }
    }
}

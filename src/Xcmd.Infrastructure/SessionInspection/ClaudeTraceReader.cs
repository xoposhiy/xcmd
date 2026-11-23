// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class ClaudeTraceReader(TraceRecords trace, string agent, string directory, ClassificationEvidence evidence)
{
    private readonly Dictionary<string, PendingTool> pending = [];
    internal int Unfinished => pending.Count;

    internal void Record(JsonElement record, long at, int line)
    {
        evidence.Line = line;
        var kind = record.Str("type");
        if (kind is not ("assistant" or "user" or "system")) return;
        trace.Observe(at);
        var cwd = record.Str("cwd") is { Length: > 0 } location ? location : directory;
        var content = record.Field("message").Field("content");
        if (kind is "assistant" or "user" && record.Field("isMeta").ValueKind != JsonValueKind.True)
            evidence.Message(kind == "assistant" ? "AgentMessage" : "UserMessage", content.Words());
        if (kind == "user" && record.Field("isMeta").ValueKind != JsonValueKind.True) trace.MarkTask(agent, content.Words(), at);
        if (kind == "assistant" && record.Field("message").Str("stop_reason") != "end_turn")
            trace.MarkProgress(agent, string.Join(" ", content.Items().Where(p => p.Str("type") == "text").Select(p => p.Str("text"))), at);
        foreach (var part in content.Items())
        {
            if (part.Str("type") == "tool_use")
            {
                var input = part.Field("input");
                pending[part.Str("id")] = new(part.Str("name"), input.ToString(), at, cwd);
                trace.MarkStep(agent, input.Field("todos"), at);
            }
            if (part.Str("type") == "tool_result" && pending.Remove(part.Str("tool_use_id"), out var tool))
            {
                var result = content.Items().Count(p => p.Str("type") == "tool_result") == 1 ? record.Field("toolUseResult") : default;
                Result(part, result, tool, at);
            }
        }
    }

    private void Result(JsonElement part, JsonElement result, PendingTool tool, long at)
    {
        var input = TraceJson.Object(tool.Input);
        var content = part.Field("content");
        var files = trace.FileCoverageStudy ? ToolEvidence.ClaudeFiles(input, result, content, tool.Name, tool.Directory, trace.Project) : [];
        var body = result.Str("stdout") is { Length: > 0 } stdout ? stdout + "\n" + result.Str("stderr") : content.Words();
        var failed = part.Field("is_error").ValueKind == JsonValueKind.True;
        var status = failed ? "failed" : "completed";
        var command = input.Str("command");
        CoverageInput? review = null;
        if (trace.FileCoverageStudy && tool.Name is "Bash" or "PowerShell")
            review = CoverageRouting.Shell(tool.Name, command, body, status, tool.Directory, trace.Project, failed ? [] : files);
        else if (trace.FileCoverageStudy && files.Length == 0 && (tool.Name != "Read" || LegacyRead(result, body, failed)))
            review = CoverageRouting.Other(tool.Name, tool.Input, body, status, tool.Directory, trace.Project);
        var id = evidence.Calls.Count + 1;
        var exit = result.Field("exitCode");
        if (exit.ValueKind != JsonValueKind.Number) exit = result.Field("exit_code");
        var outcome = new ToolOutcome(body, status, failed, exit.ValueKind == JsonValueKind.Number && exit.TryGetInt32(out var code) ? code : null, result);
        evidence.Result(id, tool, command, outcome, part.Str("tool_use_id"));
        trace.Calls.Add(TraceRecords.Call(agent, tool, at, command, body.Length, files, failed) with { Review = review, Id = id });
    }

    private static bool LegacyRead(JsonElement result, string body, bool failed) => !failed && result.ValueKind != JsonValueKind.Object &&
        body.Length > 0 && !body.Contains("unchanged since", StringComparison.OrdinalIgnoreCase);
}

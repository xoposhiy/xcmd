// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class ClaudeTraceReader(TraceRecords trace, string agent, string directory, ClassificationEvidence evidence)
{
    private readonly Dictionary<string, PendingTool> pending = [];
    private readonly HashSet<string> tools = [];
    private int toolOrder;
    internal void Finish()
    {
        foreach (var tool in pending.Values) trace.AddPending(agent, tool);
    }

    internal void Record(JsonElement record, long at, int line)
    {
        evidence.Line = line;
        var kind = record.Str("type");
        if (kind is not ("assistant" or "user" or "system")) return;
        var message = record.Field("message");
        trace.Observe(at);
        var cwd = record.Str("cwd") is { Length: > 0 } location ? location : directory;
        var content = message.Field("content");
        var meta = record.Field("isMeta").ValueKind == JsonValueKind.True;
        if (kind == "user" && !meta) User(content, at);
        var response = kind == "assistant" ? Assistant(message, at, line, meta) : null;
        RecordCalls(record.Field("toolUseResult"), content, cwd, at, response);
    }

    private ResponseKey? Assistant(JsonElement message, long at, int line, bool meta)
    {
        var content = message.Field("content");
        if (!meta) evidence.Message("AgentMessage", content.Words());
        if (message.Str("stop_reason") != "end_turn")
            trace.MarkProgress(agent, string.Join(" ", content.Items().Where(p => p.Str("type") == "text").Select(p => p.Str("text"))), at);
        var model = message.Str("model");
        if (model == "<synthetic>") return null;
        if (agent == SessionTrace.MainAgent && model.Length > 0) trace.Model = model;
        return RecordResponse(message, model, at, line);
    }

    private void User(JsonElement content, long at)
    {
        var words = content.Words();
        evidence.Message("UserMessage", words);
        trace.MarkTask(agent, words, at);
    }

    private void RecordCalls(JsonElement result, JsonElement content, string cwd, long at, ResponseKey? response)
    {
        var singleResult = content.Items().Count(part => part.Str("type") == "tool_result") == 1;
        foreach (var part in content.Items())
        {
            if (part.Str("type") == "tool_use" && tools.Add(part.Str("id")))
            {
                var input = part.Field("input");
                pending[part.Str("id")] = new(part.Str("name"), input.ToString(), at, cwd)
                { Response = response, Order = ++toolOrder, RecordType = "Tool use" };
                trace.MarkStep(agent, input.Field("todos"), at);
            }
            if (part.Str("type") == "tool_result" && pending.Remove(part.Str("tool_use_id"), out var tool))
                Result(part, singleResult ? result : default, tool, at);
        }
    }

    private ResponseKey RecordResponse(JsonElement message, string model, long at, int line)
    {
        var id = message.Str("id");
        var key = id.Length > 0 ? new("Claude", id)
            : new ResponseKey("Claude:" + agent, line.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var cost = SessionApiPrices.Price("anthropic", model, ResponseUsageReader.Claude(message.Field("usage")));
        var title = TraceJson.Caption(message.Field("content").Words());
        var caption = title.Length == 0 ? null : title;
        if (!trace.Responses.TryGetValue(key, out var previous))
            trace.Responses.Add(key, new(key, agent, at, caption, cost));
        else
            trace.Responses[key] = previous with { Message = previous.Message ?? caption, Cost = cost.Usage != null ? cost : previous.Cost };
        return key;
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
        trace.Calls.Add(TraceRecords.Call(agent, tool, at, command, body, files, failed)
            with
        { Review = review, Id = id });
    }

    private static bool LegacyRead(JsonElement result, string body, bool failed) => !failed && result.ValueKind != JsonValueKind.Object &&
        body.Length > 0 && !body.Contains("unchanged since", StringComparison.OrdinalIgnoreCase);
}

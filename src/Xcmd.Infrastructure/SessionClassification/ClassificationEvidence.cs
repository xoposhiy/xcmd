// @implements InspectAgentSession
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Xcmd.Infrastructure;

internal sealed record EvidenceLine(string Brief, string Detail);
internal sealed record CallEvidence(int Id, int Line, string Digest, string Tool, bool Failed, EvidenceLine Data);
internal sealed record SessionMessage(int Line, string Speaker, EvidenceLine Data);
internal sealed record ToolOutcome(string Output, string Status, bool Failed, int? Exit, JsonElement Details = default);

internal sealed class ClassificationEvidence(string project, string stream)
{
    internal string Stream { get; } = stream;
    internal int Line { get; set; }
    internal Dictionary<int, CallEvidence> Calls { get; } = [];
    internal List<SessionMessage> Messages { get; } = [];
    private readonly ClassificationText text = new(project);

    internal void Message(string speaker, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var clean = text.Clean(value);
        string Row(int limit) => JsonSerializer.Serialize(new { speaker, text = ClassificationText.Clip(clean, limit) });
        var data = new EvidenceLine(Row(720), Row(ClassificationText.Expanded));
        if (Messages.LastOrDefault() is { } last && last.Speaker == speaker && last.Data == data) return;
        Messages.Add(new(Line, speaker, data));
    }

    internal void Record(int id, JsonElement item, string directory, bool failed)
    {
        var brief = new JsonObject { ["id"] = id };
        var detail = new JsonObject { ["id"] = id };
        void Field(string name, string value, int limit = 720)
        {
            brief[name] = ClassificationText.Clip(value, limit);
            detail[name] = ClassificationText.Clip(value, ClassificationText.Expanded);
        }
        var kind = item.Str("type");
        Field("tool", text.Clean(kind));
        Field("cwd", text.Location(item.Str("cwd") is { Length: > 0 } cwd ? cwd : directory));
        foreach (var key in new[] { "status", "exit_code", "kind", "tool", "url", "query", "action", "arguments", "prompt", "server", "receiver_thread_ids" })
            if (item.Field(key) is { ValueKind: not JsonValueKind.Undefined } value) Field(key == "tool" ? "name" : key, text.Clean(value));
        if (item.Str("path") is { Length: > 0 } path) Field("path", text.Location(path));
        if (item.Field("command") is { ValueKind: not JsonValueKind.Undefined } command)
            Field("command", text.Clean(CodexToolItem.CommandText(command)), 1800);
        if (item.Field("changes") is { ValueKind: JsonValueKind.Object } changes)
        {
            var shortChanges = new JsonArray();
            var longChanges = new JsonArray();
            foreach (var change in changes.EnumerateObject())
            {
                var file = text.Clean(change.Name);
                var move = text.Clean(change.Value.Str("move_path"));
                var diff = text.Clean(change.Value.Str("unified_diff") is { Length: > 0 } patch ? patch : change.Value.Str("content"));
                var type = text.Clean(change.Value.Str("type"));
                JsonArray Change(int pathLimit, int diffLimit) => new(ClassificationText.Clip(file, pathLimit), ClassificationText.Clip(type, pathLimit),
                    ClassificationText.Clip(move, pathLimit), ClassificationText.Clip(diff, diffLimit));
                shortChanges.Add(Change(300, 260));
                longChanges.Add(Change(ClassificationText.Expanded, ClassificationText.Expanded));
            }
            brief["changes"] = shortChanges;
            detail["changes"] = longChanges;
        }
        var result = string.Join(" ", new[] { "aggregated_output", "stdout", "stderr", "result", "results" }
            .Select(key => item.Field(key)).Where(v => v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)).Select(text.Clean));
        if (result.Length > 0) Field("result", result, 600);
        Calls[id] = new(id, Line, ClassificationText.Hash(item.GetRawText()), kind, failed, new(brief.ToJsonString(), detail.ToJsonString()));
    }

    internal void Result(int id, PendingTool tool, string command, ToolOutcome result, string identity)
    {
        var args = TraceJson.Object(tool.Input);
        var item = new JsonObject
        {
            ["type"] = tool.Name,
            ["arguments"] = args.ValueKind == JsonValueKind.Object ? JsonNode.Parse(args.GetRawText()) : tool.Input,
            ["status"] = result.Status,
            ["aggregated_output"] = result.Output,
            ["call_id"] = identity
        };
        if (result.Details.ValueKind != JsonValueKind.Undefined) item["result"] = JsonNode.Parse(result.Details.GetRawText());
        if (result.Exit.HasValue) item["exit_code"] = result.Exit.Value;
        if (command.Length > 0) item["command"] = command;
        Record(id, JsonSerializer.SerializeToElement(item), tool.Directory, result.Failed);
    }
}

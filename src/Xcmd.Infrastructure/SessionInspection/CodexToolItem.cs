// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class CodexToolItem
{
    internal static TraceCall? Read(JsonElement item, string agent, string cwd, TraceRecords trace, long start, long end)
    {
        var type = item.Str("type");
        var name = type;
        if (name is "" or "Reasoning" or "AgentMessage" or "UserMessage" or "SubAgentActivity" or "ContextCompaction") return null;
        if (item.Str("cwd") is { Length: > 0 } working) cwd = working;
        var files = Array.Empty<TraceFile>();
        var input = "";
        var command = "";
        var output = "";
        string? shell = null;
        CoverageInput? review = null;
        switch (name)
        {
            case "CommandExecution":
                output = item.Str("aggregated_output");
                command = CommandText(item.Field("command"));
                shell = item.Field("command").Items().FirstOrDefault().Words();
                if (trace.FileCoverageStudy)
                {
                    (_, files) = ReadCommand(item, output, cwd, trace.Project);
                    review = CoverageRouting.Shell(shell, command, output, CommandStatus(item), cwd, trace.Project, files);
                }
                input = command;
                break;
            case "FileChange":
                var changes = item.Field("changes");
                if (trace.FileCoverageStudy && item.Str("status") == "completed" && changes.ValueKind == JsonValueKind.Object)
                    files = changes.EnumerateObject().SelectMany(change => Changed(change.Name, change.Value, cwd, trace.Project)).ToArray();
                input = changes.ToString();
                name = "File change";
                break;
            case "McpToolCall":
                name = item.Str("tool");
                input = item.Field("arguments").ToString();
                output = item.Field("result").Field("content").Words();
                if (trace.FileCoverageStudy) review = CoverageRouting.Other(name, input, output, item.Str("status"), cwd, trace.Project);
                break;
            case "CollabAgentToolCall":
                name = item.Str("tool") + " agent";
                break;
            case "ImageView":
                name = "View image";
                input = item.Str("path");
                if (trace.FileCoverageStudy) files = ImageReads(item, cwd, trace.Project);
                break;
            default:
                input = item.Field("arguments").ToString();
                break;
        }
        var failed = item.Str("status") is "failed" or "declined" || item.Number("exit_code") != 0;
        var recordType = type switch
        {
            "CommandExecution" => "Command execution",
            "FileChange" => "File change",
            "McpToolCall" => "MCP tool call",
            "CollabAgentToolCall" => "Agent tool call",
            "ImageView" => "Image view",
            _ => type
        };
        var tool = new PendingTool(name, input, start, cwd) { RecordType = recordType, Shell = shell };
        return TraceRecords.Call(agent, tool, end, command, output, files, failed) with
        {
            Review = review,
            ToolName = item.Str("tool")
        };
    }

    private static TraceFile[] ImageReads(JsonElement item, string cwd, string project) =>
        item.Str("status") is "" or "completed" && TranscriptPath.Resolve(item.Str("path"), cwd, project) is { Length: > 0 } path
            ? [new(path, "Read")] : [];

    private static string CommandStatus(JsonElement item) => item.Str("status") is { Length: > 0 } state && state != "completed" ? state :
        item.Field("exit_code").ValueKind == JsonValueKind.Number ? "exit " + item.Field("exit_code") : "exit unknown";

    private static (string Command, TraceFile[] Files) ReadCommand(JsonElement item, string output, string cwd, string project)
    {
        var args = item.Field("command");
        var parts = args.Items().Select(a => a.Words()).ToArray();
        var switchAt = CommandSwitch(parts);
        var command = parts.Length == 0 ? args.Words() : string.Join(" ", parts.Skip(switchAt + 1));
        if (!Succeeded(item)) return (command, []);
        if (switchAt > 0 && switchAt == parts.Length - 2)
            return (command, ShellReadEvidence.Read(parts[0], parts[^1], output, cwd, project));
        if (parts.Length != 2 || Path.GetFileName(parts[0]) != "cat" || parts[1].StartsWith('-')) return (command, []);
        var path = TranscriptPath.Resolve(parts[1], cwd, project);
        return (command, path.Length > 0 ? [new(path, "Read")] : []);
    }

    internal static string CommandText(JsonElement args)
    {
        var parts = args.Items().Select(a => a.Words()).ToArray();
        return parts.Length == 0 ? args.Words() : string.Join(" ", parts.Skip(CommandSwitch(parts) + 1));
    }

    private static int CommandSwitch(string[] parts) => Array.FindIndex(parts, x => x.ToLowerInvariant() is "-command" or "-c" or "-lc" or "/c");

    private static bool Succeeded(JsonElement item) => item.Field("exit_code") is { ValueKind: JsonValueKind.Number } code &&
        code.TryGetInt32(out var exit) && exit == 0 && item.Str("status") is "" or "completed";

    private static IEnumerable<TraceFile> Changed(string name, JsonElement change, string cwd, string project)
    {
        var path = TranscriptPath.Resolve(name, cwd, project);
        if (path.Length == 0) yield break;
        if (change.Str("move_path") is { Length: > 0 } move)
        {
            if (TranscriptPath.Resolve(move, cwd, project) is { Length: > 0 } target)
            {
                yield return new(path, "Moved from");
                yield return new(target, "Moved to");
            }
            yield break;
        }
        var action = change.Str("type") switch { "add" => "Created", "delete" => "Deleted", "update" => "Edited", _ => "" };
        if (action.Length > 0) yield return new(path, action);
    }
}

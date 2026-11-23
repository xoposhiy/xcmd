// @implements InspectAgentSession
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static class ToolEvidence
{
    internal static TraceFile[] PatchResult(string input, string output, string cwd, string project)
    {
        var lines = output.Trim().ReplaceLineEndings("\n").Split('\n');
        if (lines[0] != "Success. Updated the following files:") return [];
        var requested = input.ReplaceLineEndings("\n").Split('\n').ToHashSet(StringComparer.Ordinal);
        var files = new List<TraceFile>();
        foreach (var row in lines.Skip(1))
        {
            if (row.Length < 3 || row[1] != ' ') return [];
            var action = row[0] switch { 'A' => "Created", 'M' => "Edited", 'D' => "Deleted", _ => "" };
            var path = row[2..];
            var header = row[0] switch { 'A' => "*** Add File: ", 'M' => "*** Update File: ", 'D' => "*** Delete File: ", _ => "" };
            if (action.Length == 0 || !requested.Contains(header + path)) return [];
            var resolved = TranscriptPath.Resolve(path, cwd, project);
            if (resolved.Length > 0) files.Add(new(resolved, action));
        }
        return files.Distinct().ToArray();
    }

    internal static TraceFile[] ClaudeFiles(JsonElement input, JsonElement result, JsonElement content, string name, string cwd, string project)
    {
        if (name is "Bash" or "PowerShell")
            return ShellReadEvidence.Read(name == "Bash" ? "bash" : "pwsh", input.Str("command"), result.Str("stdout") is { Length: > 0 } stdout ? stdout : content.Words(), cwd, project);
        var path = TranscriptPath.Resolve(input.Str(name == "NotebookEdit" ? "notebook_path" : "file_path"), cwd, project);
        if (path.Length == 0) return [];
        var file = result.Field("file");
        var returned = file.Str("filePath") is { Length: > 0 } nested ? nested : result.Str("filePath");
        var comparison = TranscriptPath.IsUnixRoot(cwd) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (returned.Length > 0 && !path.Equals(TranscriptPath.Resolve(returned, cwd, project), comparison)) return [];
        if (name != "Read" && returned.Length == 0 && content.Words().Length == 0) return [];
        return name switch
        {
            "Read" => ClaudeRead(path, result, file, content),
            "Write" => [new(path, result.Str("type") == "create" ? "Created" : "Written")],
            "Edit" or "MultiEdit" or "NotebookEdit" => [new(path, "Edited")],
            _ => []
        };
    }

    private static TraceFile[] ClaudeRead(string path, JsonElement result, JsonElement file, JsonElement content)
    {
        if (result.Str("type") == "file_unchanged" || content.Words().Contains("unchanged since", StringComparison.OrdinalIgnoreCase)) return [];
        if (file.ValueKind != JsonValueKind.Object && result.Str("type") is not ("image" or "pdf" or "notebook") &&
            !content.Items().Any(c => c.Str("type") == "image")) return [];
        var first = PositiveInt(file, "startLine"); var count = PositiveInt(file, "numLines"); var total = PositiveInt(file, "totalLines");
        if (first < 1 || count < 1 || total > 0 && (long)first + count - 1 > total) return [new(path, "Read")];
        return [new(path, "Read", first, count, total)];
    }

    private static int PositiveInt(JsonElement element, string key) => (int)Math.Clamp(element.Number(key), 0, int.MaxValue);
}

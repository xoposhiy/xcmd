// @implements InspectAgentSession
using System.Text.RegularExpressions;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal static partial class CoverageRouting
{
    // These commands report metadata, search hits or changes. Match only stand-alone invocations.
    [GeneratedRegex("""^\s*(?:rg(?:\.exe)?(?!.*\s--pre)|grep|ls|dir|pwd|echo|printf|write-output|get-childitem|get-item|get-location|test-path|select-object|sort-object|set-content|add-content|git\s+(?:status|diff|log|ls-files|rev-parse|worktree\s+list)|dotnet\s+(?:test|build)|point-taken\s+(?:list|show|start|close))(?=\s|$)""", RegexOptions.IgnoreCase)]
    private static partial Regex NonRead();

    [GeneratedRegex("""(?:'[^'\r\n]*'|"[^"$%`\r\n]*")""")]
    private static partial Regex Quoted();

    internal static CoverageInput? Shell(string tool, string command, string output, string status, string cwd, string project, TraceFile[] files)
    {
        if (string.IsNullOrWhiteSpace(command) || status is "declined" or "cancelled" or "interrupted") return null;
        var syntax = Quoted().Replace(command.Trim(), "literal");
        var shell = Path.GetFileNameWithoutExtension(tool).ToLowerInvariant();
        if (shell is "sh" or "bash" or "zsh" && Regex.IsMatch(syntax, @"^type\s+[\w./ -]+$")) return null;
        if (files.Length > 0 && (!syntax.Contains("&&", StringComparison.Ordinal) ||
            command.Split("&&").All(part => KnownPart(part, tool, output, cwd, project, files)))) return null;
        if (status is "failed" or "exit unknown" || status.StartsWith("exit ", StringComparison.Ordinal) && status != "exit 0")
        {
            if (Regex.IsMatch(syntax, @"^\s*(?:cat|type|gc|get-content)\s+(?:-(?:literalpath|path)\s+)?[\w./:\\-]+$", RegexOptions.IgnoreCase)) return null;
        }
        if (syntax.IndexOfAny(['$', '`', '\n', '\r', '<', '>', '(', ')', '{', '}', '"', '\'', '%']) < 0 &&
            syntax.Split([';', '|', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).All(s => NonRead().IsMatch(s))) return null;
        return Input(tool, command, output, status, cwd, project);
    }

    private static bool KnownPart(string command, string tool, string output, string cwd, string project, TraceFile[] files)
    {
        if (NonRead().IsMatch(command)) return true;
        var reads = ShellReadEvidence.Read(tool, command, output, cwd, project);
        return reads.Length > 0 && reads.All(read => files.Any(f => f.Path == read.Path));
    }

    internal static CoverageInput? Other(string tool, string input, string output, string status, string cwd, string project)
    {
        var name = tool.Split('.').Last();
        if (name is "apply_patch" or "Edit" or "MultiEdit" or "Write" or "NotebookEdit" or "Grep" or "Glob" or "TodoWrite"
            or "update_plan" or "spawn_agent" or "wait" or "wait_agent" or "send_message" or "list_agents" or "update_goal"
            or "get_goal" or "create_goal" or "WebSearch" or "WebFetch" or "Skill" or "AskUserQuestion") return null;
        if (string.IsNullOrWhiteSpace(input) || status is "declined" or "cancelled") return null;
        return Input(tool, input, output, status, cwd, project);
    }

    private static CoverageInput Input(string tool, string command, string output, string status, string cwd, string project) =>
        new(tool, CoverageExcerpt.Text(cwd, 1500), CoverageExcerpt.Text(project, 1500), CoverageExcerpt.Command(command),
            CoverageExcerpt.Text(output, 6000), status);
}

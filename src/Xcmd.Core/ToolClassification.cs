// @implements InspectAgentSession, OpenViewers
namespace Xcmd.Core;

public sealed record ToolLabel(string Target, string Action)
{
    public static ToolLabel Unclassified { get; } = new("", "");
    public bool IsValid => Target is "build_release" or "agent_instructions" or "tests" or "source" or "git" or "docs" or "tracker" or "project" or "unknown"
        && Action is "read" or "write" or "exec" or "other";
    public override string ToString() => this == Unclassified ? "Unclassified" : Target + "/" + Action;
}

public sealed record ToolOperation(ToolLabel Label, string[] Objects);
public sealed record ToolResource(string Kind, string Value)
{
    public bool IsValid => Kind is "file" or "directory" or "url" or "issue" or "repository" or "other"
        && !string.IsNullOrWhiteSpace(Value);
}
public sealed record ToolClassification(string Summary, ToolOperation[] Pairs, ToolResource[] Resources);

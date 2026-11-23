// @implements RestoreWorkspace, ReleaseHandoff, OpenViewers, InspectAgentSession
namespace Xcmd.Core;

public sealed record InspectorHandoff(AgentSession Session, WindowPlacement Window, InspectorRange? Range);
public sealed record InspectorRange(long From, long To, long ZoomFrom, long ZoomTo, bool Zoomed, string? Agent, bool Tokens);

public sealed record ViewerHandoff(string[] Paths, int Index, string WorkingDirectory, ViewerDocument? Document,
    WindowPlacement Window, ViewerReadingState Reading);

public sealed record ViewerReadingState(FileViewMode? Mode, long FirstByte, double ScrollX, int CodePage,
    bool Wrap, bool Diff, bool Follow, bool InformationVisible)
{
    public int? DiffPosition { get; init; }
}

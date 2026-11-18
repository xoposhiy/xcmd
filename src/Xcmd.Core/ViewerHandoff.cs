// @implements RestoreWorkspace, ReleaseHandoff, OpenViewers
namespace Xcmd.Core;

public sealed record ViewerHandoff(string[] Paths, int Index, string WorkingDirectory, ViewerDocument? Document,
    WindowPlacement Window, ViewerReadingState Reading);

public sealed record ViewerReadingState(FileViewMode? Mode, long FirstByte, double ScrollX, int CodePage,
    bool Wrap, bool Diff, bool Follow, bool InformationVisible)
{
    public int? DiffPosition { get; init; }
}

// @implements InspectAgentSession
namespace Xcmd.Core;

public sealed record TimelineMark(TraceCall Call, bool Edit, bool Run);
public sealed record TimelineCategory(string Target, string[] Actions, TimelineMark[] Marks);

public sealed class SessionCallBlock
{
    public IReadOnlyList<TraceCall> Calls { get; }
    public IReadOnlyList<TimelineCategory> Categories { get; }

    private SessionCallBlock(List<TraceCall> calls)
    {
        Calls = calls;
        Categories = calls.SelectMany(call => call.Classification?.Pairs.Select(pair => (call, pair.Label)) ?? [])
            .GroupBy(entry => entry.Label.Target)
            .Select(group => new TimelineCategory(group.Key, group.Select(entry => entry.Label.Action).Distinct().ToArray(),
                group.Where(entry => entry.Label.Action is "write" or "exec").GroupBy(entry => entry.call)
                    .Select(actions => new TimelineMark(actions.Key, actions.Any(a => a.Label.Action == "write"),
                        actions.Any(a => a.Label.Action == "exec"))).ToArray())).ToArray();
    }

    public static SessionCallBlock For(TraceCall call) => new([call]);
}

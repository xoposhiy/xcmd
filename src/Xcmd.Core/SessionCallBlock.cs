// @implements InspectAgentSession
namespace Xcmd.Core;

public sealed record TimelineMark(TraceCall Call, bool Edit, bool Run);
public sealed record TimelineCategory(string Target, string[] Actions, string[] Files, string[] Repositories, TimelineMark[] Marks);

public sealed class SessionCallBlock
{
    public IReadOnlyList<TraceCall> Calls { get; }
    public IReadOnlyList<TimelineCategory> Categories { get; }

    private SessionCallBlock(SessionTrace trace, List<TraceCall> calls)
    {
        Calls = calls;
        var resources = calls.SelectMany(call => ResourcesByTarget(trace, call))
            .ToLookup(entry => (entry.Target, entry.Resource.Kind), entry => entry.Resource.Value);
        Categories = calls.SelectMany(call => call.Classification?.Pairs.Select(pair => (call, pair.Label)) ?? [])
            .GroupBy(entry => entry.Label.Target)
            .Select(group => new TimelineCategory(group.Key, group.Select(entry => entry.Label.Action).Distinct().ToArray(),
                resources[(group.Key, "file")].Select(path => path.Replace('\\', '/')).DistinctBy(trace.FileKey).ToArray(),
                resources[(group.Key, "repository")].Distinct().ToArray(),
                group.Where(entry => entry.Label.Action is "write" or "exec").GroupBy(entry => entry.call)
                    .Select(actions => new TimelineMark(actions.Key, actions.Any(a => a.Label.Action == "write"),
                        actions.Any(a => a.Label.Action == "exec"))).ToArray())).ToArray();
    }

    private static IEnumerable<(string Target, ToolResource Resource)> ResourcesByTarget(SessionTrace trace, TraceCall call)
    {
        if (call.Classification is not { } classification) yield break;
        foreach (var resource in classification.Resources.Where(resource => resource.Kind is "file" or "repository"))
            foreach (var label in SessionResourceActivity.Labels(trace, resource, classification))
                yield return (label.Target, resource);
    }

    public static SessionCallBlock For(SessionTrace trace, TraceCall call) => new(trace, [call]);
}

// @implements InspectAgentSession
namespace Xcmd.Core;

public sealed record ResourceInterval(TraceCall Call, ToolLabel Label);

public sealed record SessionResourceActivity(ResourceInterval[] Intervals, string[] Targets)
{
    public static SessionResourceActivity For(SessionTrace trace, TouchedResource row, IReadOnlySet<string> targets, IReadOnlySet<string> actions)
    {
        if (targets.Count == 0 || actions.Count == 0) return new([], []);
        var intervals = row.Calls.SelectMany(call => Labels(trace, row.Resource, call.Classification!)
            .Where(label => targets.Contains(label.Target) && (actions.Contains(label.Action) || label.Action == "other"))
            .Select(label => new ResourceInterval(call, label))).ToArray();
        var matchingTargets = intervals.GroupBy(i => i.Label.Target).OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key).ToArray();
        return new(intervals, matchingTargets);
    }

    public static IEnumerable<ToolLabel> Labels(SessionTrace trace, ToolResource resource, ToolClassification classification)
    {
        var pairs = classification.Pairs;
        if (pairs.Length == 1) return [pairs[0].Label];
        var isPath = resource.Kind is "file" or "directory";
        string Key(string value) => isPath ? trace.FileKey(value.Replace('\\', '/')) : value;
        var key = Key(resource.Value);
        var keys = new HashSet<string> { key };
        if (isPath)
        {
            var name = key.Split('/')[^1];
            if (name != key && pairs.Any(p => p.Objects.Any(o => Key(o) == name)) &&
                !classification.Resources.Where(r => r.Kind is "file" or "directory").Select(r => Key(r.Value))
                    .Any(path => path != key && path.Split('/')[^1] == name))
                keys.Add(name);
        }
        var matches = pairs.Where(p => p.Objects.Any(o => keys.Contains(Key(o)))).ToArray();
        if (matches.Length > 0) return matches.Select(p => p.Label);
        // Without a matching object, only labels shared by all operations are certain.
        var targets = pairs.Select(p => p.Label.Target).Distinct().ToArray();
        var actions = pairs.Select(p => p.Label.Action).Distinct().ToArray();
        return [new(targets.Length == 1 ? targets[0] : "unknown", actions.Length == 1 ? actions[0] : "other")];
    }
}

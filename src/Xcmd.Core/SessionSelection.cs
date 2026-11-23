// @implements InspectAgentSession, OpenViewers
namespace Xcmd.Core;

public sealed record ActivityTotal(ToolLabel Activity, long Milliseconds, long Tokens, int Calls);

public sealed class SessionSelection
{
    public SessionTrace Trace { get; }
    public long From { get; private set; }
    public long To { get; private set; }
    public string? Agent { get; set; }
    public long ZoomFrom { get; private set; }
    public long ZoomTo { get; private set; }
    public bool Zoomed => ZoomFrom != Trace.Start || ZoomTo != Trace.End;

    public SessionSelection(SessionTrace trace)
    {
        Trace = trace;
        ZoomFrom = From = trace.Start;
        ZoomTo = To = trace.End;
    }

    public void Select(long from, long to)
    {
        From = Math.Clamp(Math.Min(from, to), Trace.Start, Trace.End);
        To = Math.Clamp(Math.Max(from, to), From, Trace.End);
        if (From < ZoomFrom || To > ZoomTo) ResetZoom();
    }

    public void ToggleZoom()
    {
        if (Zoomed) ResetZoom();
        else if (To > From) { ZoomFrom = From; ZoomTo = To; }
    }

    private void ResetZoom() { ZoomFrom = Trace.Start; ZoomTo = Trace.End; }

    public bool Includes(long end) => (end > From || From == Trace.Start && end == From) && end <= To;
    public bool InScope(TraceCall call) => Agent == null || call.Agent == Agent;

    public SessionSummary Summarize()
    {
        var calls = Trace.Calls.Where(InScope).Where(c => c.End >= From && c.Start <= To).ToArray();
        var results = calls.Where(c => Includes(c.End)).ToArray();
        long Duration(TraceCall c) => Math.Max(0, Math.Min(c.End, To) - Math.Max(c.Start, From));
        var activities = calls.SelectMany(c => c.Labels.Select(label => (label, c))).GroupBy(x => x.label, x => x.c)
            .Select(group => new ActivityTotal(group.Key, group.Sum(Duration),
            group.Where(c => Includes(c.End)).Sum(c => c.EstimatedTokens), group.Count(c => Includes(c.End))))
            .Where(row => row.Calls > 0 || row.Milliseconds > 0)
            .OrderByDescending(row => row.Tokens).ThenByDescending(row => row.Milliseconds).ToArray();
        var busy = 0L;
        var last = From;
        foreach (var call in calls.OrderBy(c => c.Start))
        {
            var end = Math.Min(call.End, To);
            busy += Math.Max(0, end - Math.Max(last, call.Start));
            last = Math.Max(last, end);
        }
        var resources = results.SelectMany(call => (call.Classification?.Resources ?? [])
                .DistinctBy(ResourceKey).Select(resource => (resource, call)))
            .GroupBy(entry => ResourceKey(entry.resource))
            .Select(group => new TouchedResource(group.First().resource, group.Select(entry => entry.call).ToArray()))
            .OrderByDescending(row => row.Calls.Count).ThenBy(row => row.Resource.Kind, StringComparer.Ordinal)
            .ThenBy(row => row.Resource.Value, StringComparer.Ordinal).ToArray();
        return new(activities, resources, results, To - From - busy, calls.Sum(Duration));
    }

    private ToolResource ResourceKey(ToolResource resource) => resource.Kind is "file" or "directory"
        ? resource with { Value = Trace.FileKey(resource.Value.Replace('\\', '/')) } : resource;
}

public sealed record TouchedResource(ToolResource Resource, IReadOnlyList<TraceCall> Calls);

public sealed record SessionSummary(IReadOnlyList<ActivityTotal> Activities, IReadOnlyList<TouchedResource> Resources,
    IReadOnlyList<TraceCall> Results, long OutsideTools, long ToolTime);

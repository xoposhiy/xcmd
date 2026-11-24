// @implements InspectAgentSession, OpenViewers
namespace Xcmd.Core;

public sealed record ActivityTotal(string Target, string? Action, long Milliseconds, decimal Cost, int Calls)
{
    public string Name => Target.Length == 0 ? "Unclassified" : Action == null || Action.Length == 0 ? Target : Target + "/" + Action;
}

public sealed class SessionSelection
{
    public SessionTrace Trace { get; }
    public long From { get; private set; }
    public long To { get; private set; }
    public string? Agent { get; set; }
    public long ZoomFrom { get; private set; }
    public long ZoomTo { get; private set; }
    public bool CostAxis { get; private set; }
    public int FirstResponse { get; private set; }
    public int LastResponse { get; private set; }
    public int ZoomFirstResponse { get; private set; }
    public int ZoomLastResponse { get; private set; }
    public bool FullRange => CostAxis ? FirstResponse == 0 && LastResponse == Trace.Responses.Count : From == Trace.Start && To == Trace.End;
    public bool Zoomed => CostAxis ? ZoomFirstResponse != 0 || ZoomLastResponse != Trace.Responses.Count
        : ZoomFrom != Trace.Start || ZoomTo != Trace.End;

    public SessionSelection(SessionTrace trace)
    {
        Trace = trace;
        ZoomFrom = From = trace.Start;
        ZoomTo = To = trace.End;
        ZoomLastResponse = LastResponse = trace.Responses.Count;
    }

    public void SetAxis(bool cost)
    {
        if (CostAxis == cost) return;
        var wasZoomed = Zoomed;
        var zoomFrom = ZoomFrom;
        var zoomTo = ZoomTo;
        var from = From;
        var to = To;
        CostAxis = cost;
        ResetZoom();
        if (wasZoomed)
        {
            Select(zoomFrom, zoomTo);
            ToggleZoom();
        }
        Select(from, to);
    }

    public void Select(long from, long to)
    {
        if (CostAxis)
        {
            SelectResponses(Boundary(Math.Min(from, to)), Trace.BoundaryAfter(Math.Max(from, to)));
            return;
        }
        From = Math.Clamp(Math.Min(from, to), Trace.Start, Trace.End);
        To = Math.Clamp(Math.Max(from, to), From, Trace.End);
        if (From < ZoomFrom || To > ZoomTo) ResetZoom();
    }

    private int Boundary(long at) => at <= Trace.Start ? 0 : Trace.BoundaryAfter(at);

    public void SelectResponses(int first, int last)
    {
        FirstResponse = Math.Clamp(Math.Min(first, last), 0, Trace.Responses.Count);
        LastResponse = Math.Clamp(Math.Max(first, last), FirstResponse, Trace.Responses.Count);
        From = Trace.BoundaryTime(FirstResponse);
        To = Trace.BoundaryTime(LastResponse);
        if (FirstResponse < ZoomFirstResponse || LastResponse > ZoomLastResponse) ResetZoom();
    }

    public void ToggleZoom()
    {
        if (Zoomed) ResetZoom();
        else if (CostAxis && FirstResponse < LastResponse)
        {
            ZoomFirstResponse = FirstResponse;
            ZoomLastResponse = LastResponse;
            ZoomFrom = From;
            ZoomTo = To;
        }
        else if (To > From) { ZoomFrom = From; ZoomTo = To; }
    }

    private void ResetZoom()
    {
        ZoomFrom = Trace.Start;
        ZoomTo = Trace.End;
        ZoomFirstResponse = 0;
        ZoomLastResponse = Trace.Responses.Count;
    }

    public bool Includes(long end) => (end > From || From == Trace.Start && end == From) && end <= To;
    public bool InScope(TraceCall call) => Agent == null || call.Agent == Agent;

    public SessionSummary Summarize(bool groupActions = false)
    {
        var responses = (CostAxis ? Trace.Responses.Skip(FirstResponse).Take(LastResponse - FirstResponse)
            : Trace.Responses.Where(response => Includes(response.At))).Where(response => Agent == null || response.Agent == Agent).ToArray();
        TraceCall[] calls, results;
        if (CostAxis)
        {
            calls = responses.SelectMany(response => response.Calls).Where(InScope).ToArray();
            results = calls.Where(call => call.Completed).ToArray();
        }
        else
        {
            calls = Trace.Calls.Where(InScope).Where(c => c.End >= From && c.Start <= To).ToArray();
            results = calls.Where(c => Includes(c.End)).ToArray();
        }
        long Duration(TraceCall c) => CostAxis ? c.End - c.Start : Math.Max(0, Math.Min(c.End, To) - Math.Max(c.Start, From));
        var completed = results.ToHashSet();
        var activities = calls.SelectMany(c => ActivityShares(c, groupActions, completed.Contains(c), Duration(c)))
            .GroupBy(row => (row.Target, row.Action)).Select(group => new ActivityTotal(group.Key.Target, group.Key.Action,
                group.Sum(row => row.Milliseconds), group.Sum(row => row.Cost), group.Sum(row => row.Calls)))
            .Where(row => row.Calls > 0 || row.Milliseconds > 0 || row.Cost > 0).ToArray();
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
        return new(activities, resources, results, Math.Max(0, To - From - busy), calls.Sum(Duration),
            responses.Sum(response => response.Cost.Usd ?? 0), responses.Count(response => response.Cost.Usd == null));
    }

    private IEnumerable<ActivityTotal> ActivityShares(TraceCall call, bool groupActions, bool complete, long duration)
    {
        var response = Trace.ResponseFor(call);
        var cost = (complete || CostAxis) && response?.Cost.Usd is { } usd ? usd / response.Calls.Count : 0;
        var targets = call.Labels.Distinct().ToLookup(label => label.Target);
        foreach (var target in targets)
        {
            var share = cost / targets.Count;
            if (groupActions) yield return new(target.Key, null, duration, share, complete ? 1 : 0);
            else
            {
                share /= target.Count();
                foreach (var label in target) yield return new(target.Key, label.Action, duration, share, complete ? 1 : 0);
            }
        }
    }

    private ToolResource ResourceKey(ToolResource resource) => resource.Kind is "file" or "directory"
        ? resource with { Value = Trace.FileKey(resource.Value.Replace('\\', '/')) } : resource;
}

public sealed record TouchedResource(ToolResource Resource, IReadOnlyList<TraceCall> Calls);

public sealed record SessionSummary(IReadOnlyList<ActivityTotal> Activities, IReadOnlyList<TouchedResource> Resources,
    IReadOnlyList<TraceCall> Results, long OutsideTools, long ToolTime, decimal Cost, int MissingCosts);

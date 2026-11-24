// @implements InspectAgentSession, OpenViewers
namespace Xcmd.Core;

public sealed record TraceFile(string Path, string Action, int FirstLine = 0, int Lines = 0, int TotalLines = 0)
{
    public string? ModelEvidence { get; init; }
}
public sealed record TraceCall(string Agent, string Title, long Start, long End,
    IReadOnlyList<TraceFile> Files, bool Failed = false, string? Evidence = null)
{
    public int Id { get; init; }
    public string? Input { get; init; }
    public string? Output { get; init; }
    public string? JavaScript { get; init; }
    public string? ToolName { get; init; }
    public string? RecordType { get; init; }
    public string? Shell { get; init; }
    public string? WorkingDirectory { get; init; }
    public ResponseKey? Response { get; init; }
    public int EmissionOrder { get; init; }
    public bool Completed { get; init; } = true;
    public ToolClassification? Classification { get; init; }
    public bool IsStandaloneJavaScript => JavaScript != null && JavaScript == Input;
    public string DisplayTitle => IsStandaloneJavaScript ? Title : Classification?.Summary ?? Title;
    public IEnumerable<ToolLabel> Labels => Classification?.Pairs.Select(p => p.Label) ?? [ToolLabel.Unclassified];
    public string ActivityName => string.Join(", ", Labels);
    public CoverageInput? Review { get; init; }
    public bool ReviewUnresolved { get; init; }
}
public sealed record TraceSpan(string Title, long Start, long End);
public sealed record TraceMarker(string Title, long At);

public sealed class SessionTrace
{
    public const string MainAgent = "Main agent";
    public long Start { get; }
    public long End { get; }
    public IReadOnlyList<TraceCall> Calls { get; }
    public IReadOnlyList<string> Agents { get; }
    public IReadOnlyList<TraceSpan> Tasks { get; }
    public IReadOnlyList<TraceSpan> Phases { get; }
    public string? Note { get; init; }
    public string? ClassificationError { get; init; }
    public bool HasOutdatedClassifications { get; init; }
    public string? Model { get; init; }
    private readonly bool caseSensitivePaths;
    public string FileKey(string path)
    {
        var unix = path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal);
        var windows = path.StartsWith("//", StringComparison.Ordinal) || path.Length > 1 && path[1] == ':';
        return unix || caseSensitivePaths && !windows ? path : path.ToUpperInvariant();
    }
    public bool SameFile(string first, string second) => FileKey(first) == FileKey(second);
    public IReadOnlyList<SessionResponse> Responses { get; }
    public SessionCostAxis CostAxis { get; }
    public decimal TotalCost => CostAxis.At(Responses.Count).Usd;
    private readonly IReadOnlyDictionary<ResponseKey, SessionResponse> responsesByKey;
    public SessionResponse? ResponseFor(TraceCall call) => call.Response is { } key ? responsesByKey.GetValueOrDefault(key) : null;

    public SessionTrace(long start, long end, IEnumerable<TraceCall> calls, IEnumerable<TraceMarker> tasks,
        IEnumerable<TraceMarker> steps, bool caseSensitivePaths = false,
        IEnumerable<ResponseRecord>? responses = null)
    {
        Start = start;
        End = Math.Max(start, end);
        this.caseSensitivePaths = caseSensitivePaths;
        var allCalls = calls.Select((call, index) => call.Response == null
            ? call with { Response = new("", index.ToString(System.Globalization.CultureInfo.InvariantCulture)) } : call)
            .OrderBy(call => call.End).ToArray();
        Calls = allCalls.Where(call => call.Completed).ToArray();
        Tasks = Spans(tasks);
        var planned = steps.ToArray();
        Phases = Spans(planned.Length > 0 ? planned : ActivityRuns());
        var byResponse = allCalls.ToLookup(call => call.Response!.Value);
        var recorded = (responses ?? []).ToDictionary(response => response.Key);
        foreach (var group in byResponse)
        {
            var first = group.MinBy(call => call.Start)!;
            recorded.TryAdd(group.Key, new(group.Key, first.Agent, first.Start, null, ResponseCost.Missing()));
        }
        var task = -1;
        Responses = recorded.Values.OrderBy(response => response.At).Select((response, index) =>
        {
            while (task + 1 < Tasks.Count && Tasks[task + 1].Start <= response.At) task++;
            return new SessionResponse(index, response, byResponse[response.Key].OrderBy(call => call.EmissionOrder).ThenBy(call => call.Start).ToArray(), task);
        }).ToArray();
        responsesByKey = Responses.ToDictionary(response => response.Record.Key);
        CostAxis = new(Responses);
        Agents = Calls.Select(call => call.Agent).Concat(Responses.Select(response => response.Agent)).Prepend(MainAgent).Distinct().ToArray();
    }

    private IEnumerable<TraceMarker> ActivityRuns()
    {
        string? previous = null;
        foreach (var call in Calls.Where(call => call.Agent == MainAgent).OrderBy(call => call.Start))
        {
            var title = call.ActivityName;
            if (title != previous) yield return new(title, call.Start);
            previous = title;
        }
    }

    private TraceSpan[] Spans(IEnumerable<TraceMarker> markers)
    {
        var rows = markers.OrderBy(marker => marker.At).DistinctBy(marker => marker.At).ToArray();
        return rows.Select((row, i) => new TraceSpan(row.Title, row.At, i + 1 < rows.Length ? rows[i + 1].At : End)).ToArray();
    }

    public int BoundaryAfter(long at, bool inclusive = true)
    {
        var first = 0;
        var last = Responses.Count;
        while (first < last)
        {
            var middle = first + (last - first) / 2;
            if (Responses[middle].At < at || inclusive && Responses[middle].At == at) first = middle + 1;
            else last = middle;
        }
        return first;
    }

    public long BoundaryTime(int boundary) => boundary == 0 ? Start : boundary == Responses.Count ? End : Responses[boundary - 1].At;
}

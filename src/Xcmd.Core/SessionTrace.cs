// @implements InspectAgentSession, OpenViewers
namespace Xcmd.Core;

public sealed record TraceFile(string Path, string Action, int FirstLine = 0, int Lines = 0, int TotalLines = 0)
{
    public string? ModelEvidence { get; init; }
}
public sealed record TraceCall(string Agent, string Title, long Start, long End,
    long EstimatedTokens, IReadOnlyList<TraceFile> Files, bool Failed = false, string? Evidence = null)
{
    public int Id { get; init; }
    public ToolClassification? Classification { get; init; }
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
    public string? Note { get; }
    public string? ClassificationError { get; init; }
    private readonly bool caseSensitivePaths;
    public string FileKey(string path)
    {
        var unix = path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal);
        var windows = path.StartsWith("//", StringComparison.Ordinal) || path.Length > 1 && path[1] == ':';
        return unix || caseSensitivePaths && !windows ? path : path.ToUpperInvariant();
    }
    public bool SameFile(string first, string second) => FileKey(first) == FileKey(second);
    private readonly long[] times;
    private readonly long[] tokens;
    public long TotalTokens => tokens[^1];

    public SessionTrace(long start, long end, IEnumerable<TraceCall> calls, IEnumerable<TraceMarker> tasks,
        IEnumerable<TraceMarker> steps, string? note = null, bool caseSensitivePaths = false)
    {
        Start = start;
        End = Math.Max(start, end);
        Note = note;
        this.caseSensitivePaths = caseSensitivePaths;
        Calls = calls.OrderBy(call => call.End).ToArray();
        Agents = Calls.Select(call => call.Agent).Prepend(MainAgent).Distinct().ToArray();
        Tasks = Spans(tasks);
        var planned = steps.ToArray();
        Phases = Spans(planned.Length > 0 ? planned : ActivityRuns());
        var completions = Calls.GroupBy(call => call.End).ToArray();
        times = new long[completions.Length + 1];
        tokens = new long[times.Length];
        times[0] = Start;
        for (var i = 0; i < completions.Length; i++)
        {
            times[i + 1] = completions[i].Key;
            tokens[i + 1] = tokens[i] + completions[i].Sum(call => call.EstimatedTokens);
        }
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

    public long TokensAt(long at)
    {
        var i = Array.BinarySearch(times, at);
        if (i < 0) i = ~i - 1;
        if (i == 0 && times.Length > 1 && times[1] == at) i++;
        return tokens[Math.Max(0, i)];
    }

    public long TimeAtTokens(double value)
    {
        if (value <= 0) return Start;
        if (value >= TotalTokens) return End;
        var i = Array.BinarySearch(tokens, (long)Math.Ceiling(value));
        if (i < 0) i = ~i;
        return value - tokens[i - 1] < tokens[i] - value ? times[i - 1] : times[i];
    }

    public long StepResult(long at, int step, long min, long max)
    {
        var i = Array.BinarySearch(times, at);
        if (i < 0) i = step > 0 ? ~i - 1 : ~i;
        if (step > 0 && i == 0 && times.Length > 1 && times[1] == at) i++;
        var next = i + step;
        return next < 0 ? min : next >= times.Length ? max : Math.Clamp(times[next], min, max);
    }

}

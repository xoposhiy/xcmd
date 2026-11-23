// @implements InspectAgentSession
namespace Xcmd.Core;

public sealed record CoverageInput(string Tool, string Directory, string Project, string Command, string Result, string Status);
public sealed record CoverageAnswer(TraceFile[] Reads, bool Unresolved);

public interface ICoverageReviewer
{
    Task<CoverageAnswer> ReviewAsync(CoverageInput input, CancellationToken token);
}

public sealed class CoverageReview
{
    private readonly Dictionary<TraceCall, CoverageAnswer> answers = new(ReferenceEqualityComparer.Instance);

    public int Completed => answers.Count;
    public int Unresolved => answers.Values.Count(a => a.Unresolved);
    public int AddedReads => answers.Values.Sum(a => a.Reads.Length);

    public async Task RunAsync(SessionTrace trace, ICoverageReviewer reviewer, Action completed, CancellationToken token)
    {
        foreach (var call in trace.Calls.Where(c => c.Review != null && !answers.ContainsKey(c)))
        {
            token.ThrowIfCancellationRequested();
            var answer = await reviewer.ReviewAsync(call.Review!, token);
            token.ThrowIfCancellationRequested();
            var added = answer.Reads.Where(f => !call.Files.Any(old => old.Action == "Read" && trace.SameFile(old.Path, f.Path)))
                .DistinctBy(f => trace.FileKey(f.Path)).ToArray();
            answers.Add(call, answer with { Reads = added });
            completed();
        }
    }

    internal TraceCall Include(TraceCall call) => answers.TryGetValue(call, out var answer)
        ? call with { Files = [.. call.Files, .. answer.Reads], ReviewUnresolved = answer.Unresolved } : call;
}

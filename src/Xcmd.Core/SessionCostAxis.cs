// @implements InspectAgentSession
namespace Xcmd.Core;

public readonly record struct CostBoundary(decimal Usd, int Missing, int Free)
{
    public static CostBoundary operator -(CostBoundary end, CostBoundary start) =>
        new(end.Usd - start.Usd, end.Missing - start.Missing, end.Free - start.Free);
}

public sealed class SessionCostAxis
{
    private readonly CostBoundary[] boundaries;

    internal SessionCostAxis(IReadOnlyList<SessionResponse> responses)
    {
        boundaries = new CostBoundary[responses.Count + 1];
        for (var i = 0; i < responses.Count; i++)
        {
            var cost = responses[i].Cost.Usd;
            var previous = boundaries[i];
            boundaries[i + 1] = new(previous.Usd + (cost ?? 0), previous.Missing + (cost == null ? 1 : 0),
                previous.Free + (cost == 0 ? 1 : 0));
        }
    }

    public CostBoundary At(int boundary) => boundaries[boundary];
}

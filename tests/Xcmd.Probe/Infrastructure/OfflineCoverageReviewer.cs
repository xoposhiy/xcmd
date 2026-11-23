// @tests InspectAgentSession
using Xcmd.Core;

namespace Xcmd.Probe.Infrastructure;

internal sealed class OfflineCoverageReviewer : ICoverageReviewer
{
    public int Requests { get; private set; }
    public Task<CoverageAnswer> ReviewAsync(CoverageInput input, CancellationToken token)
    {
        Requests++;
        return Task.FromResult(new CoverageAnswer([new TraceFile("src/Reviewed.cs", "Read", Lines: 4)
            { ModelEvidence = input.Command + "\nLine count evidence: 4" }], false));
    }
}

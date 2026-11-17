// @implements HeadlessVerification, IntegrationVerification
using System.Diagnostics;

namespace Xcmd.Probe.Infrastructure;

public sealed class ProbeTimer
{
    private readonly Stopwatch watch = Stopwatch.StartNew();
    public TimeSpan Elapsed => watch.Elapsed;
    public static ProbeTimer Start() => new();
}

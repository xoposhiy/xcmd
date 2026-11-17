// @implements HeadlessVerification, IntegrationVerification
using System.Diagnostics;

namespace Xcmd.Probe.Infrastructure;

public static class FixtureCleanup
{
    public static int Remove(string scenarioDirectory)
    {
        var root = Path.GetFullPath(scenarioDirectory);
        var fixture = Path.GetFullPath(Path.Combine(root, "fixture"));
        if (!fixture.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The fixture cleanup target is outside the scenario directory.");
        var elapsed = Stopwatch.StartNew();
        var retries = 0;
        while (Directory.Exists(fixture))
        {
            try { Directory.Delete(fixture, recursive: true); }
            catch (Exception error) when ((error is IOException or UnauthorizedAccessException) && elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                // Windows can briefly retain assembly locks after a child process exits.
                retries++; Thread.Sleep(100);
            }
        }
        return retries;
    }
}

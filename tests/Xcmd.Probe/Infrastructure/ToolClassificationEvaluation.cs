// @tests InspectAgentSession, IntegrationVerification
using System.Text.Json;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

internal static class ToolClassificationEvaluation
{
    internal static async Task<int> RunAsync(string transcript, string project, string cache, bool live)
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(15) };
        var classifier = live ? new LunaSessionClassifier(cache, () => Environment.GetEnvironmentVariable("OPENAI_API_KEY"), http) : null;
        var reader = new SessionTranscriptReader(cache, cache, classifier);
        var projectDirectory = Path.Combine(cache, project);
        var session = new AgentSession(SessionAgent.Codex, Guid.Empty, "", projectDirectory, DateTimeOffset.UnixEpoch) { TranscriptPath = transcript };
        var trace = live ? await reader.AnalyzeAsync(session, CancellationToken.None) : await reader.ReadAsync(session, CancellationToken.None);
        foreach (var call in trace.Calls)
        {
            var answer = call.Classification;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                id = call.Id,
                pairs = answer?.Pairs.Select(p => new { target = p.Label.Target, action = p.Label.Action }).ToArray() ?? [],
                resources = answer?.Resources.Select(r => new { kind = r.Kind, value = r.Value }).ToArray() ?? []
            }));
        }
        if (trace.ClassificationError == null) return 0;
        Console.Error.WriteLine(trace.ClassificationError);
        return 1;
    }
}

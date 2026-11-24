// @implements InspectAgentSession
namespace Xcmd.Core;

public readonly record struct SessionActivityTrack(string Agent, string Name)
{
    public static SessionActivityTrack For(string agent, string? target, bool cost)
    {
        if (!cost && agent != SessionTrace.MainAgent) return new(agent, agent);
        return new(agent, target switch
        {
            "source" => "Source",
            "tests" => "Tests",
            "docs" or "agent_instructions" or "tracker" => "Docs",
            _ => "Others"
        });
    }

    public static SessionActivityTrack[] OnAxis(SessionTrace trace, bool cost)
    {
        var first = new Dictionary<SessionActivityTrack, long>();
        if (cost)
        {
            foreach (var response in trace.Responses)
                foreach (var track in response.Calls.SelectMany(call => call.Labels.Select(label => For(response.Agent, label.Target, true)))
                    .DefaultIfEmpty(For(response.Agent, null, true)))
                    first.TryAdd(track, response.Index);
        }
        else
            foreach (var call in trace.Calls)
                foreach (var label in call.Labels)
                {
                    var track = For(call.Agent, label.Target, false);
                    first[track] = Math.Min(first.GetValueOrDefault(track, long.MaxValue), call.Start);
                }
        var main = SessionTrace.MainAgent;
        SessionActivityTrack[] tracks = [For(main, "source", cost), For(main, "tests", cost), For(main, "docs", cost), For(main, null, cost),
            .. (cost ? first.Keys : trace.Agents.Skip(1).Select(agent => For(agent, null, false)))];
        return tracks.Distinct().OrderBy(track => first.GetValueOrDefault(track, long.MaxValue)).ToArray();
    }
}

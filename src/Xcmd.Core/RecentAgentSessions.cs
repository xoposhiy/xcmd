// @implements ViewRecentAgentSessions
using System.Collections.ObjectModel;

namespace Xcmd.Core;

public sealed class RecentAgentSessions
{
    private readonly ObservableCollection<AgentSession> rows = [];
    private readonly Dictionary<(SessionAgent, Guid), AgentSession> byId = [];
    public ReadOnlyObservableCollection<AgentSession> Rows { get; }

    public RecentAgentSessions() => Rows = new(rows);

    public AgentSession? Find(AgentSession? session) =>
        session == null ? null : byId.GetValueOrDefault((session.Agent, session.Id));

    public void AddPage(IReadOnlyList<AgentSession> page)
    {
        foreach (var session in page)
        {
            var key = (session.Agent, session.Id);
            if (byId.TryGetValue(key, out var previous))
            {
                if (previous == session || previous.Updated > session.Updated) continue;
                rows.Remove(previous);
            }
            byId[key] = session;
            rows.Insert(InsertionIndex(session), session);
        }
    }

    public bool HasGapBefore(SessionAgent agent, IReadOnlyList<AgentSession> recent)
    {
        var boundary = recent.OrderByDescending(row => row.Updated).Skip(19).FirstOrDefault();
        var latest = rows.FirstOrDefault(row => row.Agent == agent);
        return boundary != null && (latest == null || boundary.Updated > latest.Updated);
    }

    public void Reconcile(SessionAgent agent, IReadOnlyList<AgentSession> returned, SessionListScope scope)
    {
        // An incomplete page cannot rule out sessions tied at its oldest timestamp.
        var boundary = scope == SessionListScope.Recent
            ? returned.OrderByDescending(row => row.Updated).Skip(19).FirstOrDefault()?.Updated : null;
        var ids = returned.Select(row => row.Id).ToHashSet();
        var covered = boundary == null ? rows.AsEnumerable() : rows.TakeWhile(row => row.Updated > boundary);
        foreach (var row in covered.Where(row => row.Agent == agent && !ids.Contains(row.Id)).ToArray())
        {
            byId.Remove((agent, row.Id));
            rows.Remove(row);
        }
    }

    private int InsertionIndex(AgentSession session)
    {
        var low = 0;
        var high = rows.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var row = rows[middle];
            var order = session.Updated.CompareTo(row.Updated);
            if (order == 0) order = row.Id.CompareTo(session.Id);
            if (order == 0) order = row.Agent.CompareTo(session.Agent);
            if (order < 0) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}

// @implements ViewRecentAgentSessions, ResumeAgentSession
namespace Xcmd.Core;

public enum SessionAgent { ClaudeCode, Codex }

public enum SessionListScope { History, Recent }
public enum SessionActivity { Unknown, Working, Waiting, Idle, Completed, Failed, Stopped }

public sealed record AgentSession(SessionAgent Agent, Guid Id, string Topic, string Directory, DateTimeOffset Updated)
{
    public string ProjectDirectory { get; init; } = Directory;
    public string ProjectName => Path.GetFileName(Path.TrimEndingDirectorySeparator(ProjectDirectory)) is { Length: > 0 } name ? name : ProjectDirectory;
    public SessionActivity Activity { get; init; }
    public static string AgentName(SessionAgent agent) => agent == SessionAgent.Codex ? "Codex" : "Claude Code";
    public string DisplayTopic => string.IsNullOrWhiteSpace(Topic) ? Id.ToString() : Topic.Trim();
    public string ResumeArguments => (Agent == SessionAgent.Codex ? "resume " : "--resume ") + Id.ToString("D");
    public string LocalUpdatedTime => Updated.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string DisplayUpdatedTime(DateTimeOffset now)
    {
        var age = now - Updated;
        if (age < TimeSpan.Zero || age.TotalDays >= 1) return LocalUpdatedTime;
        if (age.TotalMinutes < 1) return "just now";
        if (age.TotalHours < 1) return age.Minutes == 1 ? "1 minute ago" : $"{age.Minutes} minutes ago";
        return age.Hours == 1 ? "1 hour ago" : $"{age.Hours} hours ago";
    }
}

public interface IAgentSessions
{
    Task ListAsync(SessionAgent agent, SessionListScope scope, Func<IReadOnlyList<AgentSession>, Task> showPage, CancellationToken token);
    ExternalLaunch Resume(AgentSession session);
}

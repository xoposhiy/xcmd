namespace Xcmd.Core;

public sealed record IssueTarget(string Id, string Name);
public sealed record FeedbackIssue(string Id, string Status, string Text, IssueTarget Target, string Release)
{
    public const string New = "new";
    public const string InProgress = "in-progress";
    public const string Closed = "closed";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Any(char.IsWhiteSpace) ||
            Status is not (New or InProgress or Closed) || string.IsNullOrWhiteSpace(Text) ||
            Target == null || string.IsNullOrWhiteSpace(Target.Id) || string.IsNullOrWhiteSpace(Target.Name) || string.IsNullOrWhiteSpace(Release))
            throw new InvalidDataException("Invalid issue: " + Id);
    }

    public static FeedbackIssue FromLegacy(FeedbackNote note) => new(note.Id,
        note.ResolvedIn == null ? New : Closed, note.Text, new(note.ElementId, note.ElementName), note.Release);
}

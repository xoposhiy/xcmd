// @implements FeedbackQueue
using System.Text.Encodings.Web;
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class IssueStore(string directory)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string DatabasePath => Path.Combine(DirectoryPath, "issues.db");

    public static IssueStore ForApplication()
    {
        for (var location = new DirectoryInfo(AppContext.BaseDirectory); location != null; location = location.Parent)
            if (File.Exists(Path.Combine(location.FullName, "Xcmd.slnx"))) return new(Path.Combine(location.FullName, "issues"));
        throw new IOException("Cannot find the Xcmd repository. Keep releases inside the repository so feedback can be saved to issues/issues.db.");
    }

    public Task<FeedbackIssue[]> ReadAsync() => Task.Run(Read);
    public FeedbackIssue[] Read()
    {
        using var database = Open();
        return Read(database);
    }

    public Task AddAsync(FeedbackIssue issue) => Task.Run(() => Add(issue));
    public void Add(FeedbackIssue issue) => Write(database =>
    {
        issue.Validate();
        Insert(database, issue);
        return true;
    });

    public Task ImportAsync(IEnumerable<FeedbackNote> notes, bool once = false) => Task.Run(() => Import(notes, once));
    public void Import(IEnumerable<FeedbackNote> notes, bool once = false) => Write(database =>
    {
        if (once && database.Query("SELECT completed FROM legacy_import").Length != 0) return false;
        var ids = Read(database).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var note in notes)
            if (ids.Add(note.Id)) Insert(database, FeedbackIssue.FromLegacy(note));
        if (once) database.Execute("INSERT INTO legacy_import (completed) VALUES (1)");
        return true;
    });

    public FeedbackIssue[] Change(string command, string[] ids) => Write(database =>
    {
        var issues = Read(database);
        if (command == "start" && issues.Any(item => item.Status == FeedbackIssue.InProgress))
            throw new InvalidOperationException("Stop: work is already in progress. Coordinate with the current worker before starting anything.\n" +
                string.Join("\n", issues.Where(item => item.Status == FeedbackIssue.InProgress).Select(item => item.Id + ": " + item.Text)));
        if (ids.Length == 0) throw new ArgumentException("Specify at least one issue ID (an unambiguous prefix is accepted).");
        var selected = ids.Select(id => Resolve(issues, id)).DistinctBy(item => item.Id).ToArray();
        var (before, after) = command switch
        {
            "start" => (FeedbackIssue.New, FeedbackIssue.InProgress),
            "close" => (FeedbackIssue.InProgress, FeedbackIssue.Closed),
            "release" => (FeedbackIssue.InProgress, FeedbackIssue.New),
            "reopen" => (FeedbackIssue.Closed, FeedbackIssue.New),
            _ => throw new ArgumentException("Unknown issue command: " + command)
        };
        if (selected.Any(item => item.Status != before)) throw new InvalidOperationException(command + " requires every selected issue to be " + before + ". Nothing changed.");
        foreach (var issue in selected) database.Execute("UPDATE issues SET status = ?1 WHERE id = ?2", after, issue.Id);
        return selected.Select(item => item with { Status = after }).ToArray();
    });

    public Task RemoveAsync(string id) => Task.Run(() => Write(database =>
    {
        var selected = Resolve(Read(database), id);
        if (selected.Status == FeedbackIssue.InProgress) throw new InvalidOperationException("This issue is in progress. Release it before removing it.");
        database.Execute("DELETE FROM issues WHERE id = ?1", selected.Id);
        return true;
    }));

    public static string Serialize(FeedbackIssue issue) => JsonSerializer.Serialize(issue, Json);

    private static FeedbackIssue Resolve(FeedbackIssue[] issues, string id)
    {
        var matches = issues.Where(item => item.Id.StartsWith(id, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new ArgumentException(matches.Length == 0 ? "Unknown issue: " + id : "Ambiguous issue ID: " + id);
    }

    private static FeedbackIssue[] Read(IssueDatabase database) => database.Query(
        "SELECT id, status, text, target_id, target_name, release FROM issues ORDER BY rowid")
        .Select(row => new FeedbackIssue(row[0], row[1], row[2], new(row[3], row[4]), row[5])).ToArray();

    private static void Insert(IssueDatabase database, FeedbackIssue issue)
    {
        issue.Validate();
        database.Execute("INSERT INTO issues (id, status, text, target_id, target_name, release) VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            issue.Id, issue.Status, issue.Text, issue.Target.Id, issue.Target.Name, issue.Release);
    }

    private IssueDatabase Open()
    {
        Directory.CreateDirectory(DirectoryPath);
        return new IssueDatabase(DatabasePath);
    }

    private T Write<T>(Func<IssueDatabase, T> action)
    {
        using var database = Open();
        // Reserve the writer before checking work: competing agents must see the committed status.
        database.Execute("BEGIN IMMEDIATE");
        var result = action(database);
        database.Execute("COMMIT");
        // Closing after an exception rolls back the uncommitted transaction.
        return result;
    }
}

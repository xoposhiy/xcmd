// @tests ReportControl, KeepFeedback, WorkspaceStorage, FeedbackIdentity, FeedbackQueue
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Workspace;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class IssueStoreScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("issues-sqlite-compact-roundtrip", "UC39; compact independent SQLite and multiline Unicode", async c =>
        {
            var store = new IssueStore(Path.Combine(c.FixturePath, "issues"));
            var issue = new FeedbackIssue("one", FeedbackIssue.New, "Убери это\nИ это тоже", new("CommanderWindow.status", "Ready"), "release-1");
            await store.AddAsync(issue);
            Equal(issue, (await store.ReadAsync()).Single(), "Multiline Unicode feedback round-trips exactly");
            Check(File.Exists(store.DatabasePath), "Feedback is stored in a real SQLite database");
            Equal("SQLite format 3", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(store.DatabasePath), 0, 15), "The store uses the SQLite file format");
            var json = IssueStore.Serialize(issue);
            Check(json.Contains("Убери это", StringComparison.Ordinal), "UTF-8 text stays readable without Unicode escaping");
            using var document = JsonDocument.Parse(json);
            Equal("id,status,text,target,release", string.Join(',', document.RootElement.EnumerateObject().Select(property => property.Name)), "Only necessary fields are persisted");
            Equal(1, Directory.GetFiles(store.DirectoryPath).Length, "No application lock or custom journal files remain");
        }),
        new("issues-cli-batch-and-work-guard", "UC39; one active work batch and atomic validation", c =>
        {
            var store = new IssueStore(Path.Combine(c.FixturePath, "issues"));
            foreach (var id in new[] { "first", "second", "third" }) store.Add(new(id, FeedbackIssue.New, id, new("test.target", "Target"), "test"));
            var output = new StringWriter(); var error = new StringWriter();
            Equal(0, IssueCli.Run(["start", "first", "second"], store, output, error), "An agent claims a batch");
            Equal(1, IssueCli.Run(["start", "third"], new IssueStore(store.DirectoryPath), output, error), "Another agent is stopped while work is in progress");
            Check(error.ToString().Contains("first", StringComparison.Ordinal) && error.ToString().Contains("Coordinate", StringComparison.Ordinal), "The error identifies existing work and asks the agent to coordinate");
            Equal(FeedbackIssue.New, store.Read().Single(item => item.Id == "third").Status, "A rejected claim changes nothing");
            Equal(1, IssueCli.Run(["close", "first", "unknown"], store, output, error), "Unknown IDs reject the entire batch");
            Equal(2, store.Read().Count(item => item.Status == FeedbackIssue.InProgress), "Batch validation precedes every change");
            store.Change("close", ["first"]); store.Change("release", ["second"]);
            Equal(0, IssueCli.Run(["start", "thi"], store, output, error), "Unambiguous ID prefixes work after the batch finishes");
            store.Change("close", ["third"]); store.Change("reopen", ["first"]);
            output.GetStringBuilder().Clear();
            Equal(0, IssueCli.Run(["list"], store, output, error), "List prints all open issues");
            Equal(2, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length, "Closed issues are omitted from list");
            return Task.CompletedTask;
        }),
        new("issues-cli-real-process-race", "UC39; competing agent processes cannot both start work", async c =>
        {
            var store = new IssueStore(Path.Combine(c.FixturePath, "issues"));
            foreach (var id in new[] { "first", "second" }) store.Add(new(id, FeedbackIssue.New, id, new("test.target", "Target"), "test"));
            var results = await IssueProbe.RaceAsync(store.DirectoryPath);
            Equal(1, results.Count(result => result.Code == 0), "Exactly one real CLI process claims work");
            Equal(1, results.Count(result => result.Code == 1), "The other real CLI process fails explicitly");
            Equal(1, store.Read().Count(item => item.Status == FeedbackIssue.InProgress), "Only one work batch reaches disk");
        }),
        new("issues-sqlite-rollback-and-corruption", "UC39; transaction rollback and explicit corruption errors", c =>
        {
            var store = new IssueStore(Path.Combine(c.FixturePath, "issues"));
            var issue = new FeedbackIssue("one", FeedbackIssue.New, "Keep me", new("test.target", "Target"), "test");
            store.Add(issue);
            try
            {
                store.Import([
                    new("valid", "test", "test.target", "Target", c.LeftPath, "Partial insert", DateTimeOffset.UtcNow),
                    new("invalid", "test", "test.target", "Target", c.LeftPath, "", DateTimeOffset.UtcNow)]);
                throw new InvalidOperationException("Invalid imports must fail.");
            }
            catch (InvalidDataException) { }
            Equal(1, store.Read().Length, "A failure after the first INSERT rolls back the entire transaction");
            File.WriteAllText(store.DatabasePath, "broken SQLite file");
            var output = new StringWriter(); var error = new StringWriter();
            Equal(1, IssueCli.Run(["list"], store, output, error), "A corrupt database produces a failing CLI result");
            Equal("broken SQLite file", File.ReadAllText(store.DatabasePath), "A corrupt database is never silently recreated");
            Check(error.ToString().Contains("database", StringComparison.OrdinalIgnoreCase), "The diagnostic identifies the storage failure");
            return Task.CompletedTask;
        }),
        new("issues-legacy-migration-and-session-independence", "UC39; idempotent migration preserves original text and current status", async c =>
        {
            var session = new SessionStore(Path.Combine(c.FixturePath, "session.json"));
            var legacy = new FeedbackNote("old", "release-old", "left.sort.name", "Name", c.LeftPath, "Original text", DateTimeOffset.UtcNow);
            await session.SaveAsync(c.Window.CaptureWorkspace() with { Notes = [legacy] });
            using var lifecycle = new WorkspaceLifecycle(c.Window, session, null);
            await lifecycle.InitializeAsync([c.LeftPath, c.RightPath]);
            var store = c.Window.Feedback.Store!;
            Equal("Original text", store.Read().Single().Text, "Legacy user text migrates unchanged");
            Check((await session.ReadAsync())!.Notes == null, "Successful migration removes feedback from future session snapshots");
            store.Change("start", ["old"]); store.Change("close", ["old"]);
            await store.ImportAsync([legacy]);
            Equal(FeedbackIssue.Closed, store.Read().Single().Status, "Reimport cannot reopen an already migrated issue");
            File.WriteAllText(session.Path, "{bad session");
            Equal("Original text", store.Read().Single().Text, "A broken session cannot erase independent issues");
            await lifecycle.PollAsync();
            Equal(FeedbackIssue.Closed, c.Window.Feedback.Notes.Single().Status, "CLI changes reach the running UI without a new release");
            await store.RemoveAsync("old");
            await store.ImportAsync([legacy], once: true);
            Equal(0, store.Read().Length, "A stale legacy session cannot resurrect an intentionally removed issue");
        }),
        new("issues-ui-append-keeps-external-changes", "UC39; submitting a comment cannot overwrite another writer's changes", async c =>
        {
            var store = c.Window.Feedback.Store!;
            await store.AddAsync(new("existing", FeedbackIssue.New, "Existing", new("test.target", "Target"), "test"));
            await c.Window.Feedback.RefreshAsync();
            var other = new IssueStore(store.DirectoryPath);
            other.Change("start", ["existing"]); other.Change("close", ["existing"]);
            await c.KeyAsync(Key.F12); await c.ClickAsync(FeedbackRemarksProbe.Button(c.Window, "leftSortSize"));
            var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("New feedback", comment); await c.KeyAsync(Key.Enter, Avalonia.Input.RawInputModifiers.Control, comment);
            var issues = store.Read();
            Equal(2, issues.Length, "Both writers' issues survive");
            Equal(FeedbackIssue.Closed, issues.Single(item => item.Id == "existing").Status, "The UI cannot reopen externally closed work");
            Check(issues.Single(item => item.Id != "existing").Target.Id.Length > 0, "The new note contains a source identity");
            Check(issues.Single(item => item.Id != "existing").Target.Name.StartsWith("left:", StringComparison.Ordinal), "The target also identifies its panel instance");
        }),
        new("issues-ui-save-failure-keeps-draft", "UC39; visible save failure and safe retry", async c =>
        {
            var original = c.Window.Feedback.Store!;
            var blocked = Path.Combine(c.FixturePath, "blocked"); File.WriteAllText(blocked, "A file cannot be a database directory");
            c.Window.Feedback.Store = new IssueStore(blocked);
            await c.KeyAsync(Key.F12); await c.ClickAsync(FeedbackRemarksProbe.Button(c.Window, "leftSortSize"));
            var comment = c.Window.Feedback.ActiveComment!;
            await c.TextAsync("Keep my draft", comment); await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            Check(comment.IsVisible && comment.SaveError.IsVisible, "The comment editor shows a save error without closing");
            Equal("Keep my draft", comment.Editor.Text, "Failed persistence keeps the user's text");
            c.Window.Feedback.Store = original;
            await c.KeyAsync(Key.Enter, RawInputModifiers.Control, comment);
            Equal("Keep my draft", original.Read().Single().Text, "Retry persists the comment exactly once");
        }),
        new("issues-ui-removal-respects-concurrent-claim", "UC39; removal cannot erase active work or crash the UI", async c =>
        {
            var store = c.Window.Feedback.Store!;
            await store.AddAsync(new("one", FeedbackIssue.New, "Work", new("test.target", "Target"), "test"));
            await c.Window.Feedback.RefreshAsync();
            c.Window.Feedback.ShowNotes(); await c.SettleAsync();
            var window = c.Window.Feedback.ActiveNotes!;
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            list.SelectedIndex = 0;
            store.Change("start", ["one"]);
            await c.ClickAsync(FeedbackRemarksProbe.Button(window, "RemoveNote"), target: window);
            Check(window.IsVisible, "A concurrent claim does not crash or close the notes window");
            Check(window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.IsVisible && text.Text?.Contains("in progress", StringComparison.Ordinal) == true), "The notes window explains why removal was refused");
            Equal(FeedbackIssue.InProgress, store.Read().Single().Status, "The active work remains protected");
        })
    ];
}

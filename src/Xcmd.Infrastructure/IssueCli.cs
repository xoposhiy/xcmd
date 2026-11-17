// @implements FeedbackQueue
namespace Xcmd.Infrastructure;

public static class IssueCli
{
    public static int Run(string[] arguments, TextWriter output, TextWriter error)
    {
        try
        {
            var explicitStore = arguments is ["--store", _, ..];
            var store = explicitStore ? new IssueStore(arguments[1]) : IssueStore.ForApplication();
            return Run(explicitStore ? arguments[2..] : arguments, store, output, error);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        { error.WriteLine(failure.Message); return 1; }
    }

    public static int Run(string[] arguments, IssueStore store, TextWriter output, TextWriter error)
    {
        try
        {
            if (arguments is [] or ["help"] or ["--help"])
            {
                output.WriteLine("issues list [--all] | start ID... | close ID... | release ID... | reopen ID...\nlist prints all new and in-progress issues. --all includes closed issues. start refuses while any issue is in progress.\nOne-time migration: import-legacy SESSION_PATH");
                return 0;
            }
            if (arguments is ["import-legacy", var sessionPath])
            {
                using var stream = File.OpenRead(sessionPath);
                var state = System.Text.Json.JsonSerializer.Deserialize(stream, SessionJsonContext.Default.WorkspaceState)
                    ?? throw new InvalidDataException("The legacy session is empty.");
                state.Validate();
                store.Import(state.Notes ?? [], once: true);
                output.WriteLine("Legacy issues imported without replacing existing records.");
                return 0;
            }
            var issues = arguments is ["list"] or ["list", "--all"]
                ? store.Read().Where(item => arguments.Length == 2 || item.Status != Xcmd.Core.FeedbackIssue.Closed).ToArray()
                : store.Change(arguments[0], arguments[1..]);
            foreach (var issue in issues) output.WriteLine(IssueStore.Serialize(issue));
            return 0;
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            error.WriteLine(failure.Message);
            return 1;
        }
    }
}

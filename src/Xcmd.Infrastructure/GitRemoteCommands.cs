// @implements SyncRepository
namespace Xcmd.Infrastructure;

public enum GitRemoteCommand { Push, Pull, PullWithRebase, PullWithMerge }

public static class GitRemoteCommands
{
    public static Task RunAsync(string repository, GitRemoteCommand command) =>
        GitHistoryProcess.RunAsync(repository, Arguments(command), Stream.Null, CancellationToken.None, timeoutSeconds: 120);

    private static string[] Arguments(GitRemoteCommand command) => command switch
    {
        GitRemoteCommand.Push => ["push"],
        GitRemoteCommand.Pull => ["pull", "--ff-only"],
        GitRemoteCommand.PullWithRebase => ["pull", "--rebase"],
        GitRemoteCommand.PullWithMerge => ["pull", "--no-rebase", "--ff", "--no-edit"],
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}

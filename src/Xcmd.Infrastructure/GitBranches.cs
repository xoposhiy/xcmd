// @implements SeeRepositoryState
namespace Xcmd.Infrastructure;

public sealed record GitBranchTip(string Name, string CommitDate)
{
    public override string ToString() => $"{Name} · {CommitDate}";
}

public static class GitBranches
{
    public const int RecentBranchLimit = 4;

    internal static Task<GitBranchTip[]> RecentAsync(string root, CancellationToken token) => ReadAsync(root, RecentBranchLimit, token);

    public static Task<GitBranchTip[]> LocalAsync(string root) => ReadAsync(root, 0, CancellationToken.None);

    private static async Task<GitBranchTip[]> ReadAsync(string root, int limit, CancellationToken token)
    {
        var output = await GitHistoryProcess.TextAsync(root,
            ["for-each-ref", $"--count={limit}", "--sort=refname", "--sort=-committerdate",
                "--format=%(refname:lstrip=2)%09%(committerdate:format-local:%Y-%m-%d)", "refs/heads/"], token).ConfigureAwait(false);
        return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t')).Select(fields => new GitBranchTip(fields[0], fields[1])).ToArray();
    }

    public static Task SwitchAsync(string root, string name) => GitHistoryProcess.RunAsync(root,
        ["switch", "--no-guess", "--no-overwrite-ignore", "--", name], Stream.Null, CancellationToken.None);
}

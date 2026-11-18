// @tests SyncRepository
using Avalonia.Controls;
using Xcmd.Desktop;

namespace Xcmd.Probe.Infrastructure;

internal sealed class GitRemoteFixture(ProbeContext context) : IDisposable
{
    private string Remote => Path.Combine(context.FixturePath, "remote.git");
    public string Peer => Path.Combine(context.FixturePath, "peer");

    public static async Task<GitRemoteFixture> CreateAsync(ProbeContext context)
    {
        var fixture = new GitRemoteFixture(context);
        await GitFixture.RunAsync(context.FixturePath, "init", "--bare", "--template=", "-b", "main", fixture.Remote);
        await GitFixture.InitializeAsync(context.LeftPath);
        await ConfigureAsync(context.LeftPath);
        await fixture.CommitAsync(context.LeftPath, "published.txt");
        await GitFixture.RunAsync(context.LeftPath, "remote", "add", "origin", fixture.Remote);
        await GitFixture.RunAsync(context.LeftPath, "config", "push.autoSetupRemote", "true");
        return fixture;
    }

    public async Task ClonePeerAsync()
    {
        await GitFixture.RunAsync(context.FixturePath, "clone", Remote, Peer);
        await ConfigureAsync(Peer);
        ProbeContext.Check(File.Exists(Path.Combine(Peer, "published.txt")), "The remote contains the commit sent by Xcmd");
    }

    public async Task CommitAsync(string directory, string name)
    {
        File.WriteAllText(Path.Combine(directory, name), name);
        await GitFixture.RunAsync(directory, "add", "--", name);
        await GitFixture.RunAsync(directory, "commit", "-m", name);
    }

    private static async Task ConfigureAsync(string directory)
    {
        await GitFixture.RunAsync(directory, "config", "user.name", "Probe");
        await GitFixture.RunAsync(directory, "config", "user.email", "probe@example.test");
        await GitFixture.RunAsync(directory, "config", "commit.gpgsign", "false");
        await GitFixture.RunAsync(directory, "config", "push.default", "simple");
    }

    public static MenuItem Menu(GitStatusView git, string id) =>
        git.Actions.Items.OfType<MenuItem>().Single(item => FeedbackTarget.Id(item) == "GitStatusView." + id);

    public void Dispose()
    {
        GitFixture.AllowCleanup(context.LeftPath);
        if (Directory.Exists(Path.Combine(Peer, ".git"))) GitFixture.AllowCleanup(Peer);
        foreach (var file in Directory.EnumerateFiles(Remote, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
    }
}

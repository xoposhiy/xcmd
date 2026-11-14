namespace Xcmd.Probe.Infrastructure;

public sealed class GitHistoryFixture(string root) : IDisposable
{
    public static async Task<GitHistoryFixture> CreateAsync(string root)
    {
        var fixture = new GitHistoryFixture(root);
        await GitFixture.RunAsync(root, "init", "-b", "main");
        await GitFixture.RunAsync(root, "config", "user.name", "History Probe");
        await GitFixture.RunAsync(root, "config", "user.email", "history@example.test");
        await GitFixture.RunAsync(root, "config", "core.autocrlf", "false");
        await GitFixture.RunAsync(root, "config", "core.longpaths", "true");
        await GitFixture.RunAsync(root, "config", "commit.gpgsign", "false");
        await GitFixture.RunAsync(root, "add", ".");
        await GitFixture.RunAsync(root, "commit", "-m", "Initial files");
        File.WriteAllText(Path.Combine(root, "folder1", "inside.txt"), "Nested committed version\n");
        File.WriteAllText(Path.Combine(root, "folder1", "new 日本語.txt"), "Added content\n");
        await GitFixture.RunAsync(root, "add", ".");
        await GitFixture.RunAsync(root, "commit", "-m", "Update nested files", "-m", "Full nested commit message.");
        File.WriteAllText(Path.Combine(root, "alpha.txt"), "Committed root version\n");
        await GitFixture.RunAsync(root, "mv", "file2.log", "renamed file.log");
        File.Delete(Path.Combine(root, "zeta.md"));
        await GitFixture.RunAsync(root, "add", ".");
        await GitFixture.RunAsync(root, "commit", "-m", "Update root files");
        File.AppendAllText(Path.Combine(root, "file10.bin"), "unrelated");
        await GitFixture.RunAsync(root, "commit", "-am", "Update unrelated file");
        return fixture;
    }

    public void Dispose() => GitFixture.AllowCleanup(root);

    public async Task<string> AddDetailedCommitAsync()
    {
        var body = "Full commit message with separate paragraphs.\n\n" +
            string.Join("\n", Enumerable.Range(1, 40).Select(index => $"Message line {index}: explain the complete change.")) +
            "\n\nFinal paragraph: Unicode 日本語 and punctuation stay intact.";
        File.AppendAllText(Path.Combine(root, "alpha.txt"), "Detailed change\n");
        await GitFixture.RunAsync(root, "commit", "-am", "Detailed commit", "-m", body);
        return "Detailed commit\n\n" + body + "\n";
    }
}

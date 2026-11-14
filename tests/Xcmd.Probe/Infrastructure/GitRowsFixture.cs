namespace Xcmd.Probe.Infrastructure;

public static class GitRowsFixture
{
    public static async Task CreateIgnoredRepositoryAsync(ProbeContext context)
    {
        await CreateAsync(context);
        foreach (var folder in new[] { "ignored", "new-tree" })
            await GitFixture.RunAsync(Path.Combine(context.LeftPath, folder), "init", "--template=", "-b", "main");
        await GitFixture.RefreshAsync(context.Window.Left.Git);
        await context.SettleAsync();
    }

    public static async Task CreateAsync(ProbeContext context, bool conflict = false)
    {
        var root = context.LeftPath;
        foreach (var folder in new[] { "dirty/nested", "clean-folder", "old-folder", "deleted-folder", "ignored", "new-tree" }) Directory.CreateDirectory(Path.Combine(root, folder));
        File.WriteAllText(Path.Combine(root, "dirty/nested/tracked.txt"), "initial\n");
        File.WriteAllText(Path.Combine(root, "clean-folder/clean.txt"), "clean\n");
        File.WriteAllText(Path.Combine(root, "old-folder/source.txt"), "rename me\n");
        File.WriteAllText(Path.Combine(root, "old-folder/keep.txt"), "keep\n");
        File.WriteAllText(Path.Combine(root, "deleted-folder/victim.txt"), "delete me\n");
        File.WriteAllText(Path.Combine(root, ".gitignore"), "ignored/\n*.cache\n");
        await GitFixture.RunAsync(root, "init", "--template=", "-b", "main");
        await GitFixture.RunAsync(root, "config", "core.longpaths", "true");
        await GitFixture.RunAsync(root, "config", "commit.gpgsign", "false");
        await GitFixture.RunAsync(root, "config", "core.hooksPath", Path.Combine(root, ".git", "disabled-hooks"));
        await GitFixture.RunAsync(root, "config", "core.excludesFile", Path.Combine(root, ".git", "info", "exclude"));
        await GitFixture.RunAsync(root, "config", "user.name", "Probe");
        await GitFixture.RunAsync(root, "config", "user.email", "probe@example.test");
        await GitFixture.RunAsync(root, "add", "."); await GitFixture.RunAsync(root, "commit", "-m", "initial");
        if (conflict) await CreateConflictAsync(root);
        File.AppendAllText(Path.Combine(root, "alpha.txt"), "modified");
        File.AppendAllText(Path.Combine(root, "zeta.md"), "staged");
        await GitFixture.RunAsync(root, "add", "zeta.md");
        File.AppendAllText(Path.Combine(root, "zeta.md"), "modified again");
        File.WriteAllText(Path.Combine(root, "added 日本語.txt"), "added");
        await GitFixture.RunAsync(root, "add", "added 日本語.txt");
        File.WriteAllText(Path.Combine(root, "loose.txt"), "untracked");
        File.WriteAllText(Path.Combine(root, "new-tree/loose.txt"), "untracked descendant");
        File.WriteAllText(Path.Combine(root, "new-tree/ignored.cache"), "ignored descendant");
        File.AppendAllText(Path.Combine(root, "dirty/nested/tracked.txt"), "changed\n");
        File.Delete(Path.Combine(root, "deleted-folder/victim.txt"));
        File.WriteAllText(Path.Combine(root, "ignored/generated.bin"), "ignored");
        await GitFixture.RunAsync(root, "mv", "old-folder/source.txt", "renamed file 日本語.txt");
        await context.Window.Left.LoadAsync(root);
        await GitFixture.RefreshAsync(context.Window.Left.Git);
        await context.SettleAsync();
    }

    private static async Task CreateConflictAsync(string root)
    {
        await GitFixture.RunAsync(root, "checkout", "-b", "other");
        File.WriteAllText(Path.Combine(root, "folder1/inside.txt"), "other branch\n");
        await GitFixture.RunAsync(root, "commit", "-am", "other");
        await GitFixture.RunAsync(root, "checkout", "main");
        File.WriteAllText(Path.Combine(root, "folder1/inside.txt"), "main branch\n");
        await GitFixture.RunAsync(root, "commit", "-am", "main");
        try { await GitFixture.RunAsync(root, "merge", "other"); }
        catch (IOException) { return; }
        throw new InvalidOperationException("The fixture merge must create a conflict.");
    }
}

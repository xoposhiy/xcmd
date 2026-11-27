// @tests SeeRepositoryState
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

internal sealed class GitBranchFixture(ProbeContext context) : IDisposable
{
    public const string Topic = "topic/日本語";
    public string FilePath(string name) => Path.Combine(context.LeftPath, name);

    public async Task PrepareAsync()
    {
        var root = context.LeftPath;
        await GitFixture.InitializeAsync(root);
        await GitFixture.RunAsync(root, "add", "alpha.txt", "zeta.md");
        await CommitAsync("Initial files", 1);
        await GitFixture.RunAsync(root, "switch", "-c", Topic);
        File.WriteAllText(FilePath("zeta.md"), "topic version");
        File.WriteAllText(FilePath("topic.txt"), "branch file");
        await GitFixture.RunAsync(root, "add", "zeta.md", "topic.txt");
        await CommitAsync("Topic changes", 3);
        await GitFixture.RunAsync(root, "switch", "main");
        await GitFixture.RefreshAsync(context.Window.Left.Git);
    }

    public async Task AddMoreBranchesAsync()
    {
        await GitFixture.RunAsync(context.LeftPath, "branch", "z-older");
        await GitFixture.RunAsync(context.LeftPath, "branch", "z-oldest");
        await GitFixture.RunAsync(context.LeftPath, "switch", "-c", "another_branch");
        await CommitAsync("Another branch", 2);
        await GitFixture.RunAsync(context.LeftPath, "switch", "main");
        await GitFixture.RefreshAsync(context.Window.Left.Git);
    }

    private Task CommitAsync(string message, int day) => GitFixture.RunAsync(context.LeftPath,
        new DateTimeOffset(2025, 5, day, 12, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2025, 5, day))),
        ["-c", "user.name=Probe", "-c", "user.email=probe@example.test", "-c", "commit.gpgSign=false", "commit", "--allow-empty", "-m", message]);

    public async Task<MenuItem[]> OpenRecentAsync()
    {
        var git = context.Window.Left.Git;
        await context.ClickAsync(git.ActionsButton);
        return git.Actions.Items.SkipWhile(item => item is not Separator).OfType<MenuItem>()
            .Where(item => item.IsVisible).ToArray();
    }

    public async Task<Window> OpenAsync()
    {
        var view = context.Window.Left.Git;
        await context.ClickAsync(view.ActionsButton);
        await context.ClickAsync(GitRemoteFixture.Menu(view, "Branches"));
        return context.Window.OperationDialog ?? throw new InvalidOperationException("The branch picker did not open.");
    }

    public static ListBox List(Window dialog) => dialog.GetVisualDescendants().OfType<ListBox>().Single();
    public static string? SelectedBranch(Window dialog) => (List(dialog).SelectedItem as GitBranchTip)?.Name;

    public static IEnumerable<(string? Name, string? Date)> VisibleRows(Window dialog)
    {
        foreach (var row in List(dialog).GetRealizedContainers().OrderBy(List(dialog).IndexFromContainer))
        {
            var cells = row.GetVisualDescendants().OfType<TextBlock>().ToArray();
            yield return (cells.Single(cell => Grid.GetColumn(cell) == 0).Text, cells.Single(cell => Grid.GetColumn(cell) == 1).Text);
        }
    }

    public static string Message(Window dialog) => string.Join("\n", dialog.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text));
    public async Task SwitchToTopicAsync(Window dialog)
    {
        await context.KeyAsync(Key.Home, target: dialog);
        ProbeContext.Equal(Topic, SelectedBranch(dialog), "The keyboard selects the newest branch");
        await context.KeyAsync(Key.Enter, target: dialog);
    }

    public void Dispose() => GitFixture.AllowCleanup(context.LeftPath);
}

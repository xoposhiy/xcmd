// @tests SeeRepositoryState
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels.Git;
using Xcmd.Desktop.Shared.Ui;

namespace Xcmd.Probe.Infrastructure;

internal sealed class WorkflowFixture : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UnixEpoch;
    internal static WorkflowStatusCache NoRemoteWorkflows() => new(_ => Task.FromResult(WorkflowStatus.Empty(false)), TimeProvider.System);
    internal void Advance(TimeSpan time) => now += time;
    public override DateTimeOffset GetUtcNow() => now;
    internal static TextBlock? Indicator(GitStatusView view) => view.GetVisualDescendants().OfType<TextBlock>()
        .SingleOrDefault(text => text.GetValue(FeedbackTarget.IdProperty) == "GitStatusView.workflows");

    internal static string Details(GitStatusView view) => Indicator(view) is { } indicator ? ToolTip.GetTip(indicator) as string ?? "" : "";
    internal static bool Shows(GitStatusView view, string label, string symbol) =>
        Indicator(view) is { IsVisible: true, Inlines: [Run { Text: "CI: " }, Run mark] }
        && mark.Text == symbol && mark.Foreground is ISolidColorBrush brush && ExpectedColor(brush.Color.ToHsl().H, label)
        && Details(view).Contains(" · " + label + " · ", StringComparison.Ordinal);

    private static bool ExpectedColor(double hue, string label) => label switch
    {
        "Passed" => hue is > 90 and < 160,
        "Failed" => hue is < 20 or > 340,
        _ => hue is >= 30 and <= 65
    };

    internal static async Task AdvanceRemoteAsync(string root, string message)
    {
        await GitFixture.RunAsync(root, "-c", "user.name=Probe", "-c", "user.email=probe@example.test", "commit", "--allow-empty", "-m", message);
        await GitFixture.RunAsync(root, "update-ref", "refs/remotes/origin/main", "HEAD");
    }
}

internal sealed class WorkflowRemote
{
    private TaskCompletionSource<string?>? nextTip;
    public string? RequestedCommit { get; private set; }
    public string RunCommit { get; set; } = "first";
    public string Conclusion { get; set; } = "success";
    public void DelayTip() => nextTip = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void ReturnTip(string value) => nextTip?.TrySetResult(value);
    public Task<string?> RunAsync(string[] args)
    {
        if (args.Contains("repos/team/project/branches/main")) return nextTip?.Task ?? Task.FromResult<string?>("first");
        if (args[0] != "run" || !args.Contains("--commit") || !args.Contains("--branch")) throw new InvalidOperationException("Unexpected GitHub CLI arguments.");
        RequestedCommit = args[Array.IndexOf(args, "--commit") + 1];
        return Task.FromResult<string?>($$"""[{"workflowDatabaseId":1,"workflowName":"Verify","headSha":"{{RunCommit}}","status":"completed","conclusion":"{{Conclusion}}"}]""");
    }
}

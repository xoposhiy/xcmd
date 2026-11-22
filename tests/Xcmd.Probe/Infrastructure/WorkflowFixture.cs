// @tests SeeRepositoryState
using Avalonia.Controls;
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
    internal static TextBlock Indicator(GitStatusView view) => view.GetVisualDescendants().OfType<TextBlock>()
        .Single(text => text.GetValue(FeedbackTarget.IdProperty) == "GitStatusView.workflows");
}

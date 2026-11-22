// @implements SeeRepositoryState
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Panels.Git;

public interface IWorkflowStatusProvider
{
    WorkflowStatusCache WorkflowStatuses { get; }
}

internal sealed class GitWorkflowIndicator : TextBlock
{
    private readonly WorkflowStatusCache? cache;
    private WorkflowTarget? target;
    private Task<WorkflowStatus>? lastRead;

    internal GitWorkflowIndicator(WorkflowStatusCache? cache)
    {
        this.cache = cache ?? (Application.Current as IWorkflowStatusProvider)?.WorkflowStatuses;
        IsVisible = false;
        Margin = new Thickness(5, 0, 0, 0);
        VerticalAlignment = VerticalAlignment.Center;
        FontSize = UiPalette.MetaSize;
    }

    internal async Task UpdateAsync(WorkflowTarget? current, bool poll)
    {
        if (target != current)
        {
            target = current;
            lastRead = null;
            Text = "";
            IsVisible = false;
        }
        if (current == null || !poll || cache == null) return;
        var read = cache.ReadAsync(current);
        if (lastRead == read) return;
        lastRead = read;
        var result = await read;
        if (lastRead != read || target != current) return;
        Text = result.Label.Length == 0 ? "" : "CI " + result.Label;
        IsVisible = result.Label.Length > 0;
        Foreground = result.Label switch { "Passed" => UiPalette.Success, "Failed" => UiPalette.Error, "Running" or "Pending" => UiPalette.Mark, _ => UiPalette.Secondary };
        var description = $"GitHub Actions · {current.Repository} · {current.Ref}\n{result.Details}";
        ToolTip.SetTip(this, description);
        AutomationProperties.SetName(this, description);
    }
}

// @implements SeeRepositoryState
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
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
        Margin = new Thickness(8, 0, 0, 0);
        VerticalAlignment = VerticalAlignment.Center;
        FontFamily = new FontFamily("Consolas");
        FontSize = UiPalette.BodySize;
        FontWeight = FontWeight.SemiBold;
        LineHeight = 18;
        Foreground = UiPalette.Secondary;
    }

    internal async Task UpdateAsync(WorkflowTarget? current, bool poll)
    {
        if (target != current)
        {
            target = current;
            lastRead = null;
            if (current == null) IsVisible = false;
            else ShowStatus(current, "Unknown", "Waiting for the workflow status.");
        }
        if (current == null || !poll || cache == null) return;
        var read = cache.ReadAsync(current);
        if (lastRead == read) return;
        lastRead = read;
        var result = await read;
        if (lastRead != read || target != current) return;
        ShowStatus(current, result.Label, result.Details);
    }

    private void ShowStatus(WorkflowTarget current, string label, string details)
    {
        IsVisible = label.Length > 0;
        Inlines!.Clear();
        if (!IsVisible) return;
        var (symbol, color) = label switch
        {
            "Passed" => ("●", UiPalette.Success),
            "Failed" => ("×", UiPalette.Error),
            "Running" or "Pending" => ("●", UiPalette.Mark),
            _ => ("○", UiPalette.Mark)
        };
        Inlines.Add(new Run("CI: "));
        Inlines.Add(new Run(symbol) { Foreground = color });
        var description = $"GitHub Actions · {label} · {current.Repository} · {current.Ref}\n{details}";
        ToolTip.SetTip(this, description);
        AutomationProperties.SetName(this, description);
    }
}

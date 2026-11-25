// @implements SeeRepositoryState
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Panels.Git;

public interface IWorkflowStatusProvider
{
    WorkflowStatusCache WorkflowStatuses { get; }
}

internal sealed class GitWorkflowIndicator : Button
{
    private static readonly Cursor LinkCursor = new(StandardCursorType.Hand);
    private readonly WorkflowStatusCache? cache;
    private readonly TextBlock text = new TextBlock
    {
        FontFamily = new FontFamily("Consolas"),
        FontSize = UiPalette.BodySize,
        FontWeight = FontWeight.SemiBold,
        LineHeight = 18,
        Foreground = UiPalette.Secondary,
        IsHitTestVisible = false
    }.WithFeedbackId("GitStatusView.workflowText");
    private WorkflowTarget? target;
    private Task<WorkflowStatus>? lastRead;

    internal Uri? RunUrl { get; private set; }

    internal GitWorkflowIndicator(WorkflowStatusCache? cache)
    {
        this.cache = cache ?? (Application.Current as IWorkflowStatusProvider)?.WorkflowStatuses;
        IsVisible = false;
        IsEnabled = false;
        Height = 24;
        MinHeight = 0;
        MinWidth = 0;
        Padding = new Thickness(0);
        Margin = new Thickness(8, 0, 0, 0);
        VerticalAlignment = VerticalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Classes.Add("quiet");
        Content = text;
        ToolTip.SetShowOnDisabled(this, true);
    }

    protected override Type StyleKeyOverride => typeof(Button);

    internal async Task UpdateAsync(WorkflowTarget? current, bool poll)
    {
        if (target != current)
        {
            target = current;
            lastRead = null;
            ShowStatus(current, current == null ? "" : "Unknown", "Waiting for the workflow status.");
        }
        if (current == null || !poll || cache == null) return;
        var read = cache.ReadAsync(current);
        if (lastRead == read) return;
        lastRead = read;
        var result = await read;
        if (lastRead != read || target != current) return;
        ShowStatus(current, result.Label, result.Details, result.RunUrl);
    }

    private void ShowStatus(WorkflowTarget? current, string label, string details, Uri? url = null)
    {
        IsVisible = current != null && label.Length > 0;
        RunUrl = IsVisible ? url : null;
        IsEnabled = RunUrl != null;
        Cursor = IsEnabled ? LinkCursor : null;
        text.Inlines!.Clear();
        if (!IsVisible || current == null) return;
        var (symbol, color) = label switch
        {
            "Passed" => ("●", UiPalette.Success),
            "Failed" => ("×", UiPalette.Error),
            "Running" or "Pending" => ("●", UiPalette.Mark),
            _ => ("○", UiPalette.Mark)
        };
        text.Inlines.Add(new Run("CI: ") { TextDecorations = IsEnabled ? TextDecorations.Underline : null });
        text.Inlines.Add(new Run(symbol) { Foreground = color });
        var description = $"GitHub Actions · {label} · {current.Repository} · {current.Ref}\n{details}";
        ToolTip.SetTip(this, IsEnabled ? description + "\nOpen the workflow run in your browser." : description);
        AutomationProperties.SetName(this, IsEnabled ? "Open workflow run · " + description : description);
    }
}

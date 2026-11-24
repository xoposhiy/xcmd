// @implements ViewRecentAgentSessions
using Avalonia.Controls;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Panels;

internal sealed partial class AgentSessionView
{
    private event Action? UpdateClassifications;

    private TextBlock AgentCell(AgentSession row)
    {
        var name = row.Agent == SessionAgent.Codex ? "Codex" : "Claude";
        var text = Cell(name, row.Agent == SessionAgent.Codex ? UiPalette.Focus : UiPalette.Mark);
        CancellationTokenSource? reading = null;
        text.AttachedToVisualTree += (_, _) => { UpdateClassifications += Refresh; Refresh(); };
        text.DetachedFromVisualTree += (_, _) => { UpdateClassifications -= Refresh; reading?.Cancel(); reading = null; };
        return text;

        async void Refresh()
        {
            if (reading != null || source == null) return;
            using var request = new CancellationTokenSource();
            reading = request;
            try
            {
                var ready = await source.IsClassificationReadyAsync(row, request.Token);
                if (request.IsCancellationRequested) return;
                text.Text = ready ? name + " ✓" : name;
                ToolTip.SetTip(text, ready ? "F3 uses saved Luna results" : "Press F3 to inspect the session. Choose Analyze with Luna to classify calls.");
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                if (request.IsCancellationRequested) return;
                text.Text = name;
                ToolTip.SetTip(text, "Luna cache status unavailable");
            }
            finally { if (reading == request) reading = null; }
        }
    }
}

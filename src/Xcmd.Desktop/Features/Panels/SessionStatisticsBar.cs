// @implements ViewRecentAgentSessions
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Panels;

internal sealed class SessionStatisticsBar : TextBlock
{
    private AgentSession? selected;
    private CancellationTokenSource? reading;

    internal SessionStatisticsBar()
    {
        IsVisible = false;
        VerticalAlignment = VerticalAlignment.Center;
        Foreground = UiPalette.Text;
        FontSize = UiPalette.MetaSize;
        TextTrimming = TextTrimming.CharacterEllipsis;
    }

    internal async Task ShowAsync(AgentSession? session, IAgentSessions? source)
    {
        if (source == null) session = null;
        if (selected == session) return;
        selected = session;
        reading?.Cancel();
        IsVisible = session != null;
        ToolTip.SetTip(this, null);
        if (session == null || source == null) { Text = ""; return; }
        Text = "Loading session statistics…";
        using var request = new CancellationTokenSource();
        reading = request;
        try
        {
            var result = await source.ReadStatisticsAsync(session, request.Token);
            if (request.IsCancellationRequested) return;
            if (result == null) { Text = "Session statistics unavailable"; return; }
            Text = Summary(result);
            ToolTip.SetTip(this, Details(result));
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (request.IsCancellationRequested) return;
            Text = "Session statistics unavailable";
            ToolTip.SetTip(this, error.Message);
        }
        finally { if (reading == request) reading = null; }
    }

    private static string Summary(SessionStatistics stats)
    {
        List<string> fields =
        [
            stats.Prompts == 1 ? "1 user prompt" : $"{stats.Prompts:N0} user prompts",
            stats.ToolCalls == 1 ? "1 tool call" : $"{stats.ToolCalls:N0} tool calls"
        ];
        if (stats.Model != null) fields.Add(stats.Model);
        return string.Join(" · ", fields);
    }

    private static string Details(SessionStatistics stats)
    {
        List<string> lines = [Summary(stats)];
        if (stats.TotalTokens is { } total) lines.Add($"Total tokens: {total:N0}");
        if (stats.ContextTokens is { } last) lines.Add($"Tokens in last request: {last:N0}");
        if (stats.ContextWindow is { } window) lines.Add($"Context limit: {window:N0} tokens");
        if (stats.Prompts > 1 && stats.PromptSpan is { } span) lines.Add($"Time between first and last user prompts: {span:c}");
        if (stats.CostUsd is { } cost) lines.Add("Recorded cost: " + cost.ToString("$0.00##", CultureInfo.InvariantCulture));
        return string.Join("\n", lines);
    }
}

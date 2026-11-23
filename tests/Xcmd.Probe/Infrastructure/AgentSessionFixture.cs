// @tests NavigatePanels, ViewRecentAgentSessions, ResumeAgentSession, InspectAgentSession
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;

namespace Xcmd.Probe.Infrastructure;

internal sealed class AgentSessionFixture : IAgentSessions
{
    internal readonly AgentSession Claude = new(SessionAgent.ClaudeCode, Guid.Parse("22222222-1111-4111-8111-222222222222"),
        "Review keyboard navigation", @"C:\work\keyboard", DateTimeOffset.UnixEpoch.AddDays(2));
    internal readonly AgentSession Codex = new(SessionAgent.Codex, Guid.Parse("11111111-1111-4111-8111-111111111111"),
        "Add session columns", @"C:\work\panel", DateTimeOffset.UtcNow.AddMinutes(-5))
    { TranscriptBytes = 1536 };

    private AgentSession[] older = [];
    internal Func<AgentSession, CancellationToken, Task<SessionStatistics?>> Statistics { get; set; } =
        (_, _) => Task.FromResult<SessionStatistics?>(null);
    public Task<SessionStatistics?> ReadStatisticsAsync(AgentSession session, CancellationToken token) => Statistics(session, token);
    internal Func<AgentSession, bool> ClassificationReady { get; set; } = _ => false;
    public Task<bool> IsClassificationReadyAsync(AgentSession session, CancellationToken token) => Task.FromResult(ClassificationReady(session));
    private TaskCompletionSource? historyReady;
    internal Func<AgentSession, CancellationToken, Task<SessionTrace>> Inspector { get; set; } =
        (_, _) => throw new FileNotFoundException("The local transcript is missing.");
    public Task<SessionTrace> InspectAsync(AgentSession session, CancellationToken token, IProgress<string>? progress = null) => Inspector(session, token);

    internal void UseActivityBreakdownTrace()
    {
        var read = new ToolLabel("source", "read");
        var test = new ToolLabel("tests", "exec");
        var trace = new SessionTrace(0, 10000, [
            Call(test, 0, 3000, 300), Call(read, 3000, 3500, 450),
            Call(test, 3500, 6500, 300), Call(test, 6500, 9500, 300),
            Call(read, 9500, 10000, 500)
        ], [], []);
        Inspector = (_, _) => Task.FromResult(trace);

        static TraceCall Call(ToolLabel label, long start, long end, long tokens) =>
            new(SessionTrace.MainAgent, label.ToString(), start, end, tokens, [])
            { Classification = new(label.ToString(), [new(label, [])], []) };
    }

    internal List<(SessionAgent Agent, SessionListScope Scope)> Reads { get; } = [];
    internal void AddOlderSessions(int count) => older = Enumerable.Range(1, count).Select(i => Codex with
    {
        Id = new Guid(i, 0, 0, new byte[8]),
        Topic = $"Conversation {i}",
        Updated = Codex.Updated.AddDays(-i)
    }).ToArray();

    internal AgentSession AddCodexSession(string topic, string directory, DateTimeOffset updated, string? project = null, SessionActivity activity = SessionActivity.Unknown)
    {
        var row = new AgentSession(SessionAgent.Codex, Guid.NewGuid(), topic, directory, updated)
        { ProjectDirectory = project ?? directory, Activity = activity };
        older = [.. older, row];
        return row;
    }

    internal TaskCompletionSource DelayOlderCodexPages() => historyReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task ListAsync(SessionAgent agent, SessionListScope scope, Func<IReadOnlyList<AgentSession>, Task> showPage, CancellationToken token)
    {
        Reads.Add((agent, scope));
        if (agent == SessionAgent.ClaudeCode) { await showPage([Claude]); return; }
        var rows = older.Prepend(Codex).OrderByDescending(row => row.Updated).ToArray();
        await showPage(rows.Take(20).ToArray());
        if (scope == SessionListScope.Recent || rows.Length <= 20) return;
        if (historyReady != null) await historyReady.Task.WaitAsync(token);
        await showPage(rows.Skip(19).ToArray());
    }

    public ExternalLaunch Resume(AgentSession session) => new(ExternalLaunchKind.Command,
        session.Agent == SessionAgent.Codex ? "codex.exe" : "claude.exe", session.Directory, session.ResumeArguments);

    internal static async Task<ContextMenu> OpenSourceMenuAsync(ProbeContext c, Key key)
    {
        await c.KeyAsync(key, RawInputModifiers.Alt);
        return c.Window.GetVisualDescendants().OfType<ContextMenu>().Single(menu => menu.IsOpen);
    }

    internal static string? SelectedSource(ContextMenu menu) =>
        menu.Items.OfType<MenuItem>().Single(item => item.IsSelected && item.IsFocused).Header?.ToString()?.Replace("_", "");

    internal static MenuItem SourceItem(ContextMenu menu, string title) =>
        menu.Items.OfType<MenuItem>().Single(item => item.Header?.ToString()?.Replace("_", "") == title);

    internal static async Task ChooseAsync(ProbeContext c, Key key, string title)
    {
        var menu = await OpenSourceMenuAsync(c, key);
        await c.ClickAsync(SourceItem(menu, title));
    }

    internal static TableView Table(PanelView panel) => panel.GetVisualDescendants().OfType<TableView>().Single(table => table.IsEffectivelyVisible);

    internal static TextBlock Cell(TableView table, int index, string text) => table.ContainerFromIndex(index)!.GetVisualDescendants()
        .OfType<TextBlock>().Single(cell => cell.Text == text);

    internal static async Task ClickHeaderAsync(ProbeContext c, TableView table, string title)
    {
        var header = table.GetVisualDescendants().OfType<TableViewColumnHeader>()
            .Single(item => (item.Column?.Header as TextBlock)?.Text == title);
        var point = header.TranslatePoint(new Point(header.Bounds.Width * 0.8, header.Bounds.Height * 0.2), c.Window)!.Value;
        c.Input.MovePointer(point);
        c.Input.PressPointer(point, MouseButton.Left);
        c.Input.ReleasePointer(point, MouseButton.Left);
        await c.UpdateLayoutAsync();
    }

    internal static string? UpdatedText(TableView table, int index) => table.ContainerFromIndex(index)!.GetVisualDescendants()
        .OfType<TextBlock>().Single(text => ToolTip.GetTip(text) as string == ((AgentSession)table.Items[index]!).LocalUpdatedTime).Text;

    internal static Color RowColor(TableView table, int index) =>
        ((ISolidColorBrush)table.ContainerFromIndex(index)!.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Border").Background!).Color;

    internal static async Task HoverAsync(ProbeContext c, Control row)
    {
        c.Input.MovePointer(row.TranslatePoint(new Point(20, row.Bounds.Height / 2), c.Window)!.Value);
        await c.SettleAsync();
    }
}

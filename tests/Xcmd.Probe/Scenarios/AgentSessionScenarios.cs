// @tests SharedControls, NavigatePanels, ViewRecentAgentSessions, ResumeAgentSession
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Desktop.Features.Panels;
using Xcmd.Infrastructure;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.AgentSessionFixture;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class AgentSessionScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("agent-session-statistics", "One footer line shows the session count and the size of the selected conversation", async c =>
        {
            var codex = SessionStatisticsFixture.CodexTranscript(c.FixturePath, c.AgentSessions.Codex);
            var claude = SessionStatisticsFixture.ClaudeTranscript(c.FixturePath, c.AgentSessions.Claude);
            var codexStats = await SessionStatisticsReader.ReadAsync(codex, CancellationToken.None);
            Equal(new SessionStatistics(2, 2, TimeSpan.FromMinutes(8), 1200, 64000, null, "test-model", 10_000_123), codexStats,
                "Codex keeps the session total separate from the latest request and context limit");
            var pending = new TaskCompletionSource<SessionStatistics?>(TaskCreationOptions.RunContinuationsAsynchronously);
            c.AgentSessions.Statistics = (row, token) => row.Agent == SessionAgent.Codex ? pending.Task : SessionStatisticsReader.ReadAsync(claude, token);
            await ChooseAsync(c, Key.F1, "All Agent Sessions");
            var bar = SessionStatisticsFixture.Bar(c.Window.Left);
            Equal("Loading session statistics…", bar.Text, "The list is ready before its statistics");
            await c.KeyAsync(Key.Down);
            await WorkspaceProbe.WaitAsync(() => bar.Text?.Contains("claude-test", StringComparison.Ordinal) == true);
            Equal("2 user prompts · 100k context tokens · claude-test", bar.Text, "The footer shows context usage in thousands");
            Check(ToolTip.GetTip(bar)?.ToString()?.Contains($"Tokens in last request: {100_007:N0}", StringComparison.Ordinal) == true, "The details show the exact context count");
            Check(ToolTip.GetTip(bar)?.ToString()?.Contains("Recorded cost: $2.75", StringComparison.Ordinal) == true, "Recorded cost is available in the details");
            pending.SetResult(codexStats);
            await c.UpdateLayoutAsync();
            Check(bar.Text!.Contains("claude-test", StringComparison.Ordinal), "Late statistics cannot overwrite the current row");
            await c.KeyAsync(Key.Up);
            await WorkspaceProbe.WaitAsync(() => bar.Text?.Contains("test-model", StringComparison.Ordinal) == true);
            Equal("2 user prompts · 10m tokens · test-model", bar.Text, "The footer shows the session total in millions");
            Check(ToolTip.GetTip(bar)?.ToString()?.Contains($"Total tokens: {10_000_123:N0}", StringComparison.Ordinal) == true, "The details show the exact session total");
            await c.ScreenshotAsync("session-statistics");
            SessionStatisticsFixture.CheckFooter(c.Window.Left);
            c.Window.Width = c.Window.MinWidth;
            c.Window.SetScaleForTest(1.5);
            await c.ScreenshotAsync("session-statistics-narrow");
            SessionStatisticsFixture.CheckFooter(c.Window.Left);
            c.AgentSessions.Statistics = (_, _) => throw new FileNotFoundException();
            await c.KeyAsync(Key.Down);
            Equal("Session statistics unavailable", bar.Text, "An unreadable transcript clears the previous metrics");
        }),
        new("agent-session-reader-lease", "Readers start on demand and expire independently after the last request", async c =>
        {
            await using var fixture = new SessionReaderFixture();
            Equal(0, fixture.Started, "Opening Xcmd starts no reader");
            using var claude = await fixture.ReadAsync(SessionAgent.ClaudeCode, SessionListScope.History);
            Equal(1, fixture.Started, "A Claude list starts only its reader");
            using var codex = await fixture.ReadAsync(SessionAgent.Codex, SessionListScope.History);
            using var sameCodex = await fixture.ReadAsync(SessionAgent.Codex, SessionListScope.Recent);
            Equal(codex.Id, sameCodex.Id, "Codex reuses the initialized server");
            await Task.Delay(fixture.Idle * 0.6);
            using var sameClaude = await fixture.ReadAsync(SessionAgent.ClaudeCode, SessionListScope.Recent);
            Equal(claude.Id, sameClaude.Id, "A refresh renews the Claude reader");
            await codex.WaitForExitAsync();
            Check(!claude.HasExited, "A renewed reader outlives the other reader");
            await claude.WaitForExitAsync();
            using var restarted = await fixture.ReadAsync(SessionAgent.Codex, SessionListScope.Recent);
            Equal(3, fixture.Started, "An expired server starts again on demand");
            await fixture.Readers.DisposeAsync();
            Check(restarted.HasExited, "Closing Xcmd stops the remaining reader");
        }),
        new("agent-session-refresh-focus", "Returning to a session panel updates recent sessions and keeps the selected conversation", async c =>
        {
            c.AgentSessions.AddOlderSessions(50);
            await ChooseAsync(c, Key.F1, "Codex");
            var table = Table(c.Window.Left);
            await c.KeyAsync(Key.Down);
            var selected = table.SelectedItem;
            await c.KeyAsync(Key.Tab);
            var latest = c.AgentSessions.AddCodexSession("Returned session", c.LeftPath, DateTimeOffset.UtcNow);
            var historyReads = c.AgentSessions.Reads.Count(read => read.Scope == SessionListScope.History);
            await c.KeyAsync(Key.Tab);
            await WorkspaceProbe.WaitAsync(() => Equals(table.Items[0], latest));
            Equal(selected, table.SelectedItem, "The cursor stays on the same conversation");
            Equal(historyReads, c.AgentSessions.Reads.Count(read => read.Scope == SessionListScope.History), "Returning reads only recent pages");
            var updated = c.AgentSessions.AddCodexSession("While focused", c.LeftPath, latest.Updated.AddSeconds(1));
            await WorkspaceProbe.WaitAsync(() => Equals(table.Items[0], updated));
            Equal(53, table.ItemCount, "Polling keeps older sessions and adds fresh ones");
        }),
        new("agent-session-window-return", "Both open lists check for changes when Xcmd regains focus", async c =>
        {
            c.AgentSessions.AddCodexSession("Almost one hour", c.LeftPath, DateTimeOffset.UtcNow.AddHours(-1).AddSeconds(3));
            await ChooseAsync(c, Key.F1, "Codex");
            await ChooseAsync(c, Key.F2, "Claude Code");
            InputDriver.Deactivate(c.Window);
            await c.UpdateLayoutAsync();
            Check(!c.Window.IsActive, "Another window has focus");
            var reads = c.AgentSessions.Reads.Count;
            var latest = c.AgentSessions.AddCodexSession("While away", c.LeftPath, DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(5.2));
            Equal(reads, c.AgentSessions.Reads.Count, "The background window does not poll");
            ((Window)c.Window).Activate();
            await WorkspaceProbe.WaitAsync(() => Equals(Table(c.Window.Left).Items[0], latest));
            await c.UpdateLayoutAsync();
            Equal("1 hour ago", UpdatedText(Table(c.Window.Left), 2), "Returning updates elapsed time on existing rows");
            var refreshes = c.AgentSessions.Reads.Skip(reads).ToArray();
            Check(refreshes.Contains((SessionAgent.Codex, SessionListScope.Recent)) &&
                refreshes.Contains((SessionAgent.ClaudeCode, SessionListScope.Recent)), "Each visible source refreshes on return");
        }),
        new("agent-session-newest-first", "Session headings leave the newest conversation first and reopening reads fresh sessions", async c =>
        {
            await ChooseAsync(c, Key.F1, "All Agent Sessions");
            var table = Table(c.Window.Left);
            await c.KeyAsync(Key.Down);
            foreach (var heading in new[] { "Project", "Topic", "Agent", "Size", "Updated" })
                await ClickHeaderAsync(c, table, heading);
            Equal(c.AgentSessions.Codex, table.Items[0], "The newest session stays first");
            Equal(c.AgentSessions.Claude, table.SelectedItem, "The selected session stays selected");
            var newest = c.AgentSessions.AddCodexSession("New conversation", c.LeftPath, DateTimeOffset.UtcNow);
            await ChooseAsync(c, Key.F1, "All Agent Sessions");
            Equal(newest, table.SelectedItem, "Reopening shows the new conversation first");
        }),
        new("agent-session-progressive-pages", "Recent sessions can be opened while older pages are loading", async c =>
        {
            c.AgentSessions.AddOlderSessions(75);
            var ready = c.AgentSessions.DelayOlderCodexPages();
            await OpenSourceMenuAsync(c, Key.F1);
            await c.KeyAsync(Key.Down);
            c.PressKey(Key.Enter);
            await c.UpdateLayoutAsync();
            var table = Table(c.Window.Left);
            AgentSession selected;
            try
            {
                Equal(21, table.ItemCount, "First Codex page and Claude session are visible");
                c.PressKey(Key.Down);
                selected = (AgentSession)table.SelectedItem!;
                c.PressKey(Key.Enter);
                await c.UpdateLayoutAsync();
                Equal(selected.ResumeArguments, c.Launcher.Requests.Single().Arguments, "Resume before the next page arrives");
            }
            finally { ready.TrySetResult(); }
            await c.SettleAsync();
            Equal(77, table.ItemCount, "Overlapping pages include each conversation once");
            Check(table.Items.Cast<AgentSession>().Select(row => row.Updated).SequenceEqual(
                table.Items.Cast<AgentSession>().Select(row => row.Updated).OrderDescending()), "All pages are in update order");
            Equal(selected, table.SelectedItem, "Loading older pages keeps the selected conversation");
        }),
        new("agent-session-cancel-pages", "Switching sources discards the previous pending load", async c =>
        {
            c.AgentSessions.AddOlderSessions(35);
            var ready = c.AgentSessions.DelayOlderCodexPages();
            await OpenSourceMenuAsync(c, Key.F1);
            await c.KeyAsync(Key.Down);
            c.PressKey(Key.Enter);
            await c.UpdateLayoutAsync();
            try
            {
                c.PressKey(Key.F1, RawInputModifiers.Alt);
                c.PressKey(Key.Down);
                c.PressKey(Key.Enter);
                await c.UpdateLayoutAsync();
            }
            finally { ready.TrySetResult(); }
            await c.SettleAsync();
            var table = Table(c.Window.Left);
            Equal(1, table.ItemCount, "Only the new source has rows");
            Equal(c.AgentSessions.Claude, table.SelectedItem, "The previous load cannot change the cursor");
        }),
        new("drive-source-remembers-folders", "Each drive keeps its latest folder across navigation and workspace restores", async c =>
        {
            var history = new DirectoryHistory();
            history.Restore([@"D:\old", @"C:\first"], @"D:\latest");
            for (var index = 0; index < 105; index++) history.Visit($@"C:\folder{index}");
            history.Visit(GitPath.Log(@"D:\other", ""));
            var restored = new DirectoryHistory();
            restored.Restore(history.PreviousDirectories, @"C:\folder104", history.CaptureDriveDirectories());
            Equal(@"D:\latest", restored.DirectoryForDrive(@"d:\"), "A drive remembers its folder after it leaves recent history");
            Equal(@"E:\", restored.DirectoryForDrive(@"E:\"), "A drive with no visits opens its root");

            var child = Path.Combine(c.RightPath, "temporary");
            Directory.CreateDirectory(child);
            await c.Window.Right.LoadAsync(child);
            await ChooseAsync(c, Key.F2, "Codex");
            Directory.Delete(child);
            await ChooseAsync(c, Key.F2, Path.GetPathRoot(c.RightPath)!);
            Equal(c.RightPath, c.Window.Right.Session.DirectoryPath, "A removed folder opens its nearest existing parent");
        }),
        new("agent-session-project", "Show the Git work tree and open it across panels while keeping the session selected", async c =>
        {
            var nested = Path.Combine(c.RightPath, "nested");
            Directory.CreateDirectory(nested);
            await GitFixture.RunAsync(c.RightPath, "init", "-b", "main");
            try
            {
                await using var reader = new SessionReaderFixture(nested);
                File.WriteAllBytes(Path.Combine(nested, "session.jsonl.zst"), new byte[37]);
                var rows = new List<AgentSession>();
                await reader.Readers.ListAsync(SessionAgent.Codex, SessionListScope.Recent, page => { rows.AddRange(page); return Task.CompletedTask; }, CancellationToken.None);
                var session = rows.Single();
                Equal(37L, session.TranscriptBytes, "Codex reports the size of the archived transcript on disk");
                rows.Clear();
                await reader.Readers.ListAsync(SessionAgent.ClaudeCode, SessionListScope.Recent, page => { rows.AddRange(page); return Task.CompletedTask; }, CancellationToken.None);
                Equal(2048L, rows.Single().TranscriptBytes, "Claude reports the size provided by the SDK");
                Equal(c.RightPath, session.ProjectDirectory, "The real repository root contains the launch folder");
                Equal(SessionActivity.Working, session.Activity, "The server reports an active turn");
                c.AgentSessions.AddCodexSession("Testing", nested, DateTimeOffset.UtcNow, session.ProjectDirectory, session.Activity);
                await ChooseAsync(c, Key.F2, "All Agent Sessions");
                var table = Table(c.Window.Right);
                Equal("Project", ((TextBlock)table.Columns[0].Header!).Text, "The project is the first column");
                Equal(nested, ToolTip.GetTip(Cell(table, 0, Path.GetFileName(c.RightPath))) as string, "Hover shows the full launch folder");
                Check(Cell(table, 0, "Working · Testing").IsVisible, "The topic shows the reported activity");
                Check(!Equals(Cell(table, 0, "Codex").Foreground, Cell(table, 2, "Claude").Foreground), "The agent labels have distinct colors");
                await ChooseAsync(c, Key.F1, "Codex");
                await c.KeyAsync(Key.Right, RawInputModifiers.Control);
                Equal(c.RightPath, c.Window.Right.Session.DirectoryPath, "Ctrl+Right opens the project in the opposite panel");
                Check(!c.Window.Right.ShowsAgentSessions && c.Window.Left.ShowsAgentSessions && c.Window.ActivePanel == c.Window.Left, "The source keeps its sessions and focus");
                await ChooseAsync(c, Key.F2, "Codex");
                await c.KeyAsync(Key.Left, RawInputModifiers.Control);
                Equal(c.RightPath, c.Window.Left.Session.DirectoryPath, "Ctrl+Left opens the project on the left");
            }
            finally { GitFixture.AllowCleanup(c.RightPath); }
        }),
        new("agent-session-source-keys", "The source menu starts on the current drive or session filter in each panel", async c =>
        {
            var menu = await OpenSourceMenuAsync(c, Key.F1);
            Equal(Path.GetPathRoot(c.LeftPath), SelectedSource(menu), "The left panel's drive has keyboard focus");
            await c.KeyAsync(Key.Down);
            Equal("All Agent Sessions", SelectedSource(menu), "One Down from the current drive reaches all sessions");
            await c.KeyAsync(Key.Enter);
            var table = Table(c.Window.Left);
            Equal(2, table.ItemCount, "Enter opens sessions from both agents");
            await c.KeyAsync(Key.Down);
            var session = table.SelectedItem;
            menu = await OpenSourceMenuAsync(c, Key.F1);
            Equal("All Agent Sessions", SelectedSource(menu), "The menu remembers the all-agent list");
            await c.KeyAsync(Key.Escape);
            Check(!menu.IsOpen, "Escape dismisses the menu");
            await c.KeyAsync(Key.Escape);
            Equal(session, Table(c.Window.Left).SelectedItem, "Escape keeps the selected session");
            await ChooseAsync(c, Key.F2, "Claude Code");
            menu = await OpenSourceMenuAsync(c, Key.F2);
            Equal("Claude Code", SelectedSource(menu), "Alt+F2 selects the right panel's filter");
            await c.KeyAsync(Key.Down);
            await c.KeyAsync(Key.Enter);
            Equal(c.AgentSessions.Codex, Table(c.Window.Right).SelectedItem, "Down and Enter switch the right panel to Codex");
            menu = await OpenSourceMenuAsync(c, Key.F2);
            Equal("Codex", SelectedSource(menu), "The Codex filter has focus when reopened");
            await HoverAsync(c, SourceItem(menu, "All Agent Sessions"));
            Equal("All Agent Sessions", SelectedSource(menu), "Moving the pointer changes the highlighted source");
            await c.KeyAsync(Key.Escape);
            menu = await OpenSourceMenuAsync(c, Key.F1);
            Equal("All Agent Sessions", SelectedSource(menu), "The left panel keeps its own source");
            await c.KeyAsync(Key.Escape);
        }),
        new("agent-session-updated-time", "Updated shows elapsed minutes or hours for the last 24 hours", async c =>
        {
            var row = c.AgentSessions.Codex;
            Equal("just now", row.DisplayUpdatedTime(row.Updated.AddSeconds(59)), "Less than one minute");
            Equal("1 minute ago", row.DisplayUpdatedTime(row.Updated.AddMinutes(1)), "First full minute");
            Equal("59 minutes ago", row.DisplayUpdatedTime(row.Updated.AddHours(1).AddTicks(-1)), "Last minute of the first hour");
            Equal("1 hour ago", row.DisplayUpdatedTime(row.Updated.AddHours(1)), "First full hour");
            Equal("23 hours ago", row.DisplayUpdatedTime(row.Updated.AddDays(1).AddTicks(-1)), "Last moment within 24 hours");
            Equal(row.LocalUpdatedTime, row.DisplayUpdatedTime(row.Updated.AddDays(1)), "Exactly 24 hours shows the local date and time");
            await ChooseAsync(c, Key.F1, "All Agent Sessions");
            var table = Table(c.Window.Left);
            Equal("5 minutes ago", UpdatedText(table, 0), "Recent session in the Updated column");
            Equal(c.AgentSessions.Claude.Updated.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), UpdatedText(table, 1), "Older session in the Updated column");
        }),
        new("agent-session-cursor", "Session lists use the file panel cursor for paging, hover, clicks and focus", async c =>
        {
            c.AgentSessions.AddOlderSessions(100);
            await ChooseAsync(c, Key.F2, "Codex");
            var table = Table(c.Window.Right);
            var active = RowColor(table, 0);
            var plain = RowColor(table, 1);
            Check(active != plain, "The current session is visible");
            await HoverAsync(c, table.ContainerFromIndex(1)!);
            Equal(0, table.SelectedIndex, "Hover keeps the current session");
            Equal(plain, RowColor(table, 1), "Hover leaves the row background unchanged");
            await c.KeyAsync(Key.PageDown);
            var page = table.SelectedIndex;
            Check(page > 1 && page < table.ItemCount - 1, "Page Down moves the cursor by a page");
            await c.KeyAsync(Key.PageUp);
            Equal(0, table.SelectedIndex, "Page Up moves the cursor back");
            await c.KeyAsync(Key.End);
            Equal(table.ItemCount - 1, table.SelectedIndex, "End reaches the last session");
            var last = (AgentSession)table.SelectedItem!;
            Check(table.ContainerFromIndex(table.SelectedIndex)!.IsKeyboardFocusWithin, "The last row has keyboard focus");
            await c.KeyAsync(Key.Enter);
            Equal(last.ResumeArguments, c.Launcher.Requests.Single().Arguments, "Enter resumes the row reached by keyboard");
            await c.KeyAsync(Key.Tab);
            Check(RowColor(table, table.SelectedIndex) != active, "Inactive cursor is dim");
            await c.KeyAsync(Key.Tab);
            Equal(last, table.SelectedItem, "Tab returns to the same session");
            Equal(active, RowColor(table, table.SelectedIndex), "Active cursor is restored");
            await c.KeyAsync(Key.Home);
            Equal(0, table.SelectedIndex, "Home reaches the first session");
            await c.ClickAsync(table.ContainerFromIndex(2)!);
            Equal(2, table.SelectedIndex, "Click selects the pointed session");
            await FeedbackProbe.DoubleClickAsync(c, c.Window, table.ContainerFromIndex(3)!);
            Equal(3, table.SelectedIndex, "Double click selects the pointed session");
            Equal(2, c.Launcher.Requests.Count, "Double click launches once");
            Equal(((AgentSession)table.SelectedItem!).ResumeArguments, c.Launcher.Requests.Last().Arguments, "Double click resumes that session");
        }),
        new("agent-session-panel", "Choose a panel source and resume the selected conversation", async c =>
        {
            var left = c.Window.Left;
            var right = c.Window.Right;
            c.AgentSessions.AddCodexSession("Large transcript", c.LeftPath, DateTimeOffset.UnixEpoch, transcriptBytes: 12_416L * 1024);
            c.AgentSessions.AddCodexSession("Small transcript", c.LeftPath, DateTimeOffset.UnixEpoch.AddSeconds(-1), transcriptBytes: 37);
            await ChooseAsync(c, Key.F2, "All Agent Sessions");
            Check(right.ShowsAgentSessions && !left.ShowsAgentSessions, "Alt+F2 opens sessions on the right.");
            var table = Table(right);
            Equal(5, table.Columns.Count, "Session metadata columns");
            var size = Cell(table, 0, "2 K");
            var largeSize = Cell(table, 2, $"{12_416:N0} K");
            var smallSize = Cell(table, 3, "0 K");
            var sizeHeading = table.Columns.Select(column => (TextBlock)column.Header!).Single(header => header.Text == "Size");
            Equal(TextAlignment.Right, sizeHeading.TextAlignment, "The size heading is right aligned");
            Check(new[] { size, largeSize, smallSize }.All(cell => cell.TextAlignment == TextAlignment.Right), "Transcript sizes are right aligned");
            Equal($"Main transcript: {1536:N0} bytes", ToolTip.GetTip(size), "Exact transcript size on hover");
            Equal("Transcript size unavailable", ToolTip.GetTip(Cell(table, 1, "—")), "An unknown transcript size stays distinct from zero");
            Check(Cell(table, 0, "Codex").IsVisible, "An unclassified session has no check mark");
            Equal("Press F3 to inspect the session. Choose Analyze with Luna to classify calls.", ToolTip.GetTip(Cell(table, 0, "Codex")),
                "The session list explains how to start analysis");
            c.AgentSessions.ClassificationReady = row => row.Id == c.AgentSessions.Codex.Id;
            InputDriver.Deactivate(c.Window);
            await c.UpdateLayoutAsync();
            ((Window)c.Window).Activate();
            await c.UpdateLayoutAsync();
            Equal("F3 uses saved Luna results", ToolTip.GetTip(Cell(table, 0, "Codex ✓")), "Returning from the inspector refreshes the ready mark");
            Equal(c.AgentSessions.Codex, table.SelectedItem, "Most recently updated conversation");
            await c.KeyAsync(Key.Enter);
            Equal("resume " + c.AgentSessions.Codex.Id, c.Launcher.Requests.Single().Arguments, "Resume the selected Codex thread");
            await c.KeyAsync(Key.Down);
            Check(MainMenuProbe.FileCommand(c, "Open").IsEnabled, "File Open is available for a selected session");
            await MainMenuProbe.InvokeFileCommandAsync(c, "Open");
            Equal("--resume " + c.AgentSessions.Claude.Id, c.Launcher.Requests.Last().Arguments, "Resume the selected Claude conversation");
            Equal(c.AgentSessions.Claude.Directory, c.Launcher.Requests.Last().WorkingDirectory, "Resume from the original folder");
            await c.ScreenshotAsync("agent-sessions");
            c.Window.Width = c.Window.MinWidth;
            c.Window.SetScaleForTest(1.5);
            await c.ScreenshotAsync("agent-sessions-narrow-150");
            c.Window.SetScaleForTest(1);
            c.Window.Width = 1280;
            await ChooseAsync(c, Key.F1, "Claude Code");
            Equal(1, Table(left).ItemCount, "Claude filter");
            await ChooseAsync(c, Key.F1, "Codex");
            Equal(c.AgentSessions.Codex, Table(left).SelectedItem, "Codex filter");
            await c.KeyAsync(Key.Escape);
            Check(left.ShowsAgentSessions && right.ShowsAgentSessions, "Escape keeps both session panels open.");
            var root = Path.GetPathRoot(c.RightPath)!;
            await ChooseAsync(c, Key.F2, root);
            Check(!right.ShowsAgentSessions, "Choosing a drive switches back to files.");
            Equal(c.RightPath, right.Session.DirectoryPath, "The drive opens the right panel's last folder");
            await ChooseAsync(c, Key.F1, root);
            Equal(c.LeftPath, left.Session.DirectoryPath, "The left panel remembers its own folder on the same drive");
        })
    ];
}

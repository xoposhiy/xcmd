// @tests InspectAgentSession, OpenViewers, ViewRecentAgentSessions
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Probe.Infrastructure;
using static Xcmd.Probe.Infrastructure.AgentSessionFixture;
using static Xcmd.Probe.Infrastructure.ProbeContext;

namespace Xcmd.Probe.Scenarios;

public static class SessionInspectorScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("session-inspector-classification-cache", "Classify each call once across viewers, appends and same-size edits", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            fixture.Write(103);
            Check(!await fixture.ReadyAsync(), "A session without a Luna cache has no ready mark");
            var viewers = await fixture.OpenTwoInspectorsAsync();
            Check(viewers.All(t => t.ClassificationError == null && t.Calls.Count == 103), "Both inspectors have all results");
            Equal("3,20,20,20,20,20", string.Join(',', fixture.Packets.Select(p => p.Length).Order()), "20 calls per request including the final partial batch");
            Check(fixture.Peak is > 1 and <= 5, "Requests overlap within the five-request limit");
            fixture.Reset();
            Check(await fixture.ReadyAsync(), "A complete cache from an earlier inspector is recognized");
            Check(await fixture.ReadyAsync() && fixture.Packets.Length == 0, "Checking a saved result never calls Luna");
            fixture.Offline = true;
            fixture.Append(104);
            Check(!await fixture.ReadyAsync(), "An appended call clears the ready mark");
            var partial = await fixture.ReadAsync();
            Check(partial.ClassificationError?.Contains("103 of 104") == true, "Offline viewers keep validated rows and report missing labels");
            fixture.Offline = false;
            await fixture.ReadAsync();
            Check(await fixture.ReadyAsync(), "Finishing the missing call restores the ready mark");
            Equal(106, fixture.Packets.Single().Single(), "Appending one call reuses even the last partial batch");
            fixture.Reset();
            fixture.Rewrite("component-20.cs", "component-XX.cs");
            Check(!await fixture.ReadyAsync(), "A same-size edit clears the ready mark");
            await fixture.ReadAsync();
            Equal(22, fixture.Packets.Single().Single(), "Only the edited call is classified again");
            fixture.Reset();
            fixture.Write(2);
            fixture.Offline = true;
            var shortened = await fixture.ReadAsync();
            Check(shortened.ClassificationError == null && shortened.Calls.Count == 2 && fixture.Packets.Length == 0, "Truncation uses surviving calls offline");
        }),
        new("session-inspector-classification-context", "Invalidate changed context and unknown cache formats, then resume an unreadable response", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            fixture.Write(2);
            fixture.Message("Inspect the new settings.");
            fixture.Append(3);
            await fixture.ReadAsync();
            fixture.Reset();
            fixture.Rewrite("new settings", "old settings");
            await fixture.ReadAsync();
            Equal(6, fixture.Packets.Single().Single(), "Only the call with changed message context expires");
            fixture.UseUnknownCacheFormat();
            Check(!await fixture.ReadyAsync(), "An unknown cache format has no ready mark");
            fixture.Reset();
            fixture.UnreadableReply = true;
            var incomplete = await fixture.ReadAsync();
            Check(incomplete.Calls.All(c => c.Classification == null) && incomplete.ClassificationError?.Contains("0 of 3") == true,
                "Incompatible cache results and unreadable replies cannot mark a session complete");
            Equal(4, fixture.Packets.Length, "Recheck each unresolved ID with more evidence");
            fixture.UnreadableReply = false;
            fixture.Reset();
            var complete = await fixture.ReadAsync();
            Check(complete.ClassificationError == null && fixture.Packets.Single().Length == 3, "Retry finishes the session using the current prompt");
        }),
        new("session-inspector-classification-prompt", "Keep saved results through a prompt change and retry only unfinished updates", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            fixture.Write(3);
            await fixture.ReadAsync();
            fixture.UseLegacyCache();
            fixture.Reset();
            var unchanged = await fixture.ReadAsync();
            Check(!unchanged.HasOutdatedClassifications && unchanged.ClassificationError == null && fixture.Packets.Length == 0,
                "A legacy cache with the current prompt needs no refresh");

            fixture.UseOlderPrompt();
            Check(await fixture.ReadyAsync(), "Complete saved results keep the ready mark after a prompt change");
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Update with Luna");
            Check(probe.HasText("The classification prompt changed.") && probe.HasText("Touched resources"),
                "The inspector shows the update and the saved results together");
            Equal(0, fixture.Packets.Length, "Opening the inspector does not refresh saved results");

            fixture.OmitReplyFor = 4;
            await probe.Click("Update with Luna");
            await probe.Ready("Retry analysis");
            Check(probe.HasText("2 of 3 tool calls with the current prompt") && probe.HasText("The classification prompt changed."),
                "A partial response keeps the update notice and offers a retry");
            Equal("3,4,5|4", string.Join('|', fixture.Packets.Select(ids => string.Join(',', ids))), "Update requests the old rows and retries the missing reply");
            var partial = await fixture.InspectAsync();
            Check(partial.HasOutdatedClassifications && partial.Calls.All(call => call.Classification != null),
                "Reopening keeps the saved result for the call without a new reply");
            Equal("Saved summary from the older prompt.", partial.Calls.Single(call => call.Id == 4).Classification!.Summary,
                "An unanswered call keeps its original summary");

            fixture.OmitReplyFor = null;
            fixture.Reset();
            await probe.Click("Retry analysis");
            await WorkspaceProbe.WaitAsync(() => !probe.HasText("Retry analysis"));
            Equal(4, fixture.Packets.Single().Single(), "Retry requests only the call that still has an old result");
            var updated = await fixture.InspectAsync();
            Check(!updated.HasOutdatedClassifications && !probe.HasText("The classification prompt changed.") && !probe.HasText("Update with Luna"),
                "The update notice and action go away once all results are current");
            Equal("Read and updated component 4.", updated.Calls.Single(call => call.Id == 4).Classification!.Summary,
                "The saved summary comes from the current prompt");
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-classification-claude", "Keep Claude results and linked agents in separate classification batches", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            var transcript = new InspectorTranscripts(c);
            var session = c.AgentSessions.Claude;
            var main = transcript.Claude(session);
            var trace = await fixture.ReadClaudeAsync(session);
            Equal("1,2|1", string.Join('|', fixture.Packets.Select(ids => string.Join(',', ids))), "Each transcript has its own integer IDs");
            Check(trace.Calls.All(call => call.Classification != null) && trace.Calls.Count == 3, "Only completed results are classified");
            var selection = new SessionSelection(trace) { Agent = trace.Agents.Single(a => a != SessionTrace.MainAgent) };
            Equal(1, selection.Summarize().Resources.Single(r => r.Resource.Kind == "issue").Calls.Count, "A shared issue lists only the selected agent's calls");
            fixture.Reset();
            fixture.Offline = true;
            var reopened = await fixture.ReadClaudeAsync(session);
            Check(reopened.ClassificationError == null && fixture.Packets.Length == 0, "Main and child calls reopen offline");
            Check(await fixture.ReadyAsync(session), "The ready mark includes the linked agent");
            transcript.AddClaudeChild(main);
            Check(!await fixture.ReadyAsync(session) && fixture.Packets.Length == 0, "A new linked agent clears the ready mark without calling Luna");
        }),
        new("session-inspector-classification-repair", "Repair only duplicate and generic rows, then show all classified operations", async c =>
        {
            using var fixture = new ClassificationFixture(c) { DamageFirstReply = true };
            fixture.Write(3);
            var trace = await fixture.ReadAsync();
            Equal("3,4", string.Join(',', fixture.Packets.Skip(1).SelectMany(ids => ids)), "Retry only unresolved IDs");
            var repaired = trace.Calls.Single(call => call.Id == 3).Classification!;
            var read = repaired.Pairs.Single(pair => pair.Label == new ToolLabel("source", "read"));
            Equal("settings.cs, defaults.cs", string.Join(", ", read.Objects), "Duplicate read operations keep both objects");
            var summary = new SessionSelection(trace).Summarize();
            Equal(2, summary.Activities.Count, "Distinct target/action pairs");
            Equal(3000L, summary.ToolTime, "Each call contributes once to total time");
            Check(summary.Activities.All(a => a.Calls == 3), "Both activities include every relevant call");
            fixture.Offline = true;
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Session timeline");
            await probe.Screenshot("classified-overview");
            Check(!probe.HasText("View tool calls") && !probe.HasText("Analyze with Luna"), "A fully cached session opens with the analyzed overview");
            await probe.Click("Time");
            await probe.OpenTimelineCall(0.15);
            Equal("Read and updated component 3.", probe.CallCardText("callSummary"), "The card shows the repaired summary");
            Check(probe.CallResourceNames().Contains("Component03.cs", StringComparison.Ordinal), "The card shows the saved resource");
            probe.Window.Width = 650;
            probe.Window.Height = 650;
            probe.Window.SetScaleForTest(1.5);
            await probe.Screenshot("classified-calls-150");
            await probe.Key(Key.Escape);
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-coverage-corpus", "File actions match reviewed transcript evidence across providers and working directories", CoverageCorpus.CheckAsync),
        new("session-inspector-luna-client", "An explicit review checks the evidence, caches successful replies and retries failed calls", CoverageClientFixture.CheckAsync),
        new("session-inspector-luna-replay", "Review saved Luna claims against the original calls without network access", CoverageReplay.CheckAsync),
        new("session-inspector-resources", "Show model resources for completed calls and repair invalid resource lists", async c =>
        {
            using var fixture = new ClassificationFixture(c) { CheckResourceReplies = true };
            fixture.Write(4);
            var trace = await fixture.ReadAsync();
            Equal("3,4", string.Join(',', fixture.Packets.Skip(1).SelectMany(ids => ids)), "Only missing and invalid resource lists are retried");
            var selection = new SessionSelection(trace);
            var resources = selection.Summarize().Resources;
            Equal("directory,file,issue,other,repository,url", string.Join(',', resources.Select(r => r.Resource.Kind).Distinct().Order()), "All six resource kinds are available");
            Equal(3, resources.Single(r => r.Resource.Kind == "url").Calls.Count, "A repeated URL counts once per completed call");
            var empty = trace.Calls.Single(call => call.Classification?.Resources.Length == 0);
            selection.Select(empty.Start, empty.End);
            Equal(0, selection.Summarize().Resources.Count, "A classified call can identify no resources");
            fixture.Reset();
            fixture.Offline = true;
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Touched resources");
            Check(!probe.HasText("tracker#49"), "Issue rows start hidden");
            await probe.ToggleKind("Issues");
            await probe.ChooseResource("tracker#49");
            await probe.Ready("Resource details");
            Check(probe.HasText("Read and updated component 3."), "Open an issue to see the calls that touched it");
            Equal(0, fixture.Packets.Length, "Resource details reuse the cached classification");
            await probe.Screenshot("resource-details");
            await probe.Key(Key.Escape);
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-manual-analysis", "Inspect recorded calls locally and explicitly analyze only missing results", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            fixture.Write(3);
            await ChooseAsync(c, Key.F2, "Codex");
            var probe = new InspectorProbe(c);
            await c.KeyAsync(Key.F3);
            await probe.Ready("Analyze with Luna");
            Equal(0, fixture.Packets.Length, "Opening an uncached session sends no Luna requests");
            Check(!probe.HasText("Touched resources") && probe.HasText("Started"), "The session information appears before analysis");
            await probe.Screenshot("local-overview");
            await probe.Click("Time");
            await probe.OpenTimelineCall(0.15);
            Check(probe.HasText("read component-1.cs") && probe.HasText("Unclassified"), "An unclassified call shows the recorded operation");
            var callDialog = await probe.OpenToolCall();
            var output = InspectorProbe.ToolCallPayload(callDialog, "Output");
            Check(output.Text?.Contains("done") == true, "The call opens on its recorded output");
            Check(output.FontFamily.Name == "Consolas" && output.FontSize == 15, "The output uses larger monospaced text");
            await probe.CheckCallCopy(callDialog);
            await probe.SelectCallTab(callDialog, "Input");
            Check(InspectorProbe.ToolCallPayload(callDialog, "Input").Text?.Contains("read component-1.cs") == true, "Input shows the raw tool arguments");
            await probe.CloseToolCall(callDialog);
            await probe.Key(Key.Escape);
            fixture.Offline = true;
            await probe.Click("Analyze with Luna");
            await probe.Ready("Retry analysis");
            Check(probe.HasText("Session timeline"), "A failed analysis leaves the local timeline available");
            fixture.Offline = false;
            await probe.Click("Retry analysis");
            await probe.Ready("Touched resources");
            Equal(1, fixture.Packets.Length, "Only an explicit request analyzes the three calls");
            probe.CheckRangeCalls(1);
            Check(probe.AxisSelected("Time"), "The analyzed overview keeps the selected axis");
            await probe.Key(Key.Escape);
            fixture.Append(4);
            fixture.Reset();
            await c.KeyAsync(Key.F3);
            await probe.Ready("Analyze with Luna");
            Check(probe.HasText("Component03.cs") && !probe.HasText("Component06.cs"), "A partial cache opens with its saved resources");
            Equal(0, fixture.Packets.Length, "The appended call waits for the user to start analysis");
            await probe.Click("Analyze with Luna");
            await probe.Ready("Component06.cs");
            Equal(6, fixture.Packets.Single().Single(), "The refresh sends only the appended call");
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-activity-order", "Rank activities by API cost in the selected range", async c =>
        {
            c.AgentSessions.UseActivityBreakdownTrace();
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Activity breakdown");
            Check(probe.CombineActionsOption.IsChecked == true, "The inspector combines actions by target by default");
            Check(probe.ActivityRowTop("source") < probe.ActivityRowTop("tests"), "Source ranks first by API cost");
            probe.CheckActivityNumbers("source", "$107.85", "2");
            await probe.Click("Time");
            Check(probe.ActivityRowTop("source") < probe.ActivityRowTop("tests"), "The time axis keeps the cost order");
            await probe.SortActivities("Tool time");
            Check(probe.ActivityRowTop("tests") < probe.ActivityRowTop("source"), "Nine seconds of test time rank first");
            await probe.SortActivities("Tool calls");
            Check(probe.ActivityRowTop("tests") < probe.ActivityRowTop("source"), "Three test calls rank above two source calls");
            await probe.SortActivities("Tool calls");
            Check(probe.ActivityRowTop("source") < probe.ActivityRowTop("tests"), "Selecting Calls again puts the smallest count first");
            await probe.Screenshot("activity-by-target");
            await probe.SortActivities("API cost");
            await probe.CombineActions();
            Check(probe.ActivityRowTop("source/read") < probe.ActivityRowTop("tests/exec"), "Clearing the checkbox shows actions separately");
            await probe.DragRange(0.05, 0.80);
            Check(probe.ActivityRowTop("tests/exec") < probe.ActivityRowTop("source/read"), "Test runs lead with $0.60 over $0.225 in the selected range");
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-codex", "Archived child calls and nested commands contribute once to the selected range", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.Codex();
            var trace = await fixture.Reader.ReadAsync(fixture.Session, default);
            Equal(10, trace.Calls.Count, "The outer execution request does not duplicate the command");
            var nested = trace.Calls.Single(call => call.Title == "dotnet test");
            Equal("Run the panel scenarios", trace.ResponseFor(nested)?.Record.Message, "The inner command uses the enclosing response");
            Equal(3, trace.Tasks.Count, "User requests appear as tasks");
            Equal("Locate the refresh handler", trace.Phases[0].Title, "The agent's progress explains its work");
            Equal(2, trace.Agents.Count, "Linked child in archived sessions");
            var selection = new SessionSelection(trace);
            selection.Select(trace.Start + 150000, trace.Start + 377000);
            var all = selection.Summarize();
            Equal(4, all.Results.Count, "Only results completed in the range count");
            Check(all.Activities.Sum(a => a.Milliseconds) > selection.To - selection.From, "Parallel tool time exceeds wall time");
            Equal(0L, all.OutsideTools, "Overlapping runs cover the range");
            selection.Agent = "Test runner";
            Equal(2, selection.Summarize().Results.Count, "Filter limits results to the chosen agent");
            selection.ToggleZoom();
            Check(selection.Zoomed, "Zoom to the selected time span");
            Equal(0m, trace.TotalCost, "A transcript without usage adds no cost");
        }),
        new("session-inspector-claude", "Claude records retain agent scope and completion boundaries", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.Claude(c.AgentSessions.Claude);
            var trace = await fixture.Reader.ReadAsync(c.AgentSessions.Claude, default);
            Equal(3, trace.Calls.Count, "Two main calls and one child call completed");
            Equal("claude-fixture", trace.Model, "The trace contains the recorded model");
            Check(trace.Note?.Contains("unreadable") == true && trace.Note.Contains("Awaiting"), "Partial records are reported");
            var selection = new SessionSelection(trace) { Agent = SessionTrace.MainAgent };
            Equal(0, selection.Summarize().Resources.Count, "A literal Read call cannot supply resources without a classification");
            selection.Select(trace.Start + 9000, trace.End);
            Equal(1, selection.Summarize().Results.Count, "A result at the left edge belongs to the preceding range");
        }),
        new("session-inspector-window", "F3 opens the selected session and supports range, zoom, agent and call inspection", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.Codex();
            await ChooseAsync(c, Key.F2, "Codex");
            var probe = new InspectorProbe(c);
            await c.KeyAsync(Key.F3);
            await probe.Ready("Session timeline");
            Check(probe.HasText(fixture.Session.DisplayTopic), "The selected session's topic is visible");
            Check(probe.HasText("codex-fixture") && !probe.HasText("outside tools"), "The top information panel shows the recorded model");
            Check(probe.AxisSelected("API cost"), "The timeline opens on API cost even without usage");
            await probe.Screenshot("session-overview");
            await probe.CheckSessionId(fixture.Session.Id);
            await probe.CheckSummaryFeedback();
            var sessionCalls = probe.MetricValue("Tool calls");
            var sessionCost = probe.MetricValue("API cost");
            var sessionDuration = probe.MetricValue("Duration");
            await probe.Click("Time");
            Check(probe.AxisSelected("Time") && !probe.AxisSelected("API cost"), "The Time link selects the time axis");
            await probe.DragRange(0.05, 0.07);
            probe.CheckRangeCalls(0);
            Equal(sessionCalls, probe.MetricValue("Tool calls"), "Selecting a range keeps the session's call count");
            Equal(sessionCost, probe.MetricValue("API cost"), "Selecting a range keeps the session's API cost total");
            Equal(sessionDuration, probe.MetricValue("Duration"), "Selecting a range keeps the session's duration");
            probe.FocusTimeline();
            await probe.Key(Key.Home);
            await probe.DragRange(0.45, 0.78);
            await probe.Click("Zoom to range");
            probe.FocusTimeline();
            await probe.Key(Key.Enter);
            Check(probe.Find<Button>(b => Equals(b.Content, "Reset zoom")).IsEnabled, "Enter keeps the zoom when the keyboard event is outside the range");
            await probe.Click("API cost");
            await probe.Screenshot("session-range");
            await probe.Click("Reset zoom");
            await probe.Click("Full session");
            await probe.CheckCostSteps();
            await probe.Click("Full session");
            Check(probe.HasText("Analyze with Luna") && !probe.HasText("Touched resources"), "The inspector offers analysis below the local timeline");
            var scope = probe.Find<ComboBox>(_ => true);
            scope.Focus();
            await probe.Key(Key.Down);
            await probe.Key(Key.Down);
            Check(Equals(scope.SelectedItem, "Test runner"), "The agent filter works from the keyboard");
            Equal(sessionCalls, probe.MetricValue("Tool calls"), "Filtering an agent keeps the session's call count");
            await probe.Click("Time");
            await probe.OpenTimelineCall(0.60, track: 1);
            Check(probe.HasText("dotnet test"), "Calls include the agent's actual command");
            await probe.Key(Key.Escape);
            probe.Window.Width = 650;
            probe.Window.Height = 650;
            probe.Window.SetScaleForTest(1.5);
            await probe.Screenshot("session-narrow-150");
            probe.Window.Width = probe.Window.MinWidth;
            await c.SettleAsync();
            await probe.Screenshot("session-id-minimum-width-150");
            await probe.CheckSessionId(fixture.Session.Id);
            await probe.Click("Reading guide");
            Check(probe.HasText("Use the keyboard"), "The guide explains keyboard inspection");
            await probe.Key(Key.Escape);
            await probe.PickFeedback();
            await probe.CheckSessionIdFeedback();
            await probe.Key(Key.Escape);
            Check(c.Window.SessionInspector == null && Equals(fixture.Session, Table(c.Window.Right).SelectedItem), "Escape returns to the selected session");
            Equal(0, c.Launcher.Requests.Count, "Inspecting never resumes the agent");
        }),
        new("session-inspector-codex-javascript", "Show the input and output of shell commands and standalone JavaScript calls", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.CodexJavaScript();
            var trace = await fixture.Reader.ReadAsync(fixture.Session, default);
            Equal(6, trace.Calls.Count, "The outer tool contributes script details to two commands without adding a duplicate");
            Check(trace.Responses[0].Calls.All(call => call.JavaScript == InspectorTranscripts.StoreScript),
                "Both nested commands retain the recorded script");
            Check(trace.Responses.Skip(1).All(response => response.Calls.Single().IsStandaloneJavaScript),
                "The four loads appear as individual calls");
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Session timeline");
            await probe.OpenTimelineCall(0.10);
            var nestedDialog = await probe.OpenToolCall();
            InspectorProbe.CheckToolCallTabs(nestedDialog);
            Equal("PowerShell (pwsh)", InspectorProbe.ToolCallField(nestedDialog, "toolCallShell"), "The shell executable is explained and labeled");
            Check(InspectorProbe.ToolCallField(nestedDialog, "toolCallRecord")?.Contains("run in a shell") == true,
                "The record type explains what Command execution means");
            var command = InspectorProbe.ToolCallPayload(nestedDialog, "Input");
            Check(command.Text is { } source && source.StartsWith("Get-Content ", StringComparison.Ordinal) && !source.Contains("store("),
                "A nested command opens on its own input");
            Check(command.FontFamily.Name == "Consolas" && command.FontSize == 15, "The input uses larger monospaced text");
            await c.ScreenshotAsync("tool-call-command", nestedDialog);
            await probe.CloseToolCall(nestedDialog);
            await probe.Key(Key.Escape);
            Check((await probe.TimelineTip(0.5, 0))?.Contains("JavaScript · text(load(\"read1\"));") == true,
                "The cost tooltip names the script instead of the tool alone");
            await probe.OpenTimelineCall(0.5);
            var standaloneDialog = await probe.OpenToolCall();
            InspectorProbe.CheckToolCallTabs(standaloneDialog);
            Equal("Saved output 1.", InspectorProbe.ToolCallPayload(standaloneDialog, "Output").Text, "The standalone call opens on its recorded result");
            Equal("exec", InspectorProbe.ToolCallField(standaloneDialog, "toolCallName"), "The window shows the recorded tool name");
            await probe.SelectCallTab(standaloneDialog, "Input");
            Equal("text(load(\"read1\"));", InspectorProbe.ToolCallPayload(standaloneDialog, "Input").Text,
                "A standalone JavaScript call shows the script as its input");
            await c.ScreenshotAsync("tool-call-javascript-input", standaloneDialog);
            await probe.CloseToolCall(standaloneDialog);
            await probe.Key(Key.Escape);
            await probe.Click("Time");
            await probe.OpenTimelineCall(0.59, rowY: 4);
            Check(probe.HasText("JavaScript · text(load(\"read1\"));"), "The Time axis opens the same call by its recorded interval");
        }),
        new("session-inspector-api-cost-codex", "Price each Codex response once at its model's snapshot rate", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.CodexCost();
            var trace = await fixture.Reader.ReadAsync(fixture.Session, default);
            Equal(5, trace.Responses.Count, "Each thread has its own response IDs and repeated usage adds no interval");
            Equal(1.045m, trace.TotalCost, "The session counts the long-context response, child response and final answer");
            var first = trace.Responses[0];
            Equal(0.955m, first.Cost.Usd, "Long context applies to uncached input, cache reads and writes, and output");
            Equal(800L, first.Cost.Usage?.Reasoning, "Reasoning is included in output");
            Equal("Get-Content src/Main.cs,dotnet test", string.Join(',', first.Calls.Select(call => call.Title)),
                "The response uses emission order even when results arrive in reverse order");
            Check(first.Calls[0].Start < first.Calls[1].Start && first.Calls[0].End > first.Calls[1].End,
                "The tool calls retain their recorded parallel intervals");
            Equal("Usage unavailable", trace.Responses[2].Cost.Unavailable, "A missing response stays between priced responses");
            Check(!trace.Responses[^1].Calls.Single().Completed, "A response shows a tool call while it waits for a result");
        }),
        new("session-inspector-api-cost-legacy", "Use per-response Codex totals from older transcripts", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.LegacyCodexCost();
            var trace = await fixture.Reader.ReadAsync(fixture.Session, default);
            Equal(2, trace.Responses.Count, "Repeated token count events do not add responses");
            Equal(0.08m, trace.TotalCost, "Older usage uses the dated model alias and one charge for each answer");
            Equal(1, trace.Responses[0].Calls.Count, "Usage written after a result is associated with its call");
            Equal(0, trace.Responses[1].Calls.Count, "The final message has its own charge");
        }),
        new("session-inspector-api-cost-claude", "Price Claude messages once with cache duration and context rates", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.ClaudeCost(c.AgentSessions.Claude);
            var trace = await fixture.Reader.ReadAsync(c.AgentSessions.Claude, default);
            Equal(4, trace.Responses.Count, "Content records for the same message share one charge");
            Equal(0.03575m, trace.TotalCost, "Only the priced message contributes and Fast mode uses standard rates");
            Equal(2, trace.Responses[0].Calls.Count, "Both Claude tool calls belong to the same message");
            Equal(1000L, trace.Responses[0].Cost.Usage?.Reasoning, "Thinking is part of the recorded output");
            Equal("No API rates for claude-unknown", trace.Responses[1].Cost.Unavailable, "The tooltip can explain an unknown model");
            Equal("Usage unavailable", trace.Responses[2].Cost.Unavailable, "The tooltip can explain a missing usage record");
            Equal("Cache write duration unavailable", trace.Responses[3].Cost.Unavailable, "An unknown cache rate leaves the charge unavailable");
        }),
        new("session-inspector-api-cost", "Use one whole response interval across tracks and resource rows", async c =>
        {
            var trace = SessionTimelineFixture.Use(c.AgentSessions);
            var selection = new SessionSelection(trace);
            selection.SetAxis(true);
            Equal(0.16m, trace.TotalCost, "The whole session counts every priced response once, including two messages");
            Equal(0.16m, selection.Summarize().Cost, "The full range matches the session total");
            selection.SelectResponses(0, 2);
            var grouped = selection.Summarize(true);
            Equal(0.10m, grouped.Cost, "Two adjacent responses count once each");
            Equal(0.05m, grouped.Activities.Single(row => row.Target == "source").Cost, "Source includes two equal call shares and half of the mixed call");
            Equal(0.05m, grouped.Activities.Single(row => row.Target == "tests").Cost, "Tests includes the matching shares only");
            Equal(grouped.Activities.Sum(row => row.Cost), selection.Summarize().Activities.Sum(row => row.Cost),
                "Grouping actions preserves the sum");
            Equal("1,2,3", string.Join(',', trace.Responses[0].Calls.Select(call => call.Id)), "Calls retain emission order");

            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Session timeline");
            probe.CheckTimelineHeader();
            await probe.CheckAxisLinkFeedback();
            Equal("$0.16", probe.MetricValue("API cost"), "Messages contribute to the whole-session total");
            var first = await probe.TimelineTip(0.08, 0);
            var second = await probe.TimelineTip(0.28, 0);
            var tests = await probe.TimelineTip(0.28, 1);
            Equal("exec_command\nRead the main file\nSource: Read", first, "The first Source segment describes the read");
            Equal("exec_command\nEdit the other file\nSource: Write", second, "The second Source segment describes the edit");
            Equal("exec_command\nRun the tests\nTests: Execute", tests, "The Tests segment describes the run");
            await SessionTimelineFixture.CheckJoinedHoverAsync(c, probe);
            await probe.OpenTimelineCall(0.28, 0);
            Check(probe.HasText("Selected span") && probe.HasText("Edit the other file") && probe.HasText("Run the tests"),
                "The selected span shows calls from all tracks");
            Check(probe.HasText("See Selected span below the timeline."), "The timeline points to the details after a call is selected");
            Check(!probe.HasText("Model response") && !probe.HasText("matching calls"), "The span has one heading");
            Equal("3 calls", probe.CallCardText("responseCallCount"), "The heading reports the total number of calls");
            probe.CheckCallCardLayout(sideBySide: true);
            Equal("2", probe.CallCardText("activeCallNumber"), "The card displays the clicked call's sequence number");
            Equal("900 ms", probe.SpanCallDuration(2), "Each list row shows the call duration");
            Equal("exec_command", probe.ToolCallLinkName, "The card links to the recording by tool name");
            Equal("Edit the other file", probe.CallCardText("callSummary"), "The clicked segment selects its specific tool call");
            Equal("other.cs", probe.CallCardText("callResourceName"), "One resource is visible without expanding the card");
            Equal("≈$0.02 (1/3)", probe.CallCardText("callShareValue"), "The card shows the call's estimated share of the full response cost");
            await probe.Screenshot("timeline-call-details");
            await probe.SelectSpanCall(3);
            Equal("3", probe.CallCardText("activeCallNumber"), "Selecting another row updates the card number");
            Equal("Run the tests", probe.CallCardText("callSummary"), "Selecting another row updates the card content");
            Equal(probe.SpanCallDuration(3), probe.CallCardText("callDurationValue"), "The row and card show the same duration");
            await probe.Key(Key.Escape);

            var resource = await probe.ResourceInterval("src/other.cs", 0.30, click: true);
            Check(resource?.Contains("$0.06 API cost") == true && probe.HasText("Selected span") &&
                probe.HasText("Run the tests"), "Selecting a resource interval opens the same response");
            Equal("2", probe.CallCardText("activeCallNumber"), "The resource opens on its matching call and keeps the call order");
            await probe.Key(Key.Escape);

            await probe.OpenTimelineCall(0.45, 0);
            Check(!probe.HasCallResourceDisclosure && probe.CallResourceNames() == "main.cs verify.cs",
                "Two resources appear directly in their target groups");
            probe.Window.Width = 580;
            probe.Window.SetScaleForTest(1.5);
            await probe.ShowCallCardInViewport();
            probe.CheckCallCardLayout(sideBySide: false);
            await probe.Screenshot("timeline-call-details-narrow-150");
            await probe.Key(Key.Escape);
            probe.Window.Width = 1120;
            probe.Window.SetScaleForTest(1);
            await c.SettleAsync();

            await probe.DragHandle("Range end", 0.38, () => probe.CheckRangeCalls(3));
            probe.CheckActivityNumbers("source", "$0.04", "2");
            await probe.Click("Full session");
            await probe.OpenTimelineCall(0.81, 2);
            Check(probe.HasText("Results are ready"), "A response without calls opens its message and cost");
            await probe.Key(Key.Escape);

            Equal("exec\nReview the main file\nSource: Read", await probe.TimelineTip(0.997, 0),
                "A classified JavaScript call shows its summary in its own interval");
            probe.Window.Width = 580;
            probe.Window.SetScaleForTest(1.5);
            await probe.Screenshot("session-api-cost-narrow");
            await probe.Click("Time");
            Check((await probe.TimelineTip(0.14, 0, 3))?.Contains("Read the main file") == true &&
                (await probe.TimelineTip(0.14, 0, 13))?.Contains("Edit the other file") == true,
                "Overlapping calls on one Time track keep both recorded intervals");
        }),
        new("session-inspector-release-activity", "Distinguish Git writes, builds and tests in one release call", async c =>
        {
            SessionTimelineFixture.UseReleaseTrace(c.AgentSessions);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Session timeline");
            await CheckReleaseAsync("release-activity-cost");
            probe.Window.Width = 580;
            probe.Window.SetScaleForTest(1.5);
            await probe.Click("Time");
            await CheckReleaseAsync("release-activity-time");
            await probe.OpenTimelineCall(0.37, track: 0);
            Check(probe.HasText("Committed changes and ran release checks."), "The build block opens the compound call");

            async Task CheckReleaseAsync(string screenshot)
            {
                var git = await probe.TimelineTip(0.25, 0);
                await probe.WaitForTimelineTooltip();
                Equal("exec_command\nCommitted changes and ran release checks.\nGit\nBuild\nTests", probe.TimelineTipText(),
                    "The visible tooltip has only the tool name, summary and categories");
                Equal("Write,Read", probe.TimelineTipActions("Git"), "The Git row shows both action icons");
                Equal("Execute", probe.TimelineTipActions("Build"), "The Git block also shows the Build action");
                Equal("Execute", probe.TimelineTipActions("Tests"), "The Git block also shows the Tests action");
                var tests = await probe.TimelineTip(0.25, 1);
                Equal(git, tests, "Hovering the same call on another track shows the same tooltip");
                await probe.TimelineTip(0.52, 0);
                Equal("Read,Execute", probe.TimelineTipActions("Build"), "A read and run show two different icons");
                await SessionTimelineFixture.CheckReleaseColorsAsync(c, probe, screenshot);
            }
        }),
        new("session-inspector-call-resources", "Show every resource directly in a compact group", async c =>
        {
            SessionTimelineFixture.UseResourceLists(c.AgentSessions);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Session timeline");
            await probe.OpenTimelineCall(0.15);
            Check(!probe.HasCallResourceDisclosure && probe.CallResourceNames() == "build-1.ps1 test-1.ps1",
                "Two resources need no disclosure");
            await probe.OpenTimelineCall(0.50);
            Check(!probe.HasCallResourceDisclosure && probe.CallResourceNames() == "build-1.ps1, build-2.ps1 test-1.ps1",
                "Three resources need no disclosure");
            probe.CheckResourceNamesShareLine("build-1.ps1,", "build-2.ps1");

            probe.Window.Width = 580;
            probe.Window.Height = 530;
            probe.Window.SetScaleForTest(1.5);
            await c.SettleAsync();
            await probe.OpenTimelineCall(0.83);
            Check(!probe.HasCallResourceDisclosure, "Ten resources need no disclosure");
            Equal("build-1.ps1, build-2.ps1, build-3.ps1, build-4.ps1, build-5.ps1, build-6.ps1, build-7.ps1, build-8.ps1 test-1.ps1, test-2.ps1",
                probe.CallResourceNames(), "All files appear in comma-separated groups");
            probe.CheckResourceNamesShareLine("build-1.ps1,", "build-2.ps1,");
            await probe.ShowCallCardInViewport();
            probe.CheckResourceGroupsInViewport();
            await probe.Screenshot("call-resources-150");
        }),
        new("session-inspector-span-zoom", "Double-click tasks and work phases to zoom, and right-click to reset zoom", async c =>
        {
            SessionTimelineFixture.UseZoomSpans(c.AgentSessions);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Session timeline");
            await CheckSpanZoom();
            await probe.Click("Time");
            await CheckSpanZoom();

            async Task CheckSpanZoom()
            {
                await probe.ClickTimelineSpan(0.25, task: true, MouseButton.Left, clicks: 2);
                Check(probe.HasZoomAction("Reset zoom"), "Double-clicking a task zooms to its range");
                probe.CheckRangeCalls(2);
                await probe.ClickTimelineSpan(0.25, task: false, MouseButton.Left, clicks: 2);
                Check(probe.HasZoomAction("Reset zoom"), "Double-clicking a phase zooms further into the range");
                probe.CheckRangeCalls(1);
                await probe.ClickTimelineSpan(0.5, task: false, MouseButton.Right);
                Check(probe.HasZoomAction("Zoom to range"), "Right-clicking the phase restores the full view");
                probe.CheckRangeCalls(1);
            }
        }),
        new("session-inspector-range-handles", "Refresh the range panels while either timeline handle is held", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            fixture.Write(3);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Analyze with Luna");
            await probe.Click("Analyze with Luna");
            await probe.Ready("Component05.cs");
            await probe.Click("Time");
            await probe.DragHandle("Range end", 0.67, () =>
            {
                probe.CheckRangeCalls(2);
                Equal("2", probe.ActivityCalls("source"), "The activity table counts the first two reads");
                Check(probe.HasText("Component04.cs") && !probe.HasText("Component05.cs"), "The resource list omits the last call's file");
            });
            await probe.DragHandle("Range start", 0.55, () =>
            {
                probe.CheckRangeCalls(1);
                Equal("1", probe.ActivityCalls("source"), "The activity table counts the remaining read");
                Check(probe.HasText("Component04.cs") && !probe.HasText("Component03.cs"), "The remaining file belongs to the middle call");
            });
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-modeless", "Browse sessions and open independent inspectors while another session stays open", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.Codex();
            fixture.Claude(c.AgentSessions.Claude);
            await ChooseAsync(c, Key.F1, "All Agent Sessions");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Session timeline");
            var first = probe.Window;
            await probe.Click("Reading guide");
            c.Window.Activate();
            await c.KeyAsync(Key.End);
            Equal(c.AgentSessions.Claude, Table(c.Window.Left).SelectedItem, "The workspace still accepts cursor keys");
            await c.KeyAsync(Key.F3);
            await probe.Ready("Session timeline");
            Check(probe.HasText(c.AgentSessions.Claude.DisplayTopic), "The second window shows the newly selected session");
            Equal(2, c.Window.SessionInspectors.Count, "Both sessions remain open");
            Check(!c.Window.HasOpenDialog && c.Window.SessionInspectors.All(w => w.Owner == null), "Inspectors are independent top-level windows");
            await probe.Key(Key.Escape);
            Equal(first, probe.Window, "Closing the second viewer keeps the first viewer");
            Check(probe.HasText("Use the keyboard"), "The first viewer keeps its page");
            await probe.Key(Key.Escape);
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-missing", "File menu opens an unavailable session and can retry a partial or empty local trace", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            await ChooseAsync(c, Key.F1, "Claude Code");
            await MainMenuProbe.InvokeFileCommandAsync(c, "View file, commit or session");
            var probe = new InspectorProbe(c);
            await probe.Ready("Cannot read this session");
            fixture.EmptyClaude(c.AgentSessions.Claude);
            await probe.Click("Read again");
            await probe.Ready("No model responses");
            Check(probe.AxisSelected("API cost"), "An empty session opens on API cost");
            await probe.Key(Key.Escape);
            Check(c.Window.SessionInspector == null, "A session without events can close normally");
        }),
        new("session-inspector-close-loading", "Closing an inspector stops the read and immediately restores the session list", async c =>
        {
            var cancelled = InspectorProbe.WaitForCancelledRead(c.AgentSessions);
            await ChooseAsync(c, Key.F1, "All Agent Sessions");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Reading session");
            await probe.Key(Key.Escape);
            await cancelled.WaitAsync(TimeSpan.FromSeconds(2));
            await c.KeyAsync(Key.Down);
            Equal(c.AgentSessions.Claude, Table(c.Window.Left).SelectedItem, "Keyboard navigation resumes in the source panel");
        }),
        new("session-inspector-resource-navigation", "Filter and collapse the inline tree and open a file beyond the first screen", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            fixture.Write(35);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Analyze with Luna");
            await probe.Click("Analyze with Luna");
            await probe.Ready("Touched resources");
            Check(probe.HasText("35 of 40") && !probe.HasText("tracker#49"), "Only the 35 files appear by default");
            await probe.CheckTableLayout(1120, sideBySide: true);
            await probe.ToggleGroup("Collapse src");
            Check(!probe.VisibleText("Component37.cs"), "Collapsing the directory hides its files");
            await probe.ToggleKind("URLs");
            Check(probe.HasText("example.test") && !probe.VisibleText("Component37.cs"), "Adding URLs keeps the directory collapsed");
            await probe.ToggleKind("Directories");
            await probe.ChooseResource("src");
            Check(probe.HasText("Resource details") && probe.HasText("directory"), "A collapsed directory opens its own calls");
            await probe.Key(Key.Escape);
            await probe.ToggleKind("Directories");
            await probe.ToggleGroup("Expand src");
            await probe.Screenshot("touched-resources");
            probe.Window.Height = 650;
            probe.Window.SetScaleForTest(1.5);
            await probe.CheckTableLayout(650, sideBySide: false);
            await probe.ChooseResource("src/Component37.cs");
            Check(probe.HasText("Resource details") && probe.HasText("Read and updated component 37."), "The last file opens its recorded call");
            await probe.Key(Key.Escape);
            Check(probe.HasText("36 of 40") && probe.VisibleText("Component37.cs"), "Escape returns to the filtered overview");
            await probe.ToggleKind("Files");
            Check(!probe.HasText("Component37.cs") && probe.HasText("example.test"), "Files can be hidden independently");
            await probe.ToggleKind("URLs");
            Check(probe.HasText("No resources match the selected filters"), "An empty filter has a visible explanation");
            await probe.CheckTableLayout(1120, sideBySide: true);
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-resource-order", "Order files and folders by their first matching file access", async c =>
        {
            ResourceTimelineProbe.UseOrderingTrace(c.AgentSessions);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("z-last.cs");
            await probe.ToggleKind("Directories");
            double First() => probe.ResourceRowTop("zeta/nested/z-last.cs");
            double Next() => probe.ResourceRowTop("zeta/nested/a-next.cs");
            double Other() => probe.ResourceRowTop("alpha/middle.cs");
            Check(First() < Next(), "A file whose access started first appears before a file that finished first");
            Check(Next() < Other(), "A folder follows its earliest nested file access even if another folder itself was accessed earlier");
            await probe.Click("Time");
            Check(First() < Next() && Next() < Other(), "Changing the timeline axis preserves access order");
            await probe.FilterResources("action", "Write");
            Check(Other() < Next() && Next() < First(), "The action filter reorders both folders and files by their first matching access");
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-resource-timeline", "Show each resource's classified target and the time of each action", async c =>
        {
            ResourceTimelineProbe.UseTrace(c.AgentSessions);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("project-guide.md");
            await probe.ToggleGroup("Collapse docs/d/e");
            Check(!probe.HasText("project-guide.md") && probe.HasText("main.cs"), "The compact directory chain collapses as one branch");
            await probe.ToggleGroup("Expand docs/d/e");
            await probe.ToggleKind("Directories");
            ResourceTimelineProbe.CheckRowSpacing(probe);
            await probe.ChooseResource("docs/d");
            Check(probe.HasText("Resource details") && probe.HasText("docs/d"), "A touched directory inside the chain opens its own details");
            await probe.Key(Key.Escape);
            await probe.ToggleKind("Directories");
            await ResourceTimelineProbe.CheckColumnWidthsAsync(c, probe);
            await ResourceTimelineProbe.CheckColorsAsync(c, probe);
            await probe.FilterResources("target", "Source");
            await probe.FilterResources("action", "Read");
            Check(probe.VisibleText("main.cs") && !probe.HasText("project-guide.md"), "Only source reads appear in the file tree");
            await probe.FilterResources("action", "Write");
            Check(probe.HasText("No resources match the selected filters"), "A document edit in a compound call does not count as a source edit");
            await probe.FilterResources("target");
            await probe.ChooseResource("src/main.cs");
            Check(probe.HasText("file   ·   1"), "The source file shows only its matching Other call with the Write filter");
            await probe.Key(Key.Escape);
            await probe.FilterResources("action", "Execute");
            await probe.ChooseResource("src/main.cs");
            Check(probe.HasText("file   ·   3"), "The Execute filter includes both runs and the Other call");
            await probe.Key(Key.Escape);
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-resource-filters", "Select multiple types, targets and actions without reopening their menus", async c =>
        {
            ResourceTimelineProbe.UseTrace(c.AgentSessions);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("main.cs");
            await probe.OpenResourceFilter("type");
            Check(probe.ResourceFilterChoice("Files").IsChecked == true, "The type filter starts with Files selected");
            await probe.ToggleResourceOption("Directories");
            await probe.ToggleResourceOption("URLs");
            Check(probe.HasText("example.test") && probe.ResourceFilterChoice("Directories").IsChecked == true,
                "Two type changes take effect without closing the menu");
            await probe.CloseResourceFilter();
            Check(probe.ResourceFilterButton("type").IsFocused, "Closing the menu restores focus to the filter button");
            await probe.FilterResources("target", "Source", "Docs");
            await probe.FilterResources("action", "Read", "Write");
            Check(probe.HasText("main.cs") && probe.HasText("project-guide.md") && !probe.HasText("example.test"),
                "Both targets and both actions match while the execute-only URL is hidden");
            probe.Window.Width = 650;
            probe.Window.Height = 650;
            probe.Window.SetScaleForTest(1.5);
            await c.SettleAsync();
            await probe.OpenResourceFilter("action");
            await probe.Screenshot("resource-filters-150");
            Check(probe.ResourceFilterChoice("Read").IsChecked == true && probe.ResourceFilterChoice("Write").IsChecked == true,
                "Reopening the action menu shows the current selections");
            var write = probe.ResourceFilterChoice("Write");
            write.Focus();
            await probe.Key(Key.Space);
            Check(write.IsChecked == false, "Space changes a checkbox without closing the menu");
            await probe.ToggleResourceOption("Read");
            Check(probe.HasText("No resources match the selected filters"), "Clearing the last action shows the empty state");
            await probe.ToggleResourceOption("All");
            Check(probe.HasText("example.test"), "Selecting all actions restores the execute-only URL");
            await probe.CloseResourceFilter();
            await probe.CheckResourceFilterFeedback();
            await probe.Key(Key.Escape);
        })
    ];
}

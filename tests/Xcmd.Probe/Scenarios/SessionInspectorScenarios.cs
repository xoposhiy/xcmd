// @tests InspectAgentSession, OpenViewers
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
            var viewers = await fixture.OpenTwoInspectorsAsync();
            Check(viewers.All(t => t.ClassificationError == null && t.Calls.Count == 103), "Both inspectors have all results");
            Equal("3,20,20,20,20,20", string.Join(',', fixture.Packets.Select(p => p.Length).Order()), "20 calls per request including the final partial batch");
            Check(fixture.Peak is > 1 and <= 5, "Requests overlap within the five-request limit");
            fixture.Reset();
            fixture.Offline = true;
            fixture.Append(104);
            var partial = await fixture.ReadAsync();
            Check(partial.ClassificationError?.Contains("103 of 104") == true, "Offline viewers keep validated rows and report missing labels");
            fixture.Offline = false;
            await fixture.ReadAsync();
            Equal(106, fixture.Packets.Single().Single(), "Appending one call reuses even the last partial batch");
            fixture.Reset();
            fixture.Rewrite("component-20.cs", "component-XX.cs");
            await fixture.ReadAsync();
            Equal(22, fixture.Packets.Single().Single(), "Only the edited call is classified again");
            fixture.Reset();
            fixture.Write(2);
            fixture.Offline = true;
            var shortened = await fixture.ReadAsync();
            Check(shortened.ClassificationError == null && shortened.Calls.Count == 2 && fixture.Packets.Length == 0, "Truncation uses surviving calls offline");
        }),
        new("session-inspector-classification-context", "Invalidate changed context and older prompts, then resume an unreadable response", async c =>
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
            fixture.InvalidateVersion();
            fixture.Reset();
            fixture.UnreadableReply = true;
            var incomplete = await fixture.ReadAsync();
            Check(incomplete.Calls.All(c => c.Classification == null) && incomplete.ClassificationError?.Contains("0 of 3") == true,
                "Old prompt results and unreadable replies cannot mark a session complete");
            Equal(4, fixture.Packets.Length, "Recheck each unresolved ID with more evidence");
            fixture.UnreadableReply = false;
            fixture.Reset();
            var complete = await fixture.ReadAsync();
            Check(complete.ClassificationError == null && fixture.Packets.Single().Length == 3, "Retry finishes the session using the current prompt");
        }),
        new("session-inspector-classification-claude", "Keep Claude results and linked agents in separate classification batches", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            var transcript = new InspectorTranscripts(c);
            var session = c.AgentSessions.Claude;
            transcript.Claude(session);
            var trace = await fixture.ReadClaudeAsync(session);
            Equal("1,2|1", string.Join('|', fixture.Packets.Select(ids => string.Join(',', ids))), "Each transcript has its own integer IDs");
            Check(trace.Calls.All(call => call.Classification != null) && trace.Calls.Count == 3, "Only completed results are classified");
            var selection = new SessionSelection(trace) { Agent = trace.Agents.Single(a => a != SessionTrace.MainAgent) };
            Equal(1, selection.Summarize().Resources.Single(r => r.Resource.Kind == "issue").Calls.Count, "A shared issue lists only the selected agent's calls");
            fixture.Reset();
            fixture.Offline = true;
            var reopened = await fixture.ReadClaudeAsync(session);
            Check(reopened.ClassificationError == null && fixture.Packets.Length == 0, "Main and child calls reopen offline");
        }),
        new("session-inspector-classification-repair", "Repair only duplicate and generic rows, then show all classified operations", async c =>
        {
            using var fixture = new ClassificationFixture(c) { DamageFirstReply = true };
            fixture.Write(3);
            var trace = await fixture.ReadAsync();
            Equal("3,4", string.Join(',', fixture.Packets.Skip(1).SelectMany(ids => ids)), "Retry only unresolved IDs");
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
            await probe.Click("View tool calls  ›");
            Check(probe.HasText("Read and updated component 3.") && probe.HasText("settings.cs, defaults.cs"), "The call shows its summary and merged concrete objects");
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
            await probe.ChooseResource("tracker#49");
            await probe.Ready("Resource details");
            Check(probe.HasText("Read and updated component 3."), "Open an issue to see the calls that touched it");
            Equal(0, fixture.Packets.Length, "Resource details reuse the cached classification");
            await probe.Screenshot("resource-details");
            await probe.Key(Key.Escape);
            await probe.Key(Key.Escape);
        }),
        new("session-inspector-codex", "Archived child calls and nested commands contribute once to the selected range", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.Codex();
            var trace = await fixture.Reader.ReadAsync(fixture.Session, default);
            Equal(10, trace.Calls.Count, "The outer execution request does not duplicate the command");
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
            Equal(trace.TotalTokens, trace.TokensAt(trace.End), "The token axis ends at the sum of tool tokens");
        }),
        new("session-inspector-claude", "Claude records retain agent scope and completion boundaries", async c =>
        {
            var fixture = new InspectorTranscripts(c);
            fixture.Claude(c.AgentSessions.Claude);
            var trace = await fixture.Reader.ReadAsync(c.AgentSessions.Claude, default);
            Equal(3, trace.Calls.Count, "Two main calls and one child call completed");
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
            Check(probe.Find<Button>(b => Equals(b.Content, "Est. tokens")).Classes.Contains("primary"), "The timeline opens on estimated tokens");
            await probe.Screenshot("session-overview");
            await probe.Click("Time");
            await probe.DragRange(0.05, 0.07);
            Check(probe.HasText("No tool calls finished"), "An interval inside a running tool explains the empty call list");
            probe.FocusTimeline();
            await probe.Key(Key.Home);
            await probe.DragRange(0.45, 0.78);
            await probe.Click("Zoom to range");
            probe.FocusTimeline();
            await probe.Key(Key.Enter);
            Check(probe.Find<Button>(b => Equals(b.Content, "Reset zoom")).IsEnabled, "Enter keeps the zoom when the keyboard event is outside the range");
            await probe.Click("Est. tokens");
            await probe.Screenshot("session-range");
            await probe.Click("Reset zoom");
            await probe.Click("Full session");
            await probe.CheckTokenSteps();
            await probe.Click("Full session");
            Check(probe.HasText("Resource lists unavailable"), "An unclassified trace keeps the resource state explicit");
            var scope = probe.Find<ComboBox>(_ => true);
            scope.Focus();
            await probe.Key(Key.Down);
            await probe.Key(Key.Down);
            Check(Equals(scope.SelectedItem, "Test runner"), "The agent filter works from the keyboard");
            await probe.Click("View tool calls  ›");
            Check(probe.HasText("dotnet test"), "Calls include the agent's actual command");
            await probe.Key(Key.Escape);
            probe.Window.Width = 650;
            probe.Window.Height = 650;
            probe.Window.SetScaleForTest(1.5);
            await probe.Screenshot("session-narrow-150");
            await probe.Click("Reading guide");
            Check(probe.HasText("Use the keyboard"), "The guide explains keyboard inspection");
            await probe.Key(Key.Escape);
            await probe.PickFeedback();
            await probe.Key(Key.Escape);
            Check(c.Window.SessionInspector == null && Equals(fixture.Session, Table(c.Window.Right).SelectedItem), "Escape returns to the selected session");
            Equal(0, c.Launcher.Requests.Count, "Inspecting never resumes the agent");
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
            await probe.Ready("No completed tool calls");
            Check(probe.Find<Button>(b => Equals(b.Content, "Time")).Classes.Contains("primary"), "A session without token estimates opens on time");
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
        new("session-inspector-resource-navigation", "Open a resource from a long list and return to the same catalogue", async c =>
        {
            using var fixture = new ClassificationFixture(c);
            fixture.Write(35);
            await ChooseAsync(c, Key.F2, "Codex");
            await c.KeyAsync(Key.F3);
            var probe = new InspectorProbe(c);
            await probe.Ready("Touched resources");
            await probe.Screenshot("touched-resources");
            probe.Window.Width = 650;
            probe.Window.Height = 650;
            probe.Window.SetScaleForTest(1.5);
            await probe.OpenAllResources("Browse all 40 resources  ›");
            await probe.ChooseResource("src/Component12.cs");
            Check(probe.HasText("Resource details") && probe.HasText("Read and updated component 12."), "The selected file opens its recorded call");
            await probe.Key(Key.Escape);
            Check(probe.HasText("Resources in the selected range"), "Escape returns to the full list");
            await probe.Key(Key.Escape);
            await probe.Key(Key.Escape);
        })
    ];
}

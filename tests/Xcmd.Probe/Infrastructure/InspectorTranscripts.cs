// @tests InspectAgentSession, OpenViewers
using System.Text.Json;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Probe.Infrastructure;

internal sealed class InspectorTranscripts
{
    private static readonly DateTimeOffset Epoch = new(2026, 11, 20, 10, 12, 0, TimeSpan.Zero);
    private readonly List<string> lines = [];
    private readonly string root;
    internal readonly AgentSession Session;
    internal readonly SessionTranscriptReader Reader;

    internal InspectorTranscripts(ProbeContext c)
    {
        root = Path.Combine(c.FixturePath, "inspector");
        Reader = new(root, root);
        Session = c.AgentSessions.AddCodexSession("Keep selection after panel refresh", c.LeftPath, DateTimeOffset.UtcNow);
        c.AgentSessions.Inspector = Reader.ReadAsync;
    }

    internal void Codex()
    {
        var child = Guid.Parse("33333333-1111-4111-8111-333333333333");
        User(0, "Trace the refresh handler");
        Event(1, "event_msg", new { type = "user_message", message = "Trace the refresh handler" });
        Item(2, 3, new { type = "AgentMessage", phase = "commentary", content = new[] { new { text = "Locate the refresh handler" } } });
        Command(12, 48, "Get-Content src/PanelView.cs");
        Command(63, 118, "rg selection src");
        Event(120, "response_item", new
        {
            type = "function_call",
            name = "spawn_agent",
            call_id = "agent",
            arguments = JsonSerializer.Serialize(new { task_name = "Test runner" })
        });
        Event(122, "response_item", new { type = "function_call_output", call_id = "agent", output = JsonSerializer.Serialize(new { agent_id = child }) });
        User(140, "Restore selection by path");
        Item(141, 143, new { type = "AgentMessage", phase = "commentary", content = new[] { new { text = "Restore the selected row" } } });
        Command(145, 201, "Get-Content src/PanelSelection.cs");
        Patch(218, 302, "src/PanelSelection.cs");
        User(330, "Verify and document");
        Item(330, 331, new { type = "AgentMessage", phase = "commentary", content = new[] { new { text = "Run the panel scenarios" } } });
        Event(332, "response_item", new { type = "custom_tool_call", name = "exec", call_id = "wrapper", input = "run tests" });
        Command(333, 400, "dotnet test");
        Event(401, "response_item", new { type = "custom_tool_call_output", call_id = "wrapper", output = "done" });
        Patch(420, 473, "docs/selection.md");
        Command(490, 515, "git diff");
        Save(Path.Combine(root, "sessions", "2026", "11", "20", "rollout-" + Session.Id + ".jsonl"));
        Event(149, "session_meta", new { cwd = Session.Directory });
        Command(150, 220, "Get-Content tests/PanelRefresh.cs");
        Command(255, 377, "dotnet test --filter PanelRefresh");
        Save(Path.Combine(root, "archived_sessions", "rollout-" + child + ".jsonl"));
    }

    internal string Claude(AgentSession session)
    {
        void Record(int seconds, object value)
        {
            var data = JsonSerializer.SerializeToNode(value)!.AsObject();
            data["timestamp"] = Epoch.AddSeconds(seconds).ToString("O");
            data["cwd"] = session.Directory;
            lines.Add(data.ToJsonString());
        }
        void Read(string id, int start, int end, int first, int count)
        {
            Record(start, new { type = "assistant", message = new { content = new[] { new { type = "tool_use", id, name = "Read", input = new { file_path = "src/Panel.cs" } } } } });
            Record(end, new
            {
                type = "user",
                message = new { content = new[] { new { type = "tool_result", tool_use_id = id, content = "lines" } } },
                toolUseResult = new { file = new { startLine = first, numLines = count, totalLines = 120 } }
            });
        }
        Record(0, new { type = "user", message = new { content = "Inspect the selected file" } });
        Read("one", 1, 9, 5, 6); Read("two", 12, 21, 9, 7);
        Record(25, new { type = "assistant", message = new { content = new[] { new { type = "tool_use", id = "three", name = "Read", input = new { file_path = "unfinished" } } } } });
        lines.Add("{");
        var path = Path.Combine(root, "projects", "sample", session.Id + ".jsonl");
        Save(path);
        Read("child", 3, 9, 40, 8);
        Save(Path.Combine(root, "projects", "sample", session.Id.ToString(), "subagents", "agent-review.jsonl"));
        return path;
    }

    internal void EmptyClaude(AgentSession session) =>
        Save(Path.Combine(root, "projects", "sample", session.Id + ".jsonl"));

    internal void LongFileList()
    {
        User(0, "Inspect file coverage");
        for (var i = 0; i < 35; i++) Command(1 + i * 10, 9 + i * 10, $"Get-Content src/File{i:00}.cs");
        Save(Path.Combine(root, "sessions", "rollout-" + Session.Id + ".jsonl"));
    }

    internal void AmbiguousRead()
    {
        User(0, "Check configuration and read the review notes");
        Command(1, 6, "Get-Content src/Config.cs");
        Command(7, 9, "git status --short");
        Item(10, 14, new
        {
            type = "CommandExecution",
            id = "script",
            command = new[] { "pwsh", "-Command",
            "$p = Join-Path 'src' 'Reviewed.cs'; [IO.File]::ReadAllLines($p).Length" },
            aggregated_output = "4\n",
            exit_code = 0
        });
        Save(Path.Combine(root, "sessions", "rollout-" + Session.Id + ".jsonl"));
    }

    private void User(int at, string title) => Item(at, at, new { type = "UserMessage", id = $"user{at}", content = new[] { new { text = title } } });
    private void Command(int start, int end, string command) => Item(start, end, new
    {
        type = "CommandExecution",
        id = $"command{end}",
        command = new[] { "pwsh", "-Command", command },
        aggregated_output = string.Concat(Enumerable.Repeat("    return selection.CurrentRow;\n", end - start)),
        exit_code = 0
    });
    private void Patch(int start, int end, string file) => Item(start, end, new
    {
        type = "FileChange",
        id = $"patch{end}",
        changes = new Dictionary<string, object> { [file] = new { type = "update", unified_diff = "@@ -1 +1 @@\n-old\n+new" } },
        status = "completed"
    });
    private void Item(int start, int end, object item) => Event(end, "event_msg", new
    {
        type = "item_completed",
        started_at_ms = Epoch.AddSeconds(start).ToUnixTimeMilliseconds(),
        completed_at_ms = Epoch.AddSeconds(end).ToUnixTimeMilliseconds(),
        item
    });
    private void Event(int seconds, string type, object payload) =>
        lines.Add(JsonSerializer.Serialize(new { timestamp = Epoch.AddSeconds(seconds).ToString("O"), type, payload }));
    private void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        lines.Clear();
    }
}

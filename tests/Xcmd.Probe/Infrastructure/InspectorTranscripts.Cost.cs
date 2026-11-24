// @tests InspectAgentSession
using Xcmd.Core;

namespace Xcmd.Probe.Infrastructure;

internal sealed partial class InspectorTranscripts
{
    internal const string StoreScript = "const output = await tools.exec_command({cmd: 'Get-Content a.txt'});\nstore(\"read0\", output);";

    internal void CodexJavaScript()
    {
        User(0, "Read files and reuse the output");
        Event(1, "turn_context", new { model = "gpt-6.2-sol" });
        Script(1, "store", StoreScript);
        Runtime(1, 2, "a", "Get-Content a.txt");
        Runtime(1, 3, "b", "Get-Content b.txt");
        ScriptResult(3, "store", "Stored command outputs.");
        for (var i = 0; i < 4; i++)
        {
            var id = $"read{i}";
            var at = 4 + 2 * i;
            Script(at, id, $"text(load(\"{id}\"));");
            ScriptResult(at + 1, id, $"Saved output {i}.");
        }
        Save(Path.Combine(root, "sessions", "rollout-" + Session.Id + ".jsonl"));
    }

    private void Script(int at, string id, string input)
    {
        Event(at, "response_item", new { type = "custom_tool_call", name = "exec", call_id = id, input });
        Usage(at, id, new { input_tokens = 1000, output_tokens = 100 });
    }

    private void ScriptResult(int at, string id, string text) => Event(at, "response_item", new
    {
        type = "custom_tool_call_output",
        call_id = id,
        output = new[] { new { type = "input_text", text } }
    });

    internal void CodexCost()
    {
        var child = Guid.Parse("33333333-1111-4111-8111-333333333333");
        User(0, "Compare session costs");
        Event(1, "turn_context", new { model = "gpt-6.2-sol", prompt_cache_retention = "12h" });
        Item(1, 1, new { type = "SubAgentActivity", kind = "spawned", agent_thread_id = child, agent_path = "Reviewer" });
        Tool(2, "first", "Get-Content src/Main.cs");
        Tool(3, "second", "dotnet test");
        object usage = new
        {
            input_tokens = 300000,
            cached_input_tokens = 100000,
            cache_write_input_tokens = 20000,
            output_tokens = 1000,
            reasoning_output_tokens = 800
        };
        Usage(4, "shared-id", usage);
        Runtime(3, 6, "second", "dotnet test");
        Runtime(2, 7, "first", "Get-Content src/Main.cs");
        Result(7, "first");
        Result(7, "second");
        Usage(8, "shared-id", usage);
        Message(9, "Waiting for updated results");
        Tool(10, "missing", "git status");
        Result(11, "missing");
        Event(12, "turn_context", new { model = "gpt-5.6" });
        Message(13, "The checks passed");
        Usage(14, "message", new { input_tokens = 10000, output_tokens = 1000 });
        Tool(15, "pending", "Get-Content src/Other.cs");
        Usage(16, "pending", new { input_tokens = 0, output_tokens = 0 });
        Save(Path.Combine(root, "sessions", "rollout-" + Session.Id + ".jsonl"));

        Event(4, "turn_context", new { model = "gpt-6.2-sol" });
        Message(5, "No review issues");
        Usage(6, "shared-id", new { input_tokens = 10000, output_tokens = 1000 });
        Save(Path.Combine(root, "archived_sessions", "rollout-" + child + ".jsonl"));
    }

    internal void LegacyCodexCost()
    {
        User(0, "Read older usage");
        Event(1, "turn_context", new { model = "gpt-5.4-2026-03-05" });
        Tool(2, "read", "Get-Content src/Main.cs");
        Result(4, "read");
        Legacy(5, 11000);
        Legacy(6, 11000);
        Message(7, "Read complete");
        Legacy(8, 22000);
        Save(Path.Combine(root, "sessions", "rollout-" + Session.Id + ".jsonl"));
    }

    internal void ClaudeCost(AgentSession session)
    {
        var cache = new { ephemeral_5m_input_tokens = 20000, ephemeral_1h_input_tokens = 5000 };
        object UsageAt(int output) => new
        {
            input_tokens = 20000,
            cache_read_input_tokens = 65000,
            cache_creation_input_tokens = 25000,
            cache_creation = cache,
            output_tokens = output,
            output_tokens_details = new { thinking_tokens = 1000 },
            speed = "fast"
        };
        Assistant(1, "first", "claude-haiku-5-5", UsageAt(1000), new { type = "text", text = "Read the source" });
        Assistant(2, "first", "claude-haiku-5-5", UsageAt(2000),
            new { type = "tool_use", name = "Read", id = "first-tool", input = new { file_path = "src/Main.cs" } });
        Assistant(3, "first", "claude-haiku-5-5", UsageAt(2000),
            new { type = "tool_use", name = "Read", id = "second-tool", input = new { file_path = "src/Other.cs" } });
        ToolResult(4, "second-tool");
        ToolResult(5, "first-tool");
        Assistant(6, "unknown", "claude-unknown", new { input_tokens = 10, output_tokens = 10 }, new { type = "text", text = "Unknown model" });
        Assistant(7, "missing", "claude-opus-4-5", null, new { type = "text", text = "Missing usage" });
        Assistant(8, "duration", "claude-opus-4-5", new { input_tokens = 0, output_tokens = 0, cache_creation_input_tokens = 100 },
            new { type = "text", text = "Unknown cache duration" });
        Save(Path.Combine(root, "projects", "sample", session.Id + ".jsonl"));
    }

    private void Tool(int at, string id, string command) => Event(at, "response_item", new
    {
        type = "function_call",
        name = "shell_command",
        call_id = id,
        arguments = System.Text.Json.JsonSerializer.Serialize(new { command })
    });
    private void Result(int at, string id) => Event(at, "response_item", new { type = "function_call_output", call_id = id, output = "Done" });
    private void Runtime(int from, int to, string id, string command) =>
        Item(from, to, new { type = "CommandExecution", id, command = new[] { "pwsh", "-Command", command }, exit_code = 0 });
    private void Message(int at, string text) => Event(at, "response_item", new
    {
        type = "message",
        role = "assistant",
        content = new[] { new { type = "output_text", text } }
    });
    private void Usage(int at, string response, object usage) => Event(at, "token_usage_record", new { response_id = response, usage });
    private void Legacy(int at, long total) => Event(at, "event_msg", new
    {
        type = "token_count",
        info = new
        {
            last_token_usage = new { input_tokens = 10000, output_tokens = 1000 },
            total_token_usage = new { total_tokens = total }
        }
    });
    private void Assistant(int at, string id, string model, object? usage, object content) =>
        ClaudeEvent(at, "assistant", new { id, model, usage, content = new[] { content } });
    private void ToolResult(int at, string id) =>
        ClaudeEvent(at, "user", new { content = new[] { new { type = "tool_result", tool_use_id = id, content = "Done" } } });
    private void ClaudeEvent(int at, string type, object message) =>
        lines.Add(System.Text.Json.JsonSerializer.Serialize(new { timestamp = Epoch.AddSeconds(at).ToString("O"), type, message }));
}

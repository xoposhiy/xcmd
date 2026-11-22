// @implements IntegrationVerification, ViewRecentAgentSessions
using System.Text.Json;

internal static class SessionReaderStub
{
    internal static async Task<int> RunAsync(string agent)
    {
        var initialized = false;
        var session = Guid.NewGuid();
        var title = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        while (await Console.In.ReadLineAsync() is { } line)
        {
            if (agent == "Claude")
            {
                if (line is not ("recent" or "list")) return 1;
                Console.WriteLine(JsonSerializer.Serialize(new[] { new { id = session, topic = title, cwd = "", updated = 0 } }));
                Console.WriteLine();
                continue;
            }
            using var request = JsonDocument.Parse(line);
            var root = request.RootElement;
            var method = root.GetProperty("method").GetString();
            if (method == "initialized") continue;
            object result;
            if (method == "initialize" && !initialized) { initialized = true; result = new { }; }
            else if (method == "thread/list" && initialized)
            {
                var archived = root.GetProperty("params").GetProperty("archived").GetBoolean();
                var rows = new[] { new { id = session, name = title, cwd = Environment.CurrentDirectory, updatedAt = 0, status = new { type = "active", activeFlags = Array.Empty<string>() } } };
                result = new { data = archived ? [] : rows, nextCursor = (string?)null };
            }
            else return 1;
            Console.WriteLine(JsonSerializer.Serialize(new { id = root.GetProperty("id").GetInt32(), result }));
        }
        return 0;
    }
}

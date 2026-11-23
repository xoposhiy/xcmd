// @implements InspectAgentSession
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Xcmd.Infrastructure;

internal static partial class CodexCommandResult
{
    [GeneratedRegex(@"\A(?:Chunk ID: [\w.-]+\r?\nWall time: [\d.]+ seconds\r?\nProcess exited with code (?<code>-?\d+)\r?\n(?:Original token count: \d+\r?\n)?(?:Final output|Output):|Exit code: (?<code>-?\d+)\r?\nWall time: [\d.]+ seconds\r?\nOutput:)\r?\n")]
    private static partial Regex Header();

    internal static (string Output, int? ExitCode) Read(string text, JsonElement envelope)
    {
        if (envelope.ValueKind == JsonValueKind.Object)
        {
            var status = envelope.Field("metadata").Field("exit_code");
            if (status.ValueKind != JsonValueKind.Number) status = envelope.Field("exit_code");
            return (envelope.Str("output"), status.ValueKind == JsonValueKind.Number && status.TryGetInt32(out var code) ? code : null);
        }
        var header = Header().Match(text);
        return header.Success && int.TryParse(header.Groups["code"].Value, out int exit) ? (text[header.Length..], exit) : (text, null);
    }
}

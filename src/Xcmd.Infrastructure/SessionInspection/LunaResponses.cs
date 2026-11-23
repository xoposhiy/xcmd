// @implements InspectAgentSession
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Xcmd.Infrastructure;

internal static class LunaResponses
{
    internal static async Task<string> RequestAsync(HttpClient http, string payload, string key, CancellationToken token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        message.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(message, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"OpenAI returned HTTP {(int)response.StatusCode}. Check the API key, model access and quota, then retry.");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
        if (body.RootElement.Str("status") != "completed")
            throw new InvalidDataException("Luna did not finish the request.");
        return string.Concat(body.RootElement.Field("output").Items().SelectMany(item => item.Field("content").Items())
            .Where(part => part.Str("type") == "output_text").Select(part => part.Str("text")));
    }

    internal static string? ReadKey()
    {
        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is { Length: > 0 } key) return key;
        var folder = Path.GetDirectoryName(ReleaseCatalog.ForApplication()?.LauncherExecutable) ?? AppContext.BaseDirectory;
        var file = Path.Combine(folder, ".env");
        if (!File.Exists(file)) return null;
        const string prefix = "OPENAI_API_KEY=";
        return File.ReadLines(file).Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..].Trim().Trim('"', '\'');
    }
}

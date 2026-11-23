// @implements InspectAgentSession
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed class LunaCoverageClient(string cacheDirectory, Func<string?> apiKey, HttpClient http) : ICoverageReviewer
{
    private static readonly HttpClient SharedHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(75) };
    private static readonly SemaphoreSlim[] Locks = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();
    public static LunaCoverageClient Local() => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Xcmd", "coverage", "luna"), LunaResponses.ReadKey, SharedHttp);

    public async Task<CoverageAnswer> ReviewAsync(CoverageInput input, CancellationToken token)
    {
        var payload = LunaCoveragePrompt.Request(input);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var path = Path.Combine(cacheDirectory, Convert.ToHexStringLower(hash) + ".json");
        var gate = Locks[hash[0] % Locks.Length];
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
            {
                try { return Decode(input, await File.ReadAllTextAsync(path, token).ConfigureAwait(false)); }
                catch (Exception error) when (error is JsonException or InvalidDataException) { }
            }
            var key = apiKey();
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Set OPENAI_API_KEY in the environment or in the .env file beside the Xcmd launcher.");
            var answer = await LunaResponses.RequestAsync(http, payload, key, token).ConfigureAwait(false);
            var validated = Decode(input, answer);
            Directory.CreateDirectory(cacheDirectory);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temp, answer, token).ConfigureAwait(false);
                File.Move(temp, path, true);
            }
            finally { File.Delete(temp); }
            return validated;
        }
        finally { gate.Release(); }
    }

    private static CoverageAnswer Decode(CoverageInput input, string text)
    {
        using var json = JsonDocument.Parse(text);
        return LunaCoverageValidation.Parse(input, json.RootElement);
    }
}

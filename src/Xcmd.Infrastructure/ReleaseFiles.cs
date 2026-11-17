// @implements StableShortcut, ReleaseHandoff
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Xcmd.Infrastructure;

internal sealed record VerifiedRelease(string Id, string Executable);

internal static class ReleaseFiles
{
    public static async Task<VerifiedRelease?> FindLatestAsync(string root, string executableName, string? afterId = null)
    {
        var marker = Path.Combine(root, "latest.txt");
        if (!File.Exists(marker)) return null;
        var id = (await ReadTextAsync(marker)).Trim();
        if (!Regex.IsMatch(id, @"^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$")) return null;
        if (afterId != null && StringComparer.Ordinal.Compare(id, afterId) <= 0) return null;
        return await ReadVerifiedAsync(root, id, executableName);
    }

    private static async Task<VerifiedRelease?> ReadVerifiedAsync(string root, string id, string executableName)
    {
        var directory = Path.Combine(root, id);
        var executable = Path.Combine(directory, executableName);
        var manifestPath = Path.Combine(directory, "release.json");
        var verificationPath = Path.Combine(directory, "verification.json");
        if (!File.Exists(executable) || !File.Exists(manifestPath) || !File.Exists(verificationPath)) return null;
        using var manifest = JsonDocument.Parse(await ReadTextAsync(manifestPath));
        using var verification = JsonDocument.Parse(await ReadTextAsync(verificationPath));
        if (!IsCompatible(manifest.RootElement, id) || !IsVerified(verification.RootElement)) return null;
        return new(id, executable);
    }

    private static bool IsCompatible(JsonElement manifest, string id) =>
        manifest.TryGetProperty("restartProtocol", out var protocol) && protocol.GetInt32() == 1 &&
        manifest.TryGetProperty("id", out var releaseId) && releaseId.GetString() == id;

    private static bool IsVerified(JsonElement verification) =>
        verification.TryGetProperty("total", out var total) && total.GetInt32() > 0 &&
        verification.TryGetProperty("failures", out var failures) && failures.GetInt32() == 0;

    private static async Task<string> ReadTextAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}

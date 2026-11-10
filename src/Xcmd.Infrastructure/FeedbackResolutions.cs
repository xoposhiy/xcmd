using System.Text.Json;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

public sealed record FeedbackResolution(string NoteId, string Summary);

public static class FeedbackResolutions
{
    public static async Task<FeedbackNote[]> ApplyAsync(IEnumerable<FeedbackNote> notes, string path, string releaseId)
    {
        var saved = notes.ToArray();
        if (!File.Exists(path)) return saved;
        var resolutions = JsonSerializer.Deserialize<FeedbackResolution[]>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var lookup = resolutions.ToDictionary(item => item.NoteId, item => item.Summary, StringComparer.Ordinal);
        return saved.Select(note => note.ResolvedIn == null && lookup.TryGetValue(note.Id, out var summary)
            ? note with { ResolvedIn = releaseId, Resolution = summary } : note).ToArray();
    }
}

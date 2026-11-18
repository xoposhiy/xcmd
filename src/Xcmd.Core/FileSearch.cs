// @implements FindFiles, FindFileText, PrioritizeSearch, SearchSources
using System.Text.RegularExpressions;

namespace Xcmd.Core;

public sealed record FileSearchQuery(string Root, string Name, string Text, bool MatchCase = false, bool Regex = false, bool LiteralName = false, bool WholeName = false, bool RootIsFile = false)
{
    public void Validate()
    {
        if (Name.Length == 0 && Text.Length == 0) throw new ArgumentException("Enter a file name or text to find.");
        if (Name.Length > 1024 || Text.Length > 1024) throw new ArgumentException("Search fields are limited to 1024 characters.");
        if (Regex && Text.Length > 0) _ = CreateTextRegex();
    }

    public Regex CreateTextRegex() => new(Text, RegexOptions.CultureInvariant | (MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase), TimeSpan.FromMilliseconds(100));
}

public sealed class SearchNameMatcher(FileSearchQuery query)
{
    private readonly Regex? mask = !query.LiteralName && query.Name.IndexOfAny(['*', '?']) >= 0
        ? new Regex("^" + Regex.Escape(query.Name.Replace('\\', '/')).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) : null;

    public bool Matches(FileEntry entry, string relativePath)
    {
        var value = query.Name.IndexOfAny(['/', '\\']) >= 0 ? relativePath.Replace('\\', '/') : entry.Name;
        return mask?.IsMatch(value) ?? (query.Name.Length == 0 || (query.WholeName
            ? value.Equals(query.Name, StringComparison.OrdinalIgnoreCase) : value.Contains(query.Name, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed record SearchCandidate(FileEntry Entry, bool Ignored = false, bool Tracked = false, string? IgnoredBy = null);
public sealed record SearchTextMatch(long Line, string Excerpt, int CodePage, string Text = "");
public sealed record FileSearchResult(FileEntry Entry, string RelativePath, SearchTextMatch? Match = null);
public sealed record SearchFailure(string Path, string Message, string Reason = "Read", bool Directory = false);
public sealed record DeferredSearchLocation(string Path, bool Directory, string Reason, string? Rule = null);
public sealed record FileSearchState(string Name = "", string Text = "", string[]? NameHistory = null, string[]? TextHistory = null,
    bool MatchCase = false, bool Regex = false, bool LiteralName = false, bool WholeName = false,
    ColumnState[]? ResultColumns = null, ColumnState[]? SkippedColumns = null, ColumnState[]? FailureColumns = null);

public interface IFileSearchSource : IDisposable
{
    IReadOnlyList<SearchFailure> Failures => [];
    Task<SearchCandidate> ReadFileAsync(string path, CancellationToken token) => throw new NotSupportedException("Searching a single file is not supported by this source.");
    Task<IReadOnlyList<SearchCandidate>> ReadDirectoryAsync(string path, CancellationToken token);
    Task<SearchTextMatch?> FindTextAsync(string path, FileSearchQuery query, CancellationToken token);
}

public static class FileSearchScope
{
    public static bool IsDeferred(SearchCandidate candidate) => DeferredReason(candidate) != null;
    public static string? DeferredReason(SearchCandidate candidate)
    {
        if (candidate.Tracked) return null;
        if (candidate.Ignored) return "Git ignored";
        if (candidate.Entry.Name == ".git") return "Version-control data";
        if (!candidate.Entry.IsDirectory) return null;
        return candidate.Entry.Name.ToLowerInvariant() switch
        {
            "node_modules" => "Dependencies: node_modules",
            ".hg" or ".svn" => "Version-control data",
            ".vs" => "IDE cache: .vs",
            ".venv" => "Python environment: .venv",
            "__pycache__" => "Python cache: __pycache__",
            _ => null
        };
    }
}

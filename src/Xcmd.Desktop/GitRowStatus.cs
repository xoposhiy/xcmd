using Avalonia.Media;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed record GitRowStatus(GitChange Staged, GitChange Unstaged)
{
    public static readonly GitRowStatus Empty = new(GitChange.None, GitChange.None);
    public GitStatus? Repository { get; init; }
    public bool HasChanges => Staged != GitChange.None || Unstaged != GitChange.None;
    private bool Conflict => (Staged | Unstaged).HasFlag(GitChange.Conflict);
    public string IndexGlyph => Conflict ? "!" : Glyph(Staged, staged: true);
    public string WorkingGlyph => Conflict ? "" : Repository != null && !HasChanges ? "✓" : Glyph(Unstaged, staged: false);
    public IBrush IndexBrush => Conflict || Staged.HasFlag(GitChange.Deleted) ? UiPalette.Error : UiPalette.Success;
    public IBrush WorkingBrush => Repository != null && !HasChanges ? UiPalette.Success : Unstaged.HasFlag(GitChange.Deleted) ? UiPalette.Error : Unstaged.HasFlag(GitChange.Untracked) ? UiPalette.Focus : UiPalette.Mark;
    public string Description => Repository is { } repository ? $"Git · {repository.Branch} · {(HasChanges ? "changes" : "clean")}\nWhole repository: {repository.Staged} staged, {repository.Changed} changed, {repository.Untracked} untracked, {repository.Deleted} deleted, {repository.Conflicts} conflicts\n{repository.Ahead} ahead, {repository.Behind} behind" : ChangeDescription;
    private string ChangeDescription => !HasChanges ? "No reported changes" : Conflict ? "Unresolved merge conflict" :
        string.Join("\n", new[] { Describe("Staged", Staged), Describe("Working tree", Unstaged) }.Where(text => text.Length > 0));

    private static string Glyph(GitChange value, bool staged)
    {
        if (value == GitChange.None) return "";
        if (value.HasFlag(GitChange.Untracked)) return "?";
        if (value.HasFlag(GitChange.Deleted)) return "−";
        if (value.HasFlag(GitChange.Added)) return "+";
        if (value.HasFlag(GitChange.Renamed) || value.HasFlag(GitChange.Copied)) return "→";
        return staged ? "✓" : "●";
    }

    private static string Describe(string prefix, GitChange value) => value == GitChange.None ? "" : prefix + ": " + value.ToString().Replace("TypeChanged", "type changed", StringComparison.Ordinal).ToLowerInvariant();
}

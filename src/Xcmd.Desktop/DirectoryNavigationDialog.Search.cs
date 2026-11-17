// @implements FindSavedLocation
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class DirectoryNavigationDialog
{
    private readonly NameJump nameSearch = new();
    private readonly List<KeyboardLayouts.Stroke> searchStrokes = [];
    private readonly TextBlock searchHint = new TextBlock() { Text = "Type to find · ←/→ columns", FontSize = UiPalette.MetaSize, Foreground = UiPalette.Secondary }.WithFeedbackId("DirectoryNavigationDialog.Search.searchHint");
    private bool physicalSearch = true;
    private bool suppressSearchText;
    public string NameSearchPrefix => nameSearch.Prefix;

    private void ResetNameSearch()
    {
        nameSearch.Reset(); searchStrokes.Clear(); physicalSearch = true; suppressSearchText = false;
        searchHint.Text = "Type to find · ←/→ columns";
    }

    private bool HandleSearchKey(KeyEventArgs e)
    {
        suppressSearchText = false;
        if (NameSearchPrefix.Length > 0 && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Up or Key.Down)
        {
            var matches = activeList.Items.Cast<object>().Where(MatchesSearch).ToArray();
            var index = Array.IndexOf(matches, activeList.SelectedItem);
            if (matches.Length > 0) SelectSearchResult(matches[(index + (e.Key == Key.Down ? 1 : matches.Length - 1) + matches.Length) % matches.Length]);
            return true;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) { ResetNameSearch(); return false; }
        var stroke = KeyboardLayouts.FromPhysicalSymbol(e.PhysicalKey.ToQwertyKeySymbol(false), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        if (stroke == null)
        {
            if (e.Key is not (Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt)) ResetNameSearch();
            return false;
        }
        var text = string.IsNullOrEmpty(e.KeySymbol) ? stroke.Fallback : e.KeySymbol;
        var preview = NameSearchPrefix + text;
        var strokes = searchStrokes.Append(stroke).ToArray();
        if (AppendSearch(text, physicalSearch ? new[] { preview }.Concat(KeyboardLayouts.PhysicalPrefixes(strokes)) : KeyboardLayouts.PrefixCandidates(preview))) searchStrokes.Add(stroke);
        suppressSearchText = true;
        return true;
    }

    private void HandleSearchText(TextInputEventArgs e, FeedbackController comments)
    {
        if (comments.IsPickingIn(this) || IsOpening || !(Directories.IsKeyboardFocusWithin || Favorites.IsKeyboardFocusWithin) || string.IsNullOrEmpty(e.Text)) return;
        if (suppressSearchText) suppressSearchText = false;
        else foreach (var character in e.Text.EnumerateRunes())
            {
                var text = character.ToString();
                if (AppendSearch(text, KeyboardLayouts.PrefixCandidates(NameSearchPrefix + text))) physicalSearch = false;
            }
        e.Handled = true;
    }

    private bool AppendSearch(string text, IEnumerable<string> candidates)
    {
        if (!nameSearch.Active) nameSearch.Begin();
        var items = activeList.Items.Cast<object>().ToArray();
        if (nameSearch.Append(text, candidates, items.Select(SearchText)) == null) return false;
        SelectSearchResult(items.First(MatchesSearch));
        searchHint.Text = "Find: " + NameSearchPrefix + " · ↑/↓ matches · Esc clears";
        return true;
    }

    private static string SearchText(object item) => item is FavoriteDirectory favorite ? favorite.Name + "\n" + favorite.Path : (string)item;
    private bool MatchesSearch(object item) => nameSearch.Match(SearchText(item)) != null;
    private void SelectSearchResult(object item)
    {
        activeList.SelectedItem = item;
        activeList.ScrollIntoView(item);
        FocusSelection();
    }
}

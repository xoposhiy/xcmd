// @implements OpenSearchMatch, FindViewerText, ReadWorkingFileDiff
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

public sealed partial class FileViewer
{
    public string RememberedSearch { get; set; } = "";
    public event Action<string>? SearchTextChanged;

    public async Task SearchFromResultsAsync(string text, int? codePage)
    {
        if (closed || slice == null || text.Length == 0) return;
        if (codePage is > 0)
        {
            selectedEncoding = EncodingChoice.All.FirstOrDefault(choice => choice.CodePage == codePage)
                ?? new EncodingChoice("Search encoding", codePage.Value);
            await RunAsync(token => ChangeModeAsync(FileViewMode.Text, token));
        }
        updating = true; Search.Text = text; updating = false;
        RememberSearch(text);
        searchPanel.IsVisible = true;
        await SearchAsync(0);
    }

    private CancellationTokenSource? searching;
    private FileTextMatch? match;
    private bool altSearchArmed;
    private bool suppressSearchText;
    private string altSearchText = "";

    private bool HandleSearchStart(KeyEventArgs e)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt) { altSearchArmed = true; return true; }
        suppressSearchText = false;
        if ((!altSearchArmed && !e.KeyModifiers.HasFlag(KeyModifiers.Alt)) || e.KeyModifiers.HasFlag(KeyModifiers.Control) || Mode == FileViewMode.Image || slice == null) return false;
        var stroke = KeyboardLayouts.FromPhysicalSymbol(e.PhysicalKey.ToQwertyKeySymbol(false), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        var text = !string.IsNullOrEmpty(e.KeySymbol) ? e.KeySymbol : stroke?.Fallback ?? e.Key.ToString().ToLowerInvariant();
        if (text.Length != 1 || !char.IsLetter(text[0])) return false;
        if (!IsSearchVisible) FocusSearch();
        Search.Focus(); Search.SelectedText = text;
        altSearchText = text; suppressSearchText = true;
        return true;
    }

    private bool HandleSearchKey(KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control) { FocusSearch(); return true; }
        if (e.Key == Key.F3 || (e.Key == Key.Enter && Search.IsKeyboardFocusWithin)
            || (e.Key == Key.Tab && !string.IsNullOrEmpty(Search.Text) && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift))
        { if (string.IsNullOrEmpty(Search.Text)) FocusSearch(); _ = SearchAsync(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1); return true; }
        return false;
    }

    private void SearchChanged()
    {
        PauseFollowing();
        match = null;
        diffMatchPosition = null;
        if (updating) return;
        if (string.IsNullOrEmpty(Search.Text))
        {
            searching?.Cancel();
            structuredReader.ClearSearch();
            searchStatus.Text = "";
            return;
        }
        RememberSearch(Search.Text);
        _ = SearchAsync(0);
    }

    private void RememberSearch(string text)
    { if (text.Length > 0) { RememberedSearch = text; SearchTextChanged?.Invoke(text); } }

    private void ClearSearch(bool focus = true)
    {
        altSearchArmed = false; suppressSearchText = false;
        searching?.Cancel(); match = null; diffMatchPosition = null;
        structuredReader.ClearSearch();
        updating = true; Search.Text = ""; updating = false;
        searchStatus.Text = ""; searchPanel.IsVisible = false;
        if (focus) FocusReader();
    }

    private void FocusSearch()
    {
        if (Mode == FileViewMode.Image || slice == null) return;
        if (string.IsNullOrEmpty(Search.Text) && RememberedSearch.Length > 0)
        { updating = true; Search.Text = RememberedSearch; updating = false; }
        searchPanel.IsVisible = true;
        Search.Focus(); Search.SelectAll();
    }

    private void OnSearchText(object? sender, TextInputEventArgs e)
    {
        if (feedback.IsActive || Commands.IsOpen || string.IsNullOrEmpty(e.Text)) return;
        if (suppressSearchText && string.Equals(e.Text, altSearchText, StringComparison.Ordinal))
        { suppressSearchText = false; e.Handled = true; return; }
        if (!Search.IsKeyboardFocusWithin) e.Handled = true;
    }

    private async Task SearchAsync(int direction)
    {
        if (Mode == FileViewMode.Image || slice == null) return;
        if (string.IsNullOrEmpty(Search.Text)) { FocusSearch(); return; }
        if (diff != null) { SearchDiff(direction, Search.Text); return; }
        searching?.Cancel(); loading?.Cancel();
        using var request = new CancellationTokenSource();
        searching = request; IsSearching = true;
        searchStatus.Text = "Searching…";
        var choice = selectedEncoding;
        var searchStructured = structuredReader.IsActive;
        var codePage = Mode != FileViewMode.Text ? 28591 : choice is { CodePage: > 0 } ? choice.CodePage : detected.Encoding.CodePage;
        var start = direction == 0 ? 0 : match == null ? ByteOffset : direction < 0 ? match.Offset : match.Offset + match.Length;
        try
        {
            if (direction == 0) await Task.Delay(100, request.Token);
            if (searchStructured)
            {
                await structuredReader.FindAsync(Search.Text!, direction, request.Token);
                return;
            }
            var found = await files.FindAsync(new(CurrentPath, Search.Text, codePage, start, direction < 0), request.Token);
            request.Token.ThrowIfCancellationRequested();
            match = found;
            if (found == null) { searchStatus.Text = "No matches in this file"; return; }
            var offset = found.Offset / FileSlice.PageSize * FileSlice.PageSize;
            if (found.Offset + found.Length > offset + FileSlice.PageSize) offset = found.Offset;
            await LoadPageAsync(offset, request.Token);
            request.Token.ThrowIfCancellationRequested();
            SelectMatch(found);
            searchStatus.Text = $"Match at byte {found.Offset:N0}{(found.Wrapped ? " · Wrapped" : "")}";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { if (!closed && searching == request) searchStatus.Text = "Search failed: " + failure.Message; }
        finally { if (searching == request) { searching = null; IsSearching = false; } }
    }

    private void SelectMatch(FileTextMatch found)
    {
        if (slice == null) return;
        var start = (int)(found.Offset - slice.Offset);
        int position;
        int length;
        if (Mode == FileViewMode.Text)
        {
            var choice = selectedEncoding;
            var encoding = choice is { CodePage: > 0 } ? ViewerText.GetEncoding(choice.CodePage) : detected.Encoding;
            var prefix = new FileSlice(slice.Offset, found.Offset, slice.Bytes[..start]);
            position = ViewerText.Decode(prefix, encoding).Length;
            length = encoding.GetCharCount(slice.Bytes, start, found.Length);
        }
        else
        {
            var width = Mode == FileViewMode.Hex ? 16 : 64;
            var row = start / width * width;
            var label = (slice.Offset + row).ToString("X8");
            var line = (TextContent.Text ?? "").IndexOf(label + "  ", StringComparison.Ordinal);
            var column = start % width;
            position = line + label.Length + 2 + (Mode == FileViewMode.Hex ? column * 3 + (column >= 8 ? 1 : 0) : column);
            length = Mode == FileViewMode.Hex ? 2 : Math.Min(found.Length, width - column);
        }
        TextContent.CaretIndex = position + length;
        TextContent.SelectionStart = position; TextContent.SelectionEnd = position + length;
        TextContent.UpdateLayout();
        UpdateReadingPosition();
    }
}

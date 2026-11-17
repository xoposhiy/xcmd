// @implements JumpToName
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    private bool HandleNameSearchKey(KeyEventArgs e, bool ctrl)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            ResetQuickJump();
            nameJump.Begin();
            status.Text = "Name search · type part of a name · Esc exits";
            e.Handled = true;
            return true;
        }
        if (nameJump.Active && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Up or Key.Down)
        {
            var match = nameJump.Move(ActivePanel.Current?.Name, e.Key == Key.Down, ActivePanel.Rows.Where(row => !row.Entry.IsParent).Select(row => row.Name));
            if (match != null) ActivePanel.SelectRow(ActivePanel.Rows.First(row => row.Name == match));
            e.Handled = true;
            return true;
        }
        suppressQuickText = false;
        if ((nameJump.Active || altInput) && !ctrl && JumpPhysical(e))
        {
            e.Handled = true;
            return true;
        }
        if (e.Key is not (Key.LeftShift or Key.RightShift)) ResetQuickJump();
        return false;
    }

    private readonly List<KeyboardLayouts.Stroke> quickStrokes = [];
    private readonly NameJump nameJump = new();
    public bool IsNameSearchActive => nameJump.Active;
    public string NameSearchPrefix => nameJump.Prefix;
    private bool physicalPrefix = true;
    private bool suppressQuickText;

    private void ResetQuickJump()
    {
        var active = nameJump.Active;
        nameJump.Reset(); quickStrokes.Clear(); physicalPrefix = true; suppressQuickText = false;
        if (active) { Left.ShowNameMatches(null); Right.ShowNameMatches(null); }
        if (active) status.Text = LastError ?? "Ready";
    }

    internal void StopNameSearch() => ResetQuickJump();

    private bool JumpPhysical(KeyEventArgs e)
    {
        var stroke = KeyboardLayouts.FromPhysicalSymbol(e.PhysicalKey.ToQwertyKeySymbol(false), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        if (stroke == null) return false;
        if (!nameJump.Active) nameJump.Begin();
        var text = string.IsNullOrEmpty(e.KeySymbol) ? stroke.Fallback : e.KeySymbol;
        var preview = nameJump.Prefix + text;
        var strokes = quickStrokes.Append(stroke).ToArray();
        suppressQuickText = true;
        if (ApplyQuickJump(text, physicalPrefix ? new[] { preview }.Concat(KeyboardLayouts.PhysicalPrefixes(strokes)) : KeyboardLayouts.PrefixCandidates(preview)))
            quickStrokes.Add(stroke);
        return true;
    }

    private bool ApplyQuickJump(string text, IEnumerable<string> prefixes)
    {
        var match = nameJump.Append(text, prefixes, ActivePanel.Rows.Where(row => !row.Entry.IsParent).Select(row => row.Name));
        if (match != null) { ActivePanel.ShowNameMatches(nameJump); ActivePanel.SelectRow(ActivePanel.Rows.First(row => row.Name == match)); }
        status.Text = nameJump.Prefix.Length == 0 ? "Name search · type part of a name · Esc exits" : "Jump to: " + nameJump.Prefix + " · ↑/↓ matches · Esc exits";
        return match != null;
    }

    private void JumpText(string text)
    {
        foreach (var character in text.EnumerateRunes())
        {
            var suffix = character.ToString();
            if (ApplyQuickJump(suffix, KeyboardLayouts.PrefixCandidates(nameJump.Prefix + suffix))) physicalPrefix = false;
        }
    }
}

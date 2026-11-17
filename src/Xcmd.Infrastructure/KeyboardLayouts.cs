// @implements KeyboardLayouts
using System.Runtime.InteropServices;
using System.Text;

namespace Xcmd.Infrastructure;

public static class KeyboardLayouts
{
    public sealed record Stroke(uint ScanCode, bool Shift, string Fallback);

    public static Stroke? FromPhysicalSymbol(string? symbol, bool shift)
    {
        if (string.IsNullOrEmpty(symbol) || symbol.Length != 1) return null;
        var key = char.ToLowerInvariant(symbol[0]);
        var rows = new[] { ("1234567890-=", 0x02u), ("qwertyuiop[]", 0x10u), ("asdfghjkl;'", 0x1Eu), ("zxcvbnm,./", 0x2Cu) };
        foreach (var (letters, start) in rows)
        {
            var index = letters.IndexOf(key);
            if (index >= 0) return new(start + (uint)index, shift, shift ? symbol.ToUpperInvariant() : symbol);
        }
        return key == ' ' ? new(0x39, shift, " ") : null;
    }

    public static string[] PhysicalPrefixes(IReadOnlyList<Stroke> strokes)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Concat(strokes.Select(stroke => stroke.Fallback)) };
        if (!OperatingSystem.IsWindows()) return result.ToArray();
        var layouts = new nint[GetKeyboardLayoutList(0, null)]; GetKeyboardLayoutList(layouts.Length, layouts);
        foreach (var layout in layouts)
        {
            var text = new StringBuilder(); var valid = true;
            foreach (var stroke in strokes)
            {
                var state = new byte[256]; if (stroke.Shift) state[0x10] = 0x80;
                var buffer = new StringBuilder(16);
                var length = ToUnicodeEx(MapVirtualKeyEx(stroke.ScanCode, 3, layout), stroke.ScanCode, state, buffer, buffer.Capacity, 4, layout);
                if (length <= 0) { valid = false; break; }
                text.Append(buffer.ToString(0, length));
            }
            if (valid && text.Length > 0) result.Add(text.ToString());
        }
        return result.ToArray();
    }

    public static string[] PrefixCandidates(string text)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { text };
        if (!OperatingSystem.IsWindows()) return candidates.ToArray();
        var layouts = new nint[GetKeyboardLayoutList(0, null)];
        GetKeyboardLayoutList(layouts.Length, layouts);
        foreach (var source in layouts)
            foreach (var target in layouts)
            {
                if (source == target) continue;
                var converted = Convert(text, source, target);
                if (!string.IsNullOrEmpty(converted)) candidates.Add(converted);
            }
        return candidates.ToArray();
    }

    private static string? Convert(string text, nint source, nint target)
    {
        var result = new StringBuilder();
        foreach (var character in text)
        {
            var key = VkKeyScanEx(character, source);
            if (key == -1) return null;
            var scan = MapVirtualKeyEx((uint)(key & 255), 0, source);
            var targetKey = MapVirtualKeyEx(scan, 3, target);
            var state = new byte[256];
            var modifiers = (key >> 8) & 255;
            if ((modifiers & 1) != 0) state[0x10] = 0x80;
            if ((modifiers & 2) != 0) state[0x11] = 0x80;
            if ((modifiers & 4) != 0) state[0x12] = 0x80;
            var buffer = new StringBuilder(16);
            // Flag 4 keeps dead-key state in the real keyboard input stream unchanged.
            var length = ToUnicodeEx(targetKey, scan, state, buffer, buffer.Capacity, 4, target);
            if (length <= 0) return null;
            result.Append(buffer.ToString(0, length));
        }
        return result.ToString();
    }

    [DllImport("user32.dll")] private static extern int GetKeyboardLayoutList(int count, [Out] nint[]? layouts);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "VkKeyScanExW")] private static extern short VkKeyScanEx(char character, nint layout);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW")] private static extern uint MapVirtualKeyEx(uint code, uint mapType, nint layout);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ToUnicodeEx(uint key, uint scan, byte[] state, StringBuilder text, int count, uint flags, nint layout);
}

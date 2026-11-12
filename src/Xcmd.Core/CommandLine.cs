namespace Xcmd.Core;

public sealed record CommandLineState(string Draft, string[] History,
    bool Focused = false, int Caret = 0, int SelectionStart = 0, int SelectionEnd = 0);

public sealed record CommandLineAction(string Target, string Arguments, bool ChangesDirectory);

public static class CommandLine
{
    public const int MaximumLength = 4096;

    public static CommandLineAction Parse(string text)
    {
        var command = text.Trim();
        if (command.Length == 0) throw new ArgumentException("Enter a program, file, or cd command.");
        if (command.Length > MaximumLength || command.Any(char.IsControl)) throw new ArgumentException("The command is too long or contains control characters.");
        return IsDirectoryChange(command) ? ParseDirectoryChange(command) : ParseProgram(command);
    }

    private static bool IsDirectoryChange(string command)
    {
        if (!command.StartsWith("cd", StringComparison.OrdinalIgnoreCase)) return false;
        if (command.Length == 2) return true;
        return char.IsWhiteSpace(command[2]) || command[2] is '.' or '\\' or '/';
    }

    private static CommandLineAction ParseDirectoryChange(string command)
    {
        var directory = command[2..].Trim();
        if (directory.Equals("/d", StringComparison.OrdinalIgnoreCase)) directory = "";
        else if (directory.StartsWith("/d ", StringComparison.OrdinalIgnoreCase)) directory = directory[3..].TrimStart();
        return new(Unquote(directory), "", true);
    }

    private static CommandLineAction ParseProgram(string command)
    {
        if (command[0] == '"')
        {
            var end = command.IndexOf('"', 1);
            if (end < 0 || end == 1 || end + 1 < command.Length && !char.IsWhiteSpace(command[end + 1]))
                throw new ArgumentException("Close the program path quotes and separate arguments with a space.");
            return new(command[1..end], command[(end + 1)..].TrimStart(), false);
        }
        var separator = command.IndexOfAny([' ', '\t']);
        return separator < 0 ? new(command, "", false) : new(command[..separator], command[(separator + 1)..].TrimStart(), false);
    }

    public static string DirectoryPath(string target, string currentDirectory) =>
        target.Length == 0 ? currentDirectory : Path.GetFullPath(target, currentDirectory);

    public static string FormatArgument(string value)
    {
        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"')) return value;
        var quoted = new System.Text.StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            quoted.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character);
            slashes = 0;
        }
        return quoted.Append('\\', slashes * 2).Append('"').ToString();
    }

    private static string Unquote(string value)
    {
        if (!value.Contains('"')) return value;
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"' || value[1..^1].Contains('"'))
            throw new ArgumentException("Use one pair of quotes around the directory path.");
        return value[1..^1];
    }
}

public sealed class CommandHistory
{
    private readonly List<string> items = [];
    private int position;
    private string draft = "";
    public string[] Capture() => items.ToArray();
    public void Restore(IEnumerable<string> history)
    {
        items.Clear(); items.AddRange(history.TakeLast(100)); Reset();
    }
    public void Add(string command)
    {
        items.RemoveAll(item => item.Equals(command, StringComparison.Ordinal));
        items.Add(command);
        if (items.Count > 100) items.RemoveAt(0);
        Reset();
    }
    public void Reset() => position = items.Count;
    public string Browse(string current, bool older)
    {
        if (position == items.Count) draft = current;
        var next = position + (older ? -1 : 1);
        while (next >= 0 && next < items.Count && !items[next].StartsWith(draft, StringComparison.OrdinalIgnoreCase)) next += older ? -1 : 1;
        if (next < 0) return position == items.Count ? draft : items[position];
        position = Math.Min(next, items.Count);
        return position == items.Count ? draft : items[position];
    }
}

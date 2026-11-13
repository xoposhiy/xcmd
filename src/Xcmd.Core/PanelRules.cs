namespace Xcmd.Core;

public sealed record FileEntry(string FullPath, string Name, bool IsDirectory, long Size, DateTime ModifiedUtc, bool IsParent = false, bool IsLink = false)
{
    public string Extension => IsDirectory || (Name.StartsWith('.') && Name.IndexOf('.', 1) < 0)
        ? "" : Path.GetExtension(Name).TrimStart('.');
}

public enum SortField { Name, Extension, Size, Modified }

public sealed record SortOrder(SortField Field = SortField.Name, bool Descending = false);

public static class PanelRules
{
    public static string ParentCursorName(string directoryPath)
    {
        var child = ArchivePath.IsEntry(directoryPath) && ArchivePath.EntryName(directoryPath).Length == 0
            ? ArchivePath.Source(directoryPath) : directoryPath;
        var name = ArchivePath.IsEntry(child) ? ArchivePath.EntryName(child) : child;
        return Path.GetFileName(Path.TrimEndingDirectorySeparator(name.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static FileEntry? ParentEntry(string directoryPath)
    {
        var parent = ArchivePath.Parent(directoryPath);
        return parent == null ? null : new FileEntry(parent, "..", true, 0, DateTime.MinValue, IsParent: true);
    }

    public static IReadOnlyList<FileEntry> Sort(IEnumerable<FileEntry> entries, SortOrder order) =>
        entries.Order(Comparer<FileEntry>.Create((left, right) => Compare(left, right, order))).ToArray();

    private static int Compare(FileEntry left, FileEntry right, SortOrder order)
    {
        if (left.IsParent != right.IsParent) return left.IsParent ? -1 : 1;
        if (left.IsDirectory != right.IsDirectory)
            return left.IsDirectory ? -1 : 1;
        // Directory placement and directory name order do not change with the file sort direction.
        if (left.IsDirectory)
            return NaturalCompare(left.Name, right.Name);
        var comparison = order.Field switch
        {
            SortField.Extension => StringComparer.OrdinalIgnoreCase.Compare(left.Extension, right.Extension),
            SortField.Size => left.Size.CompareTo(right.Size),
            SortField.Modified => left.ModifiedUtc.CompareTo(right.ModifiedUtc),
            _ => NaturalCompare(left.Name, right.Name)
        };
        if (comparison == 0)
            comparison = NaturalCompare(left.Name, right.Name);
        return order.Descending ? -Math.Sign(comparison) : comparison;
    }

    public static int NaturalCompare(string left, string right)
    {
        int a = 0, b = 0;
        while (a < left.Length && b < right.Length)
        {
            if (char.IsAsciiDigit(left[a]) && char.IsAsciiDigit(right[b]))
            {
                var result = CompareNumberParts(left, ref a, right, ref b);
                if (result != 0) return result;
            }
            else
            {
                var result = char.ToUpperInvariant(left[a++]).CompareTo(char.ToUpperInvariant(right[b++]));
                if (result != 0) return result;
            }
        }
        var lengthResult = (left.Length - a).CompareTo(right.Length - b);
        return lengthResult != 0 ? lengthResult : StringComparer.Ordinal.Compare(left, right);
    }

    private static int CompareNumberParts(string left, ref int a, string right, ref int b)
    {
        var digitsA = ReadNumberPart(left, ref a);
        var digitsB = ReadNumberPart(right, ref b);
        var result = digitsA.Length.CompareTo(digitsB.Length);
        return result != 0 ? result : digitsA.CompareTo(digitsB, StringComparison.Ordinal);
    }

    private static ReadOnlySpan<char> ReadNumberPart(string text, ref int index)
    {
        var start = index;
        while (index < text.Length && char.IsAsciiDigit(text[index])) index++;
        return text.AsSpan(start, index - start).TrimStart('0');
    }

    public static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Enter a name.";
        if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Use a single valid file name.";
        if (name.EndsWith(' ') || name.EndsWith('.')) return "A name cannot end with a space or a dot.";
        if (name.Length > 255) return "A name cannot exceed 255 characters.";
        if (IsReservedName(name))
            return "This name is reserved by Windows.";
        return null;
    }

    private static bool IsReservedName(string name)
    {
        var stem = name.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }.Contains(stem, StringComparer.OrdinalIgnoreCase)) return true;
        if (stem.Length != 4) return false;
        if (!stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && !stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) return false;
        return "123456789¹²³".Contains(stem[3]);
    }
}

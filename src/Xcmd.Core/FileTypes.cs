// @implements RecognizeFileTypes
namespace Xcmd.Core;

public sealed record FileTypeRule(string Name, string Color, string[] Extensions, string[] FileNames);
public sealed record FileTypeConfiguration(FileTypeRule[] Rules);

public sealed class FileTypes
{
    private readonly (FileTypeRule Rule, HashSet<string> Extensions, HashSet<string> Names)[] rules;
    public FileTypes(FileTypeConfiguration configuration)
    {
        if (configuration.Rules == null || configuration.Rules.Any(rule => rule == null || string.IsNullOrWhiteSpace(rule.Name) ||
            rule.Color == null || rule.Color.Length != 7 || rule.Color[0] != '#' || !rule.Color.Skip(1).All(Uri.IsHexDigit) ||
            rule.Extensions == null || rule.FileNames == null || rule.Extensions.Any(extension => string.IsNullOrWhiteSpace(extension) || !extension.StartsWith('.')) ||
            rule.FileNames.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException("File type rules need a name, a #RRGGBB color, extensions and file names.");
        rules = configuration.Rules.Select(rule => (rule, new HashSet<string>(rule.Extensions, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(rule.FileNames, StringComparer.OrdinalIgnoreCase))).ToArray();
    }

    public FileTypeRule? Match(string name, bool directory = false)
    {
        if (directory) return null;
        var extension = Path.GetExtension(name);
        foreach (var (rule, extensions, names) in rules)
            if (names.Contains(name) || extensions.Contains(extension)) return rule;
        return null;
    }
}

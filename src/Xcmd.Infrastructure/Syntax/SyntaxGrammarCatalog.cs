// @implements HighlightSource, SyntaxSpans
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

internal sealed class SyntaxGrammarCatalog : IRegistryOptions
{
    private readonly RegistryOptions bundled = new(ThemeName.DarkPlus);
    public IReadOnlyList<SyntaxLanguage> Languages { get; }

    public SyntaxGrammarCatalog()
    {
        Languages = bundled.GetAvailableLanguages().DistinctBy(language => language.Id)
            .Select(language => new SyntaxLanguage(language.Id, language.Aliases[0], bundled.GetScopeByLanguageId(language.Id)))
            .Concat([new("kotlin", "Kotlin", "source.kotlin"), new("toml", "TOML", "source.toml")])
            .OrderBy(language => language.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public SyntaxLanguage? Detect(string path, string text)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(".kt", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".kts", StringComparison.OrdinalIgnoreCase)) return Find("kotlin");
        if (name.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)) return Find("toml");
        var language = bundled.GetAvailableLanguages()
            .SelectMany(language => (language.Extensions ?? []).Select(extension => (Language: language, Extension: extension)))
            .Where(item => name.EndsWith(item.Extension, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Extension.Length).Select(item => item.Language).FirstOrDefault();
        if (language != null) return Find(language.Id);
        var named = name.ToLowerInvariant() switch
        {
            "dockerfile" or "containerfile" => "dockerfile",
            "makefile" or "gnumakefile" => "makefile",
            ".editorconfig" or ".gitconfig" or ".gitmodules" => "properties",
            _ => null
        };
        if (named != null) return Find(named);
        if (!text.StartsWith("#!", StringComparison.Ordinal)) return null;
        var firstLine = text.Split('\n', 2)[0];
        if (firstLine.Contains("python", StringComparison.Ordinal)) return Find("python");
        if (firstLine.Contains("pwsh", StringComparison.Ordinal)) return Find("powershell");
        if (firstLine.Contains("sh", StringComparison.Ordinal)) return Find("shellscript");
        return null;
    }

    public SyntaxLanguage? Find(string id) => Languages.FirstOrDefault(language => language.Id == id);
    public IRawGrammar GetGrammar(string scopeName)
    {
        var resource = scopeName switch { "source.kotlin" => "kotlin", "source.toml" => "toml", _ => null };
        if (resource == null) return bundled.GetGrammar(scopeName);
        using var stream = typeof(SyntaxGrammarCatalog).Assembly.GetManifestResourceStream("Xcmd.Syntax." + resource + ".json")
            ?? throw new InvalidDataException("Missing syntax grammar: " + resource);
        using var reader = new StreamReader(stream);
        return GrammarReader.ReadGrammarSync(reader);
    }

    public IRawTheme GetTheme(string scopeName) => bundled.GetTheme(scopeName);
    public IRawTheme GetDefaultTheme() => bundled.GetDefaultTheme();
    public ICollection<string> GetInjections(string scopeName) => bundled.GetInjections(scopeName);
}

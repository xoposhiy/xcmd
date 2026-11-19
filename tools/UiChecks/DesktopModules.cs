// @implements QualityChecks
namespace UiChecks;

internal static class DesktopModules
{
    private const string NamespacePrefix = "Xcmd.Desktop.";
    private const string Workspace = "Workspace";
    private const string Shared = "Shared";
    private const string Panels = "Features.Panels";
    private const string Feedback = "Features.Feedback";

    private static readonly Dictionary<string, string[]> FeatureDependencies = new(StringComparer.Ordinal)
    {
        ["Features.FileOperations"] = [Panels, Feedback],
        ["Features.Viewer"] = [Panels, Feedback],
        ["Features.Git"] = [Panels],
        ["Features.CommandLine"] = [Panels],
        ["Features.Navigation"] = [Feedback],
        ["Features.Search"] = [Feedback],
        [Panels] = [],
        [Feedback] = [],
    };

    internal static IEnumerable<string> Names => FeatureDependencies.Keys.Append(Workspace).Append(Shared);

    internal static string? FromNamespace(string name) => name.StartsWith(NamespacePrefix, StringComparison.Ordinal)
        ? FromSegments(name[NamespacePrefix.Length..].Split('.')) : null;

    internal static string? FromFile(string projectDirectory, string filePath)
    {
        if (!Path.IsPathFullyQualified(projectDirectory) || !Path.IsPathFullyQualified(filePath)) return null;
        var folders = Path.GetDirectoryName(Path.GetRelativePath(projectDirectory, filePath));
        return folders == null ? null : FromSegments(folders.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static string? FromSegments(string[] segments) => segments switch
    {
        [Workspace or Shared, ..] => segments[0],
        ["Features", var feature, ..] when FeatureDependencies.ContainsKey("Features." + feature) => "Features." + feature,
        _ => null,
    };

    internal static bool CanReference(string source, string target) => source == target || source == Workspace || target == Shared
        || FeatureDependencies.TryGetValue(source, out var targets) && targets.Contains(target, StringComparer.Ordinal);
}

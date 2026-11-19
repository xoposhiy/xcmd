// @tests QualityChecks
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UiChecks;

internal static class DesktopArchitectureChecks
{
    private const string WorkspaceType = "namespace Xcmd.Desktop.Workspace; internal class MainWindow { }";
    private const string SearchPath = "Features/Search/SearchWindow.cs";
    private const string WorkspacePath = "Workspace/MainWindow.cs";
    private const string SharedPath = "Shared/Ui/Controls.cs";

    internal static async Task CheckAsync(MetadataReference[] references)
    {
        CheckModuleOrder();
        await CheckSourcesAsync(references,
        [
            (WorkspacePath, "namespace Xcmd.Desktop.Workspace; internal class MainWindow { internal Features.Search.SearchWindow Search = new(); }"),
            (SearchPath, "namespace Xcmd.Desktop.Features.Search; internal class SearchWindow { internal Features.Feedback.Surface Surface = new(); }"),
            ("Features/Feedback/Surface.cs", "namespace Xcmd.Desktop.Features.Feedback; internal class Surface { internal Shared.Ui.Controls View = new(); }"),
            (SharedPath, "namespace Xcmd.Desktop.Shared.Ui; internal class Controls { }")
        ], []);
        await CheckSourcesAsync(references,
        [
            (WorkspacePath, WorkspaceType),
            (SearchPath, "using Window = Xcmd.Desktop.Workspace.MainWindow; namespace Xcmd.Desktop.Features.Search; internal class SearchWindow { internal System.Collections.Generic.List<Window[]> Windows = new(); }")
        ], [(SearchPath, "XCMDARCH002")]);
        await CheckSourcesAsync(references,
        [
            (WorkspacePath, "namespace Xcmd.Desktop.Workspace; internal static class Commands { internal static int Ignore(this int value) => value; }"),
            (SearchPath, "using Xcmd.Desktop.Workspace; namespace Xcmd.Desktop.Features.Search; internal class SearchWindow { internal int Run() => 1.Ignore(); }")
        ], [(SearchPath, "XCMDARCH002")]);
        await CheckSourcesAsync(references,
        [
            (WorkspacePath, WorkspaceType),
            (SharedPath, "namespace Xcmd.Desktop.Shared.Ui; internal static class Controls { internal static Workspace.MainWindow Create() => new(); }"),
            (SearchPath, "using Xcmd.Desktop.Shared.Ui; namespace Xcmd.Desktop.Features.Search; internal class SearchWindow { internal object Run() { var value = Controls.Create(); return value; } }")
        ], [(SearchPath, "XCMDARCH002"), (SharedPath, "XCMDARCH002")]);
        await CheckSourcesAsync(references,
        [
            (SearchPath, "namespace Xcmd.Desktop.Features.Search; internal partial class SearchWindow { }"),
            ("Features/Viewer/SearchWindow.Part.cs", "namespace Xcmd.Desktop.Features.Search; internal partial class SearchWindow { }")
        ], [("Features/Viewer/SearchWindow.Part.cs", "XCMDARCH001")]);
        await CheckSourcesAsync(references,
        [
            ("Misplaced.cs", "namespace Xcmd.Desktop; internal enum Misplaced { Value }")
        ], [("Misplaced.cs", "XCMDARCH001")]);
        Console.WriteLine("PASS Desktop module analyzer (ownership, permitted dependencies, semantic references and module order)");
    }

    private static void CheckModuleOrder()
    {
        var remaining = DesktopModules.Names.ToHashSet(StringComparer.Ordinal);
        while (remaining.Count != 0)
        {
            var leaves = remaining.Where(source => !remaining.Any(target => target != source && DesktopModules.CanReference(source, target))).ToArray();
            if (leaves.Length == 0) throw new InvalidOperationException("The Desktop modules allow a dependency cycle.");
            remaining.ExceptWith(leaves);
        }
    }

    private static async Task CheckSourcesAsync(MetadataReference[] references, (string Path, string Text)[] files, (string Path, string Id)[] expected)
    {
        var directory = Path.GetFullPath("DesktopArchitectureFixture");
        var trees = files.Select(file => CSharpSyntaxTree.ParseText(file.Text, path: Path.Combine(directory, file.Path)));
        var compilation = CSharpCompilation.Create("Xcmd.Desktop", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compilerErrors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (compilerErrors.Length != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, compilerErrors.Select(diagnostic => diagnostic.ToString())));
        var options = new AnalyzerOptions([], new ProjectOptions(directory));
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new DesktopArchitectureAnalyzer()), options).GetAnalyzerDiagnosticsAsync();
        var actual = diagnostics.Select(diagnostic =>
            (Path.GetRelativePath(directory, diagnostic.Location.SourceTree?.FilePath ?? directory).Replace('\\', '/'), diagnostic.Id));
        if (!actual.Order().SequenceEqual(expected.Order()))
            throw new InvalidOperationException("Unexpected Desktop module diagnostics: " + string.Join(Environment.NewLine, diagnostics));
    }

    private sealed class ProjectOptions(string directory) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => new DirectoryOption(directory);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new DirectoryOption(null);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new DirectoryOption(null);
    }

    private sealed class DirectoryOption(string? directory) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = directory ?? "";
            return directory != null && key == "build_property.MSBuildProjectDirectory";
        }
    }
}

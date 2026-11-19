// @tests FeedbackIdentity, QualityChecks
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UiChecks;

internal static class Program
{
    private const string Prelude = """
        using Xcmd.Desktop.Shared.Ui;
        namespace Avalonia.Controls {
            public class Control { }
            public class Button : Control { }
            public class TextBlock : Control { }
        }
        namespace Xcmd.Desktop.Shared.Ui {
            public static class FeedbackTarget {
                public static T WithFeedbackId<T>(this T value, string id) where T : Avalonia.Controls.Control => value;
            }
        }
        """;

    public static async Task<int> Main(string[] args)
    {
        if (args is not ["--self-test"]) return 2;
        var cases = new (string Source, string[] Expected)[]
        {
            ("class C { object value = new Avalonia.Controls.Button(); }", ["XCMDUI001"]),
            ("class C { Avalonia.Controls.Button value = new(); }", ["XCMDUI001"]),
            ("class Custom : Avalonia.Controls.Control {} class C { object value = new Custom(); }", ["XCMDUI001"]),
            ("class C { object value = new object(); }", []),
            ("class C { object value = new Avalonia.Controls.Button().WithFeedbackId(\"button\"); }", []),
            ("class C { object value = new Avalonia.Controls.Button().WithFeedbackId(\"\"); }", ["XCMDUI003"]),
            ("class C { string id = \"dynamic\"; object Make() => new Avalonia.Controls.Button().WithFeedbackId(id); }", ["XCMDUI003"]),
            ("class C { object one = new Avalonia.Controls.Button().WithFeedbackId(\"duplicate\"); object two = new Avalonia.Controls.TextBlock().WithFeedbackId(\"duplicate\"); }", ["XCMDUI002", "XCMDUI002"]),
            ("class C { object Make() => new Avalonia.Controls.Button().WithFeedbackId(\"same\"); void Mark(Avalonia.Controls.Button b) => b.WithFeedbackId(\"same\"); }", ["XCMDUI002", "XCMDUI002"])
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        foreach (var item in cases)
        {
            var compilation = CSharpCompilation.Create("UiChecksFixture", [CSharpSyntaxTree.ParseText(Prelude + item.Source)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var compilerErrors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            if (compilerErrors.Length > 0) throw new InvalidOperationException(string.Join("\n", compilerErrors.Select(item => item.ToString())));
            var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new FeedbackIdAnalyzer())).GetAnalyzerDiagnosticsAsync();
            if (!diagnostics.Select(diagnostic => diagnostic.Id).Order().SequenceEqual(item.Expected.Order()))
                throw new InvalidOperationException("Feedback analyzer missed a deliberate violation: " + item.Source);
        }
        Console.WriteLine($"PASS feedback identity analyzer ({cases.Length} compiler fixtures)");
        await DesktopArchitectureChecks.CheckAsync(references);
        return 0;
    }
}

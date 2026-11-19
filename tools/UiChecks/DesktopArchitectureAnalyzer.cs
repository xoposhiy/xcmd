// @implements QualityChecks
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UiChecks;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DesktopArchitectureAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Ownership = new("XCMDARCH001", "Desktop module ownership",
        "Type '{0}' must be in the same Desktop module in its namespace and file path", "Architecture", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Dependency = new("XCMDARCH002", "Desktop module dependency",
        "Type '{0}' in '{1}' cannot use '{2}' in '{3}'", "Architecture", DiagnosticSeverity.Error, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Ownership, Dependency];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.AssemblyName != "Xcmd.Desktop") return;
            start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var directory);
            start.RegisterSemanticModelAction(model => CheckFile(model, directory ?? ""));
        });
    }

    private static void CheckFile(SemanticModelAnalysisContext context, string directory)
    {
        var model = context.SemanticModel;
        var fileModule = DesktopModules.FromFile(directory, model.SyntaxTree.FilePath);
        foreach (var declaration in model.SyntaxTree.GetRoot(context.CancellationToken).DescendantNodes().Where(IsTypeDeclaration))
        {
            if (model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type) continue;
            var module = DesktopModules.FromNamespace(type.ContainingNamespace.ToDisplayString());
            if (module == null || module != fileModule)
                context.ReportDiagnostic(Diagnostic.Create(Ownership, DeclarationLocation(declaration), type.ToDisplayString()));
            else CheckDependencies(context, declaration, type, module);
        }
    }

    private static bool IsTypeDeclaration(SyntaxNode node) => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;

    private static Location DeclarationLocation(SyntaxNode declaration) => declaration switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.GetLocation(),
        DelegateDeclarationSyntax callback => callback.Identifier.GetLocation(),
        _ => declaration.GetLocation(),
    };

    private static void CheckDependencies(SemanticModelAnalysisContext context, SyntaxNode declaration, INamedTypeSymbol source, string module)
    {
        var checkedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var node in declaration.DescendantNodes(node => node == declaration || !IsTypeDeclaration(node)))
        {
            if (node is not (ExpressionSyntax or AttributeSyntax or ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax)) continue;
            foreach (var type in ReferencedTypes(context.SemanticModel, node, context.CancellationToken).SelectMany(NamedTypes))
            {
                if (!SymbolEqualityComparer.Default.Equals(source.ContainingAssembly, type.ContainingAssembly) || !checkedTypes.Add(type)) continue;
                var targetModule = DesktopModules.FromNamespace(type.ContainingNamespace.ToDisplayString());
                if (targetModule != null && !DesktopModules.CanReference(module, targetModule))
                    context.ReportDiagnostic(Diagnostic.Create(Dependency, node.GetLocation(), source.ToDisplayString(), module, type.ToDisplayString(), targetModule));
            }
        }
    }

    private static IEnumerable<ITypeSymbol?> ReferencedTypes(SemanticModel model, SyntaxNode node, CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(node, cancellationToken).Symbol;
        if (symbol is IAliasSymbol alias) symbol = alias.Target;
        if (symbol is ITypeSymbol type) yield return type;
        if (symbol is IFieldSymbol or IPropertySymbol or IEventSymbol) yield return symbol.ContainingType;
        if (symbol is IMethodSymbol method)
        {
            yield return (method.ReducedFrom ?? method).ContainingType;
            foreach (var argument in method.TypeArguments) yield return argument;
        }
        if (node is ExpressionSyntax expression) yield return model.GetTypeInfo(expression, cancellationToken).Type;
    }

    private static IEnumerable<INamedTypeSymbol> NamedTypes(ITypeSymbol? type)
    {
        switch (type)
        {
            case INamedTypeSymbol named:
                yield return named.OriginalDefinition;
                foreach (var argument in named.TypeArguments.SelectMany(NamedTypes)) yield return argument;
                break;
            case IArrayTypeSymbol array:
                foreach (var element in NamedTypes(array.ElementType)) yield return element;
                break;
            case IPointerTypeSymbol pointer:
                foreach (var element in NamedTypes(pointer.PointedAtType)) yield return element;
                break;
        }
    }
}

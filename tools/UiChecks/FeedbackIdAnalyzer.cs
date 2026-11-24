// @implements FeedbackIdentity, QualityChecks
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace UiChecks;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FeedbackIdAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Missing = new("XCMDUI001", "Missing feedback identity",
        "Application control '{0}' needs .WithFeedbackId(\"stable.id\") at its creation site", "Feedback", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Duplicate = new("XCMDUI002", "Duplicate feedback identity",
        "Feedback ID '{0}' is declared more than once; give each source definition a unique ID", "Feedback", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Invalid = new("XCMDUI003", "Invalid feedback identity",
        "Pass a nonempty feedback ID literal or forward a required [FeedbackId] parameter", "Feedback", DiagnosticSeverity.Error, true);
    private const string FeedbackTargetType = "Xcmd.Desktop.Shared.Ui.FeedbackTarget";
    private const string FeedbackIdAttribute = "Xcmd.Desktop.Shared.Ui.FeedbackIdAttribute";
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Missing, Duplicate, Invalid];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var declarations = new ConcurrentDictionary<string, ConcurrentBag<Location>>(StringComparer.Ordinal);
            var forwarding = new ConcurrentDictionary<ISymbol, ConcurrentBag<Location>>(SymbolEqualityComparer.Default);
            start.RegisterSyntaxNodeAction(CheckCreation, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
            start.RegisterSyntaxNodeAction(CheckParameter, SyntaxKind.Parameter);
            start.RegisterOperationAction(context => CheckArgument(context, declarations, forwarding), OperationKind.Argument);
            start.RegisterCompilationEndAction(end =>
            {
                foreach (var pair in declarations.Where(pair => pair.Value.Count > 1))
                    foreach (var location in pair.Value) end.ReportDiagnostic(Diagnostic.Create(Duplicate, location, pair.Key));
                foreach (var pair in forwarding.Where(pair => pair.Value.Count > 1))
                    foreach (var location in pair.Value) end.ReportDiagnostic(Diagnostic.Create(Duplicate, location, pair.Key.Name));
            });
        });
    }

    internal static bool IsControl(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            if (current.ToDisplayString() == "Avalonia.Controls.Control") return true;
        return false;
    }

    private static void CheckCreation(SyntaxNodeAnalysisContext context)
    {
        if (!IsControl(context.SemanticModel.GetTypeInfo(context.Node).Type)) return;
        if (context.Node.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "WithFeedbackId", Parent: InvocationExpressionSyntax invocation } &&
            context.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            method.ContainingType.ToDisplayString() == FeedbackTargetType) return;
        context.ReportDiagnostic(Diagnostic.Create(Missing, context.Node.GetLocation(), context.SemanticModel.GetTypeInfo(context.Node).Type!.Name));
    }

    private static bool IsFeedbackId(IParameterSymbol? parameter) =>
        parameter?.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == FeedbackIdAttribute) == true;

    private static void CheckParameter(SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is IParameterSymbol parameter &&
            IsFeedbackId(parameter) && (parameter.IsOptional || parameter.Type.SpecialType != SpecialType.System_String))
            context.ReportDiagnostic(Diagnostic.Create(Invalid, context.Node.GetLocation()));
    }

    private static void CheckArgument(OperationAnalysisContext context, ConcurrentDictionary<string, ConcurrentBag<Location>> declarations,
        ConcurrentDictionary<ISymbol, ConcurrentBag<Location>> forwarding)
    {
        var argument = (IArgumentOperation)context.Operation;
        if (!IsFeedbackId(argument.Parameter) || argument.IsImplicit) return;
        if (argument.Value.Syntax is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) &&
            !string.IsNullOrWhiteSpace(literal.Token.ValueText))
        {
            declarations.GetOrAdd(literal.Token.ValueText, _ => []).Add(literal.GetLocation());
            return;
        }
        if (argument.Value is IParameterReferenceOperation reference && IsFeedbackId(reference.Parameter))
        {
            forwarding.GetOrAdd(reference.Parameter, _ => []).Add(argument.Value.Syntax.GetLocation());
            return;
        }
        context.ReportDiagnostic(Diagnostic.Create(Invalid, argument.Syntax.GetLocation()));
    }
}

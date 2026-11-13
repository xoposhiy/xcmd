using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UiChecks;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FeedbackIdAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Missing = new("XCMDUI001", "Missing feedback identity",
        "Application control '{0}' needs .WithFeedbackId(\"stable.id\") at its creation site", "Feedback", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Duplicate = new("XCMDUI002", "Duplicate feedback identity",
        "Feedback ID '{0}' is declared more than once; give each source definition a unique ID", "Feedback", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Invalid = new("XCMDUI003", "Invalid feedback identity",
        "Feedback IDs must be nonempty string literals so agents can search for their definitions", "Feedback", DiagnosticSeverity.Error, true);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Missing, Duplicate, Invalid];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var declarations = new ConcurrentDictionary<string, ConcurrentBag<Location>>(StringComparer.Ordinal);
            start.RegisterSyntaxNodeAction(CheckCreation, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
            start.RegisterSyntaxNodeAction(node => CheckId(node, declarations), SyntaxKind.InvocationExpression);
            start.RegisterCompilationEndAction(end =>
            {
                foreach (var pair in declarations.Where(pair => pair.Value.Count > 1))
                    foreach (var location in pair.Value) end.ReportDiagnostic(Diagnostic.Create(Duplicate, location, pair.Key));
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
        if (context.Node.Parent is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "WithFeedbackId", Parent: InvocationExpressionSyntax })
        {
            context.ReportDiagnostic(Diagnostic.Create(Missing, context.Node.GetLocation(), context.SemanticModel.GetTypeInfo(context.Node).Type!.Name));
            return;
        }
    }

    private static void CheckId(SyntaxNodeAnalysisContext context, ConcurrentDictionary<string, ConcurrentBag<Location>> declarations)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { Name: "WithFeedbackId" } method ||
            method.ContainingType.ToDisplayString() != "Xcmd.Desktop.FeedbackTarget") return;
        if (invocation.ArgumentList.Arguments is not [var argument] || argument.Expression is not LiteralExpressionSyntax literal ||
            !literal.IsKind(SyntaxKind.StringLiteralExpression) || string.IsNullOrWhiteSpace(literal.Token.ValueText))
        {
            context.ReportDiagnostic(Diagnostic.Create(Invalid, invocation.GetLocation()));
            return;
        }
        declarations.GetOrAdd(literal.Token.ValueText, _ => []).Add(literal.GetLocation());
    }
}

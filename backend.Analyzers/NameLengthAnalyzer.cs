using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Backend.Analyzers;

/// <summary>
/// RLMS002: variabler, parametrar och lambda-parametrar ska heta något, inte en bokstav.
/// _ (discard) och räknaren i en for-loop är ok.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NameLengthAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "RLMS002";

    private static readonly DiagnosticDescriptor _rule = new(
        Id,
        "Use a descriptive name",
        "'{0}' is a one-character name, name it after what it is",
        "Naming",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.VariableDeclarator,
            SyntaxKind.Parameter,
            SyntaxKind.SingleVariableDesignation,
            SyntaxKind.ForEachStatement,
            SyntaxKind.CatchDeclaration,
            SyntaxKind.FromClause,
            SyntaxKind.LetClause,
            SyntaxKind.JoinClause,
            SyntaxKind.JoinIntoClause,
            SyntaxKind.QueryContinuation);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var identifier = context.Node switch
        {
            VariableDeclaratorSyntax variable => variable.Identifier,
            ParameterSyntax parameter => parameter.Identifier,
            SingleVariableDesignationSyntax designation => designation.Identifier,
            ForEachStatementSyntax forEach => forEach.Identifier,
            CatchDeclarationSyntax catchDeclaration => catchDeclaration.Identifier,
            FromClauseSyntax from => from.Identifier,
            LetClauseSyntax let => let.Identifier,
            JoinClauseSyntax join => join.Identifier,
            JoinIntoClauseSyntax joinInto => joinInto.Identifier,
            QueryContinuationSyntax continuation => continuation.Identifier,
            _ => default
        };

        // Räknaren i en for-loop (i) är ok, det har vi bestämt.
        if (context.Node is VariableDeclaratorSyntax { Parent.Parent: ForStatementSyntax })
        {
            return;
        }

        if (identifier.ValueText.Length == 1 && identifier.ValueText != "_")
        {
            context.ReportDiagnostic(Diagnostic.Create(_rule, identifier.GetLocation(), identifier.ValueText));
        }
    }
}

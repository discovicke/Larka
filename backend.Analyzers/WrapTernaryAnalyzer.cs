using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Backend.Analyzers;

/// <summary>RLMS001: ? och : i en ternary ska stå först på var sin rad.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WrapTernaryAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "RLMS001";

    private static readonly DiagnosticDescriptor _rule = new(
        Id,
        "Wrap ternary expression",
        "Put '?' and ':' at the start of their own lines",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var ternary = (ConditionalExpressionSyntax)context.Node;
        if (!StartsLine(ternary.QuestionToken) || !StartsLine(ternary.ColonToken))
        {
            context.ReportDiagnostic(Diagnostic.Create(_rule, ternary.QuestionToken.GetLocation()));
        }
    }

    internal static bool StartsLine(SyntaxToken token)
    {
        var previous = token.GetPreviousToken();
        var lines = token.SyntaxTree!.GetText().Lines;
        return lines.GetLineFromPosition(previous.Span.End).LineNumber
            != lines.GetLineFromPosition(token.SpanStart).LineNumber;
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Backend.Analyzers;

/// <summary>
/// RLMS003: inga property patterns (is { } x, is { Name: "x" }), de är svåra att läsa.
/// Skriv en vanlig null-kontroll eller jämförelse i stället.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PropertyPatternAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "RLMS003";

    private static readonly DiagnosticDescriptor _rule = new(
        Id,
        "Don't use property patterns",
        "Use a null check or a plain comparison instead of a property pattern",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyPatternClause);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context) =>
        context.ReportDiagnostic(Diagnostic.Create(_rule, context.Node.Parent!.GetLocation()));
}

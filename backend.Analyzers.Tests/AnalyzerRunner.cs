using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace backend.Analyzers.Tests;

/// <summary>Kör en analyzer (och ev. kodfix) på en kodsnutt, utan att bygga något projekt.</summary>
public static class AnalyzerRunner
{
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(DiagnosticAnalyzer analyzer, string source)
    {
        var document = CreateDocument(source);
        return await GetDiagnosticsAsync(analyzer, document);
    }

    /// <summary>Kör kodfixen på första diagnosen tills inga diagnoser finns kvar och returnerar koden.</summary>
    public static async Task<string> FixAsync(DiagnosticAnalyzer analyzer, CodeFixProvider codeFix, string source)
    {
        var document = CreateDocument(source);
        var diagnostics = await GetDiagnosticsAsync(analyzer, document);
        while (!diagnostics.IsEmpty)
        {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(
                document,
                diagnostics[0],
                (action, _) => actions.Add(action),
                CancellationToken.None);
            await codeFix.RegisterCodeFixesAsync(context);

            var operations = await actions.Single().GetOperationsAsync(CancellationToken.None);
            document = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
            diagnostics = await GetDiagnosticsAsync(analyzer, document);
        }

        return (await document.GetTextAsync()).ToString();
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Test", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        return project.AddDocument("Test.cs", SourceText.From(source));
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        DiagnosticAnalyzer analyzer, Document document)
    {
        var compilation = await document.Project.GetCompilationAsync();
        return await compilation!.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
    }
}

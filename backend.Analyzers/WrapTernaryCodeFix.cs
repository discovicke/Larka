using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Backend.Analyzers;

/// <summary>Flyttar ? och : till egna rader, ett indrag (4 mellanslag) längre in än raden ternaryn börjar på.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class WrapTernaryCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(WrapTernaryAnalyzer.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var token = root!.FindToken(context.Span.Start);
        if (token.Parent is not ConditionalExpressionSyntax ternary)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Wrap ternary expression",
                cancellationToken => WrapAsync(context.Document, ternary, cancellationToken),
                nameof(WrapTernaryCodeFix)),
            context.Diagnostics);
    }

    private static async Task<Document> WrapAsync(
        Document document, ConditionalExpressionSyntax ternary, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var line = text.Lines.GetLineFromPosition(ternary.SpanStart).ToString();
        var indent = new string(' ', line.TakeWhile(char.IsWhiteSpace).Count() + 4);

        var question = ternary.QuestionToken;
        var colon = ternary.ColonToken;
        var beforeQuestion = question.GetPreviousToken();
        var beforeColon = colon.GetPreviousToken();

        var wrapped = ternary.ReplaceTokens(
            [beforeQuestion, question, beforeColon, colon],
            (original, _) => original == question || original == colon
                ? original.WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace(indent))
                : WithoutTrailingWhitespace(original));

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(ternary, wrapped));
    }

    private static SyntaxToken WithoutTrailingWhitespace(SyntaxToken token) =>
        token.WithTrailingTrivia(token.TrailingTrivia.Where(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia)));
}

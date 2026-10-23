using Backend.Analyzers;

namespace backend.Analyzers.Tests;

public class NameLengthTests
{
    [Theory]
    [InlineData("void Run() { var c = 1; }")]
    [InlineData("void Run(int c) { }")]
    [InlineData("void Run() { System.Func<int, int> twice = c => c * 2; }")]
    [InlineData("void Run() { System.Func<int, int, int> add = (a, total) => a + total; }")]
    [InlineData("void Run(object value) { if (value is string s) { } }")]
    [InlineData("void Run() { int.TryParse(\"1\", out var n); }")]
    [InlineData("void Run(int[] numbers) { foreach (var n in numbers) { } }")]
    [InlineData("void Run() { try { } catch (System.Exception e) { } }")]
    [InlineData("void Run(int[] numbers) { var query = from n in numbers select n; }")]
    public async Task OneCharacterName_IsReported(string member)
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(new NameLengthAnalyzer(), WrapInClass(member));

        Assert.Equal(NameLengthAnalyzer.Id, Assert.Single(diagnostics).Id);
    }

    [Theory]
    [InlineData("void Run() { var content = 1; }")]
    [InlineData("void Run() { System.Func<int, int> twice = number => number * 2; }")]
    [InlineData("void Run() { System.Func<int, int> zero = _ => 0; }")]
    [InlineData("void Run(System.Threading.CancellationToken ct) { }")]
    [InlineData("void Run(object value) { if (value is string _) { } }")]
    [InlineData("void Run() { for (var i = 0; i < 3; i++) { } }")]
    public async Task DescriptiveNameDiscardOrForCounter_IsAllowed(string member)
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(new NameLengthAnalyzer(), WrapInClass(member));

        Assert.Empty(diagnostics);
    }

    private static string WrapInClass(string member) => $$"""
        using System.Linq;

        class Test
        {
            {{member}}
        }
        """;
}

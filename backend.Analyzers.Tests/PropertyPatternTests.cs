using Backend.Analyzers;

namespace backend.Analyzers.Tests;

public class PropertyPatternTests
{
    [Theory]
    [InlineData("bool Run(object value) => value is { } found;")]
    [InlineData("bool Run(string text) => text is { Length: > 0 };")]
    [InlineData("bool Run(object value) => value is string { Length: 0 };")]
    [InlineData("bool Run(string[] parts) => parts is [{ Length: > 0 }];")]
    [InlineData("int Run(string text) => text switch { { Length: 0 } => 0, _ => 1 };")]
    public async Task PropertyPattern_IsReported(string member)
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(new PropertyPatternAnalyzer(), WrapInClass(member));

        Assert.Equal(PropertyPatternAnalyzer.Id, Assert.Single(diagnostics).Id);
    }

    [Theory]
    [InlineData("bool Run(object value) => value is null;")]
    [InlineData("bool Run(object value) => value is not null;")]
    [InlineData("bool Run(object value) => value is string text && text.Length > 0;")]
    [InlineData("bool Run(int number) => number is > 0 and < 10;")]
    [InlineData("bool Run(string[] parts) => parts is [_, _];")]
    public async Task PlainPatterns_AreAllowed(string member)
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(new PropertyPatternAnalyzer(), WrapInClass(member));

        Assert.Empty(diagnostics);
    }

    private static string WrapInClass(string member) => $$"""
        class Test
        {
            {{member}}
        }
        """;
}

using Backend.Analyzers;

namespace backend.Analyzers.Tests;

public class WrapTernaryTests
{
    private const string _wrapped = """
        class Test
        {
            string Status(bool healthy)
            {
                return healthy
                    ? "OK"
                    : "Unhealthy";
            }
        }
        """;

    [Fact]
    public async Task WrappedTernary_IsAllowed()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(new WrapTernaryAnalyzer(), _wrapped);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("""return healthy ? "OK" : "Unhealthy";""")]
    [InlineData("""
        return healthy
            ? "OK" : "Unhealthy";
        """)]
    [InlineData("""
        return healthy ?
            "OK" :
            "Unhealthy";
        """)]
    public async Task UnwrappedTernary_IsReported(string statement)
    {
        var source = $$"""
            class Test
            {
                string Status(bool healthy)
                {
                    {{statement}}
                }
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(new WrapTernaryAnalyzer(), source);

        Assert.Equal(WrapTernaryAnalyzer.Id, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task CodeFix_PutsQuestionAndColonOnOwnLines()
    {
        var source = """
            class Test
            {
                string Status(bool healthy)
                {
                    return healthy ? "OK" : "Unhealthy";
                }
            }
            """;

        var fixedSource = await AnalyzerRunner.FixAsync(new WrapTernaryAnalyzer(), new WrapTernaryCodeFix(), source);

        Assert.Equal(_wrapped, fixedSource);
    }

    [Fact]
    public async Task CodeFix_IndentsFromTheLineTheTernaryStartsOn()
    {
        var source = """
            class Test
            {
                string Status(bool healthy) =>
                    string.Concat(
                        healthy ? "OK" : "Unhealthy");
            }
            """;

        var fixedSource = await AnalyzerRunner.FixAsync(new WrapTernaryAnalyzer(), new WrapTernaryCodeFix(), source);

        Assert.Equal("""
            class Test
            {
                string Status(bool healthy) =>
                    string.Concat(
                        healthy
                            ? "OK"
                            : "Unhealthy");
            }
            """, fixedSource);
    }
}

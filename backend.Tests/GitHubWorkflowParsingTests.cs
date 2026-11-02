using Backend.Infrastructure.GitHub;

namespace backend.Tests;

// Kraven i Larka matchas mot GitHubs check runs, som är jobbnamn. Workflow- och stegnamn ska inte läcka in.
public class GitHubWorkflowParsingTests
{
    [Fact]
    public void ParseWorkflowJobNames_ReturnsJobNamesAndFallsBackToJobId()
    {
        const string yaml = """
            name: CI
            on: [push, pull_request]

            jobs:
              build:
                name: Build and test
                runs-on: ubuntu-latest
                steps:
                  - name: Checkout code
                    uses: actions/checkout@v4
                  - name: Run tests
                    run: dotnet test

              lint:
                runs-on: ubuntu-latest
                steps:
                  - name: Lint
                    run: dotnet format --verify-no-changes

              matrix:
                name: Test matrix
                strategy:
                  matrix:
                    os: [ubuntu-latest, windows-latest]
                runs-on: ${{ matrix.os }}
            """;

        var names = GitHubAppService.ParseWorkflowJobNames(yaml);

        Assert.Equal(["Build and test", "lint", "Test matrix"], names);
        Assert.DoesNotContain("CI", names);
        Assert.DoesNotContain("Checkout code", names);
        Assert.DoesNotContain("Run tests", names);
        Assert.DoesNotContain("Lint", names);
    }

    [Fact]
    public void ParseWorkflowJobNames_WithoutJobs_ReturnsEmpty()
    {
        const string yaml = """
            name: CI
            on: [push]
            """;

        Assert.Empty(GitHubAppService.ParseWorkflowJobNames(yaml));
    }

    [Fact]
    public void ParseWorkflowJobNames_InvalidYaml_ReturnsEmpty()
    {
        Assert.Empty(GitHubAppService.ParseWorkflowJobNames("jobs: ["));
    }
}

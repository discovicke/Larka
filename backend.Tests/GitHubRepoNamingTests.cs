using Backend.Application.Services;
using Backend.Domain.Entities;

namespace backend.Tests;

public class GitHubRepoNamingTests
{
    private static User Student(string? login = "alice", string email = "alice@example.com") =>
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Alice Andersson",
            Email = email,
            GitHubLogin = login
        };

    [Fact]
    public void DefaultPattern_UsesAssignmentSlugAndGitHubLogin()
    {
        var name = GitHubRepoNaming.ForStudent(null, "Labb 1: Release pipeline", Student());

        Assert.Equal("labb-1-release-pipeline-alice", name);
    }

    [Fact]
    public void CustomPattern_ReplacesAllSupportedPlaceholders()
    {
        var name = GitHubRepoNaming.ForStudent(
            "lab2-{assignmentSlug}-{githubLogin}-{studentId}",
            "Release pipeline",
            Student());

        Assert.Equal("lab2-release-pipeline-alice-11111111", name);
    }

    [Fact]
    public void UnlinkedStudent_FallsBackToEmailLocalPart()
    {
        var name = GitHubRepoNaming.ForStudent(null, "Labb 1", Student(null, "bob.svensson@example.com"));

        Assert.Equal("labb-1-bob-svensson", name);
    }

    [Fact]
    public void DuplicateNames_GetAStableSuffix()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var student = Student();

        var first = GitHubRepoNaming.ForStudent(null, "Labb 1", student, used);
        var second = GitHubRepoNaming.ForStudent(null, "Labb 1", student, used);

        Assert.Equal("labb-1-alice", first);
        Assert.Equal("labb-1-alice-11111111", second);
    }

    [Theory]
    [InlineData("lab2-{githubLogin}")]
    [InlineData("{assignmentTitle}-{studentEmail}")]
    [InlineData("{assignmentSlug}-{studentName}-{studentId}")]
    public void Validate_AcceptsKnownPatterns(string pattern)
    {
        Assert.Null(GitHubRepoNaming.Validate(pattern));
    }

    [Theory]
    [InlineData("lab2")]
    [InlineData("lab2-{githubLogin")]
    [InlineData("lab2-{unknown}")]
    [InlineData("lab2/{githubLogin}")]
    [InlineData("{studentName}")]
    public void Validate_RejectsInvalidPatterns(string pattern)
    {
        Assert.NotNull(GitHubRepoNaming.Validate(pattern));
    }

    [Fact]
    public void GroupPattern_UsesGroupSlug()
    {
        var name = GitHubRepoNaming.ForGroup(
            "labb-{groupSlug}", "Grupplabb", "Grupp ett", 1);

        Assert.Equal("labb-grupp-ett", name);
    }

    [Fact]
    public void GroupPattern_DefaultUsesGroupSlug()
    {
        var name = GitHubRepoNaming.ForGroup(null, "Grupplabb", "Grupp ett", 1);

        Assert.Equal("grupplabb-grupp-ett", name);
    }

    [Theory]
    [InlineData("{assignmentSlug}-{groupName}")]
    [InlineData("{assignmentSlug}-{groupSlug}")]
    [InlineData("{assignmentSlug}-grupp-{groupNumber}")]
    public void Validate_AcceptsGroupPatterns(string pattern)
    {
        Assert.Null(GitHubRepoNaming.Validate(pattern, Backend.Domain.Enums.RepoWorkMode.Group));
    }

    [Theory]
    [InlineData("{assignmentSlug}-{githubLogin}")]
    [InlineData("{assignmentTitle}")]
    public void Validate_GroupModeRequiresGroupPlaceholder(string pattern)
    {
        Assert.NotNull(GitHubRepoNaming.Validate(pattern, Backend.Domain.Enums.RepoWorkMode.Group));
    }

    [Fact]
    public void Render_TruncatesLongNames()
    {
        var name = GitHubRepoNaming.ForStudent(
            "{assignmentTitle}-{githubLogin}",
            new string('a', 200),
            Student());

        Assert.True(name.Length <= 90);
        Assert.EndsWith("-alice", name);
    }
}

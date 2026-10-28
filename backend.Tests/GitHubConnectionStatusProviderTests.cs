using System.Net;
using Backend.Application.Abstractions;
using Backend.Infrastructure.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace backend.Tests;

// Testar den sanerade statusen utan nätverk. GitHub-klienten är utbytt mot en fake.
public class GitHubConnectionStatusProviderTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-10-28T10:00:00Z");

    [Fact]
    public async Task GetStatus_WithoutConfiguration_ReturnsMissingSettingsWithoutCallingGitHub()
    {
        var github = new FakeGitHubAppService();
        var provider = CreateProvider(github, new GitHubOptions());

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.False(status.Configured);
        Assert.False(status.Connected);
        Assert.Null(status.AccountLogin);
        Assert.Null(status.LastVerifiedAt);
        Assert.Contains("AppId", status.MissingSettings);
        Assert.Contains("InstallationId", status.MissingSettings);
        Assert.Contains("Org", status.MissingSettings);
        Assert.Contains("PrivateKey", status.MissingSettings);
        Assert.Equal(0, github.InstallationCalls);
    }

    [Fact]
    public async Task GetStatus_WhenInstallationIsReachable_ReturnsConnectedAccount()
    {
        var github = new FakeGitHubAppService
        {
            Installation = new GitHubInstallation(2, "EduEdugrade-Sandbox", 1)
        };
        var provider = CreateProvider(github, ConfiguredOptions());

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Configured);
        Assert.True(status.Connected);
        Assert.Equal("EduEdugrade-Sandbox", status.AccountLogin);
        Assert.Equal(_now, status.LastVerifiedAt);
        Assert.Empty(status.MissingSettings);
        Assert.Null(status.Error);
        Assert.Equal(2, status.InstallationId);
    }

    [Fact]
    public async Task GetStatus_WhenInstallationBelongsToAnotherOrg_ReturnsDisconnected()
    {
        var github = new FakeGitHubAppService
        {
            Installation = new GitHubInstallation(2, "wrong-org", 1)
        };
        var provider = CreateProvider(github, ConfiguredOptions());

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Configured);
        Assert.False(status.Connected);
        Assert.Equal("wrong-org", status.AccountLogin);
        Assert.Contains("wrong-org", status.Error);
        Assert.Contains("EduEdugrade-Sandbox", status.Error);
    }

    [Fact]
    public async Task GetStatus_WhenGitHubReturnsError_ReturnsDisconnectedWithSafeMessage()
    {
        var github = new FakeGitHubAppService
        {
            InstallationException = new GitHubApiException(HttpStatusCode.NotFound, """{"message":"Not Found"}""")
        };
        var provider = CreateProvider(github, ConfiguredOptions());

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Configured);
        Assert.False(status.Connected);
        Assert.Null(status.AccountLogin);
        Assert.Equal(_now, status.LastVerifiedAt);
        Assert.Contains("404", status.Error);
    }

    [Fact]
    public async Task GetStatus_WhenConfigurationIsBroken_DoesNotLeakInternalDetails()
    {
        var github = new FakeGitHubAppService
        {
            InstallationException = new InvalidOperationException(
                "GitHub private key file not found: /secret/path/github.pem")
        };
        var provider = CreateProvider(github, ConfiguredOptions());

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.False(status.Connected);
        Assert.Contains("Kunde inte verifiera", status.Error);
        Assert.DoesNotContain("/secret/path", status.Error);
    }

    private static GitHubConnectionStatusProvider CreateProvider(
        FakeGitHubAppService github, GitHubOptions options) =>
        new(
            github,
            Options.Create(options),
            new TestTimeProvider(_now),
            NullLogger<GitHubConnectionStatusProvider>.Instance);

    private static GitHubOptions ConfiguredOptions() => new()
    {
        AppId = 1,
        InstallationId = 2,
        Org = "EduEdugrade-Sandbox",
        PrivateKey = "test-key",
        ApiBaseUrl = "https://api.github.com/"
    };

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeGitHubAppService : IGitHubAppService
    {
        public GitHubInstallation? Installation { get; init; }

        public Exception? InstallationException { get; init; }

        public int InstallationCalls { get; private set; }

        public Task<GitHubInstallation> GetInstallationAsync(
            long installationId, CancellationToken ct)
        {
            InstallationCalls++;
            if (InstallationException is not null)
            {
                throw InstallationException;
            }

            return Task.FromResult(Installation ?? throw new NotSupportedException());
        }

        public Task<GitHubInstallationToken> GetInstallationTokenAsync(
            long installationId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<GitHubRepository>> ListRepositoriesAsync(
            long installationId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<GitHubRepository>> ListTemplateRepositoriesAsync(
            long installationId, CancellationToken ct) => throw new NotSupportedException();

        public Task<GitHubRepository> GetRepositoryAsync(
            long installationId, string owner, string name, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<GitHubCommit?> GetLatestCommitAsync(
            long installationId, string owner, string name, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Backend.Domain.Enums.CheckStatus> GetCheckStatusAsync(
            long installationId, string owner, string name, string reference, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<GitHubRepository> CreateRepositoryFromTemplateAsync(
            long installationId,
            string templateOwner,
            string templateRepo,
            string owner,
            string name,
            bool isPrivate,
            string? description,
            CancellationToken ct) => throw new NotSupportedException();

        public Task AddCollaboratorAsync(
            long installationId, string owner, string name, string username, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ArchiveRepositoryAsync(
            long installationId, string owner, string name, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}

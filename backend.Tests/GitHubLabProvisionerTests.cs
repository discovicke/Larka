using System.Net;
using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.GitHub;
using Backend.Infrastructure.Labs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace backend.Tests;

// Testar provisioneraren utan nätverk. GitHub-klienten är utbytt mot en inspelande fake.
public class GitHubLabProvisionerTests
{
    [Fact]
    public async Task ProvisionAsync_CreatesPrivateRepoAndInvitesLinkedStudent()
    {
        var github = new FakeGitHubAppService();
        var provisioner = CreateProvisioner(github);
        var submissionId = Guid.NewGuid();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Email = "alice@example.com",
            GitHubLogin = "alice"
        };

        var repos = await provisioner.ProvisionAsync(
            "EduEdugrade-Sandbox/Template",
            "Labb 1",
            [new RepoRequest(submissionId, student)],
            CancellationToken.None);

        var repo = Assert.Single(repos);
        Assert.Equal(submissionId, repo.SubmissionId);
        Assert.Equal("EduEdugrade-Sandbox/labb-1-alice", repo.RepoFullName);
        Assert.Equal(CheckStatus.Pending, repo.CheckStatus);
        Assert.NotEqual(default, repo.UpdatedAt);

        var create = Assert.Single(github.Creates);
        Assert.Equal("EduEdugrade-Sandbox", create.TemplateOwner);
        Assert.Equal("Template", create.TemplateRepo);
        Assert.Equal("EduEdugrade-Sandbox", create.Owner);
        Assert.Equal("labb-1-alice", create.Name);
        Assert.True(create.IsPrivate);

        var collaborator = Assert.Single(github.Collaborators);
        Assert.Equal("EduEdugrade-Sandbox", collaborator.Owner);
        Assert.Equal("labb-1-alice", collaborator.Name);
        Assert.Equal("alice", collaborator.Username);
    }

    [Fact]
    public async Task ProvisionAsync_WithoutGitHubLogin_StillCreatesRepoButSkipsInvite()
    {
        var github = new FakeGitHubAppService();
        var provisioner = CreateProvisioner(github);
        var student = new User
        {
            Id = Guid.NewGuid(),
            Name = "Bob",
            Email = "bob@example.com"
        };

        var repos = await provisioner.ProvisionAsync(
            "EduEdugrade-Sandbox/Template",
            "Labb 2",
            [new RepoRequest(Guid.NewGuid(), student)],
            CancellationToken.None);

        var repo = Assert.Single(repos);
        Assert.Equal("EduEdugrade-Sandbox/labb-2-bob", repo.RepoFullName);
        Assert.Single(github.Creates);
        Assert.Empty(github.Collaborators);
    }

    [Fact]
    public async Task ProvisionAsync_WhenRepoAlreadyExists_ReusesIt()
    {
        var github = new FakeGitHubAppService
        {
            CreateException = new GitHubApiException(
                HttpStatusCode.UnprocessableEntity, """{"message":"name already exists"}"""),
            ExistingRepository = new GitHubRepository(
                "EduEdugrade-Sandbox/labb-1-alice",
                "https://github.com/EduEdugrade-Sandbox/labb-1-alice",
                "main",
                true,
                false,
                false)
        };
        var provisioner = CreateProvisioner(github);
        var student = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Email = "alice@example.com"
        };

        var repos = await provisioner.ProvisionAsync(
            "EduEdugrade-Sandbox/Template",
            "Labb 1",
            [new RepoRequest(Guid.NewGuid(), student)],
            CancellationToken.None);

        var repo = Assert.Single(repos);
        Assert.Equal("EduEdugrade-Sandbox/labb-1-alice", repo.RepoFullName);
    }

    [Fact]
    public async Task RefreshCheckStatusAsync_UpdatesShaAndStatus()
    {
        var github = new FakeGitHubAppService
        {
            LatestCommit = new GitHubCommit("sha1", "Fix tests", DateTimeOffset.Parse("2026-10-28T09:30:00Z")),
            CheckStatus = CheckStatus.Green
        };
        var provisioner = CreateProvisioner(github);
        var repo = new StudentRepo { RepoFullName = "EduEdugrade-Sandbox/labb-1-alice" };

        await provisioner.RefreshCheckStatusAsync([repo], CancellationToken.None);

        Assert.Equal("sha1", repo.LastSha);
        Assert.Equal(CheckStatus.Green, repo.CheckStatus);
        Assert.NotEqual(default, repo.UpdatedAt);
    }

    private static GitHubLabProvisioner CreateProvisioner(FakeGitHubAppService github) =>
        new(
            github,
            Options.Create(new GitHubOptions
            {
                AppId = 1,
                InstallationId = 2,
                Org = "EduEdugrade-Sandbox",
                PrivateKey = "test-key"
            }),
            TimeProvider.System,
            NullLogger<GitHubLabProvisioner>.Instance);

    private sealed class FakeGitHubAppService : IGitHubAppService
    {
        public List<(long InstallationId, string TemplateOwner, string TemplateRepo, string Owner, string Name,
            bool IsPrivate, string? Description)> Creates
        { get; } = [];

        public List<(long InstallationId, string Owner, string Name, string Username)> Collaborators { get; } = [];

        public GitHubCommit? LatestCommit { get; init; }

        public CheckStatus CheckStatus { get; init; } = CheckStatus.Pending;

        public GitHubApiException? CreateException { get; init; }

        public GitHubRepository? ExistingRepository { get; init; }

        public Task<GitHubRepository> CreateRepositoryFromTemplateAsync(
            long installationId,
            string templateOwner,
            string templateRepo,
            string owner,
            string name,
            bool isPrivate,
            string? description,
            CancellationToken ct)
        {
            if (CreateException is not null)
            {
                throw CreateException;
            }

            Creates.Add((installationId, templateOwner, templateRepo, owner, name, isPrivate, description));
            return Task.FromResult(new GitHubRepository(
                $"{owner}/{name}",
                $"https://github.com/{owner}/{name}",
                "main",
                isPrivate,
                false,
                false));
        }

        public Task AddCollaboratorAsync(
            long installationId, string owner, string name, string username, CancellationToken ct)
        {
            Collaborators.Add((installationId, owner, name, username));
            return Task.CompletedTask;
        }

        public Task<GitHubCommit?> GetLatestCommitAsync(
            long installationId, string owner, string name, CancellationToken ct) =>
            Task.FromResult(LatestCommit);

        public Task<CheckStatus> GetCheckStatusAsync(
            long installationId, string owner, string name, string reference, CancellationToken ct) =>
            Task.FromResult(CheckStatus);

        public Task<GitHubInstallationToken> GetInstallationTokenAsync(
            long installationId, CancellationToken ct) => throw new NotSupportedException();

        public Task<GitHubInstallation> GetInstallationAsync(
            long installationId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<GitHubRepository>> ListRepositoriesAsync(
            long installationId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<GitHubRepository>> ListTemplateRepositoriesAsync(
            long installationId, CancellationToken ct) => throw new NotSupportedException();

        public Task<GitHubRepository> GetRepositoryAsync(
            long installationId, string owner, string name, CancellationToken ct) =>
            ExistingRepository is null
                ? throw new NotSupportedException()
                : Task.FromResult(ExistingRepository);

        public Task ArchiveRepositoryAsync(
            long installationId, string owner, string name, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}

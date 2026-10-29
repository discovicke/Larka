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
    private static readonly Guid _schoolId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ProvisionAsync_CreatesPrivateRepoAndInvitesLinkedStudent()
    {
        var github = new FakeGitHubAppService();
        var provisioner = CreateProvisioner(github);
        var submissionId = Guid.NewGuid();
        var student = new User
        {
            Id = Guid.NewGuid(),
            SchoolId = _schoolId,
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
        Assert.Equal(2, create.InstallationId);
        Assert.Equal("EduEdugrade-Sandbox", create.TemplateOwner);
        Assert.Equal("Template", create.TemplateRepo);
        Assert.Equal("EduEdugrade-Sandbox", create.Owner);
        Assert.Equal("labb-1-alice", create.Name);
        Assert.True(create.IsPrivate);

        var collaborator = Assert.Single(github.Collaborators);
        Assert.Equal("EduEdugrade-Sandbox", collaborator.Owner);
        Assert.Equal("labb-1-alice", collaborator.Name);
        Assert.Equal("alice", collaborator.Username);
        Assert.Equal(2, collaborator.InstallationId);
    }

    [Fact]
    public async Task ProvisionAsync_WhenSchoolIsDisconnected_ThrowsWithoutCallingGitHub()
    {
        var github = new FakeGitHubAppService();
        var provisioner = CreateProvisioner(github, connected: false);
        var student = new User
        {
            Id = Guid.NewGuid(),
            SchoolId = _schoolId,
            Name = "Alice",
            Email = "alice@example.com"
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provisioner.ProvisionAsync(
                "EduEdugrade-Sandbox/Template",
                "Labb 1",
                [new RepoRequest(Guid.NewGuid(), student)],
                CancellationToken.None));

        Assert.Contains("frånkopplad", exception.Message);
        Assert.Empty(github.Creates);
    }

    [Fact]
    public async Task ProvisionAsync_WithoutGitHubLogin_StillCreatesRepoButSkipsInvite()
    {
        var github = new FakeGitHubAppService();
        var provisioner = CreateProvisioner(github);
        var student = new User
        {
            Id = Guid.NewGuid(),
            SchoolId = _schoolId,
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
            SchoolId = _schoolId,
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

        await provisioner.RefreshCheckStatusAsync(
            [new StudentRepoContext(repo, _schoolId)], CancellationToken.None);

        Assert.Equal("sha1", repo.LastSha);
        Assert.Equal(CheckStatus.Green, repo.CheckStatus);
        Assert.NotEqual(default, repo.UpdatedAt);
    }

    [Fact]
    public async Task InviteToExistingReposAsync_InvitesStudentToAlreadyCreatedRepos()
    {
        var github = new FakeGitHubAppService();
        var student = new User
        {
            Id = Guid.NewGuid(),
            SchoolId = _schoolId,
            Name = "Alice",
            Email = "alice@example.com",
            GitHubLogin = "alice"
        };
        var submissions = new FakeSubmissionRepository
        {
            Repos =
            [
                new StudentRepo
                {
                    SubmissionId = Guid.NewGuid(),
                    RepoFullName = "EduEdugrade-Sandbox/labb-1-alice"
                }
            ]
        };
        var provisioner = CreateProvisioner(github, submissions: submissions);

        await provisioner.InviteToExistingReposAsync(student, CancellationToken.None);

        var collaborator = Assert.Single(github.Collaborators);
        Assert.Equal(2, collaborator.InstallationId);
        Assert.Equal("EduEdugrade-Sandbox", collaborator.Owner);
        Assert.Equal("labb-1-alice", collaborator.Name);
        Assert.Equal("alice", collaborator.Username);
    }

    private static GitHubLabProvisioner CreateProvisioner(
        FakeGitHubAppService github,
        bool connected = true,
        FakeSubmissionRepository? submissions = null) =>
        new(
            github,
            new FakeInstallationRepository(connected),
            submissions ?? new FakeSubmissionRepository(),
            Options.Create(new GitHubOptions
            {
                AppId = 1,
                AppSlug = "test-app",
                PrivateKey = "test-key"
            }),
            TimeProvider.System,
            NullLogger<GitHubLabProvisioner>.Instance);

    private sealed class FakeInstallationRepository(bool connected = true)
        : ISchoolGitHubInstallationRepository
    {
        private readonly SchoolGitHubInstallation _installation = new()
        {
            SchoolId = _schoolId,
            InstallationId = 2,
            Org = "EduEdugrade-Sandbox",
            Connected = connected
        };

        public Task<SchoolGitHubInstallation?> GetForSchoolAsync(
            Guid schoolId, CancellationToken ct) =>
            Task.FromResult<SchoolGitHubInstallation?>(
                schoolId == _schoolId
                    ? _installation
                    : null);

        public Task<SchoolGitHubInstallation?> GetByInstallationAsync(
            long installationId, CancellationToken ct) =>
            Task.FromResult<SchoolGitHubInstallation?>(
                installationId == _installation.InstallationId
                    ? _installation
                    : null);

        public void Add(SchoolGitHubInstallation installation) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSubmissionRepository : ISubmissionRepository
    {
        public List<StudentRepo> Repos { get; init; } = [];

        public Task<List<StudentRepo>> GetReposForStudentAsync(
            Guid studentId, CancellationToken ct) => Task.FromResult(Repos);

        public Task<Submission?> GetAsync(Guid id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Submission?> GetForStudentAsync(
            Guid linkId, Guid studentId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<Submission>> GetForStudentAsync(
            Guid studentId, IReadOnlyCollection<Guid> linkIds, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<Submission>> GetRecentForStudentAsync(
            Guid studentId, int take, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<Submission>> GetByLinkAsync(Guid linkId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<SubmissionToGrade>> GetToGradeAsync(
            IReadOnlyCollection<Guid> courseIds, int take, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<CourseToGradeCount>> GetToGradeCountsAsync(
            IReadOnlyCollection<Guid> courseIds, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<ActiveSubmission>> GetActiveForLinksAsync(
            IReadOnlyCollection<Guid> linkIds, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<bool> IsTeacherOfLinkAsync(
            Guid linkId, Guid userId, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => throw new NotSupportedException();

        public void AddRange(IEnumerable<Submission> submissions) =>
            throw new NotSupportedException();

        public void Remove(Submission submission) => throw new NotSupportedException();

        public void AddFiles(IEnumerable<SubmissionFile> files) =>
            throw new NotSupportedException();

        public void RemoveFiles(IEnumerable<SubmissionFile> files) =>
            throw new NotSupportedException();

        public void AddGrade(Grade grade) => throw new NotSupportedException();

        public Task<List<StudentRepoContext>> GetAllReposAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }

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

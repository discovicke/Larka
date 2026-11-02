using Backend.Application.Abstractions;
using Backend.Application.Results;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests;

public class GitHubLinkingServiceTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task LinkAsync_SetsGitHubLogin_OnCurrentUser()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await TestData.AddUserAsync(db, role: UserRole.Student);

        var currentUser = new StubCurrentUser(user.Id);
        var service = CreateService(scope, currentUser);

        var result = await service.LinkAsync("test-code-1", CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal("github-test-code-1", result.Value);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Equal("github-test-code-1", updated.GitHubLogin);
    }

    [Fact]
    public async Task LinkAsync_InvitesToExistingReposAfterSavingLogin()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await TestData.AddUserAsync(db, role: UserRole.Student);
        var provisioner = new RecordingLabProvisioner();
        var service = CreateService(scope, new StubCurrentUser(user.Id), provisioner);

        var result = await service.LinkAsync("invite-code", CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.NotNull(provisioner.Invited);
        Assert.Equal(user.Id, provisioner.Invited.Id);
        Assert.Equal("github-invite-code", provisioner.Invited.GitHubLogin);
    }

    [Fact]
    public async Task LinkAsync_WhenAlreadyTakenByAnotherUser_ReturnsConflict()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var otherUser = await TestData.AddUserAsync(db, role: UserRole.Student);
        otherUser.GitHubLogin = "github-shared";
        await db.SaveChangesAsync();

        var currentUserEntity = await TestData.AddUserAsync(db, role: UserRole.Student);
        var currentUser = new StubCurrentUser(currentUserEntity.Id);
        var service = CreateService(scope, currentUser);

        var result = await service.LinkAsync("shared", CancellationToken.None);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("redan kopplat", result.Error);
    }

    [Fact]
    public async Task UnlinkAsync_ClearsGitHubLogin()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await TestData.AddUserAsync(db, role: UserRole.Student);
        user.GitHubLogin = "github-to-remove";
        await db.SaveChangesAsync();

        var currentUser = new StubCurrentUser(user.Id);
        var provisioner = new RecordingLabProvisioner();
        var service = CreateService(scope, currentUser, provisioner);

        var result = await service.UnlinkAsync(CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(user.Id, provisioner.Revoked?.Id);
        var updated = await db.Users.FindAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.GitHubLogin);
    }

    private static GitHubLinkingService CreateService(
        IServiceScope scope,
        ICurrentUser currentUser,
        ILabProvisioner? provisioner = null)
    {
        var oauth = scope.ServiceProvider.GetRequiredService<IGitHubOAuthService>();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return new GitHubLinkingService(
            oauth,
            users,
            currentUser,
            provisioner ?? scope.ServiceProvider.GetRequiredService<ILabProvisioner>(),
            unitOfWork,
            NullLogger<GitHubLinkingService>.Instance);
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
    }

    private sealed class RecordingLabProvisioner : ILabProvisioner
    {
        public User? Invited { get; private set; }

        public User? Revoked { get; private set; }

        public Task<IReadOnlyList<StudentRepo>> ProvisionAsync(
            string templateRepo,
            string assignmentTitle,
            string? repoNamePattern,
            IReadOnlyList<RepoRequest> requests,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<StudentRepo> ProvisionOneAsync(
            RepoProvisioningContext context, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<RepoLockResult> LockAtDeadlineAsync(
            StudentRepoContext context, DateTimeOffset deadline, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task ReopenForResubmissionAsync(
            StudentRepoContext context, CancellationToken ct) =>
            Task.CompletedTask;

        public Task RefreshCheckStatusAsync(
            IReadOnlyList<StudentRepoContext> repos, CancellationToken ct) =>
            Task.CompletedTask;

        public Task InviteToExistingReposAsync(User student, CancellationToken ct)
        {
            Invited = student;
            return Task.CompletedTask;
        }

        public Task RevokeAccessAsync(User student, CancellationToken ct)
        {
            Revoked = student;
            return Task.CompletedTask;
        }

        public Task AddRepoCollaboratorAsync(
            StudentRepo repo, Guid schoolId, User student, CancellationToken ct) =>
            Task.CompletedTask;

        public Task RemoveRepoCollaboratorAsync(
            StudentRepo repo, Guid schoolId, User student, CancellationToken ct) =>
            Task.CompletedTask;
    }
}

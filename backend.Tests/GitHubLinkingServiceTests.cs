using Backend.Application.Abstractions;
using Backend.Application.Results;
using Backend.Application.Services;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

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
        var service = CreateService(scope, currentUser);

        var result = await service.UnlinkAsync(CancellationToken.None);

        Assert.Equal(ResultStatus.Ok, result.Status);
        var updated = await db.Users.FindAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.GitHubLogin);
    }

    private static GitHubLinkingService CreateService(IServiceScope scope, ICurrentUser currentUser)
    {
        var oauth = scope.ServiceProvider.GetRequiredService<IGitHubOAuthService>();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return new GitHubLinkingService(oauth, users, currentUser, unitOfWork);
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
    }
}

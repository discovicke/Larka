using Backend.Application.Abstractions;
using Backend.Application.Results;

namespace Backend.Application.Services;

public class GitHubLinkingService(
    IGitHubOAuthService oauth,
    IUserRepository users,
    ICurrentUser currentUser,
    ILabProvisioner provisioner,
    IUnitOfWork unitOfWork,
    ILogger<GitHubLinkingService> logger)
{
    public bool IsOAuthConfigured => oauth.IsConfigured;

    public string BuildAuthorizeUrl(string state, string? redirectUri = null) =>
        oauth.BuildAuthorizeUrl(state, redirectUri);

    public async Task<string?> GetLinkStatusAsync(CancellationToken ct)
    {
        var user = await users.GetAsync(currentUser.UserId, ct);
        return user?.GitHubLogin;
    }

    public Task<ServiceResult<string>> LinkAsync(string code, CancellationToken ct) =>
        LinkForUserAsync(currentUser.UserId, code, ct);

    public async Task<ServiceResult<string>> LinkForUserAsync(Guid userId, string code, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct);
        if (user is null)
        {
            return ServiceResult<string>.NotFound("user not found");
        }

        string login;
        try
        {
            login = await oauth.ExchangeCodeForLoginAsync(code, ct);
        }
        catch (GitHubApiException exception)
        {
            return ServiceResult<string>.Invalid(exception.Message);
        }

        var allUsers = await users.GetAllAsync(ct);
        var existingUser = allUsers.FirstOrDefault(existing =>
            string.Equals(existing.GitHubLogin, login, StringComparison.OrdinalIgnoreCase) && existing.Id != user.Id);
        if (existingUser is not null)
        {
            return ServiceResult<string>.Conflict("Detta GitHub-konto är redan kopplat till en annan användare.");
        }

        user.GitHubLogin = login;
        await unitOfWork.SaveChangesAsync(ct);

        // Repon kan ha skapats innan eleven kopplade kontot. Bjud in till dem nu, men låt en
        // GitHub-tillfällighet inte göra själva kontokopplingen misslyckad.
        try
        {
            await provisioner.InviteToExistingReposAsync(user, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Could not invite {GitHubLogin} to existing repositories",
                login);
        }

        return ServiceResult<string>.Ok(login);
    }

    public Task<ServiceResult<bool>> UnlinkAsync(CancellationToken ct) =>
        UnlinkForUserAsync(currentUser.UserId, ct);

    public async Task<ServiceResult<bool>> UnlinkForUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct);
        if (user is null)
        {
            return ServiceResult<bool>.NotFound("user not found");
        }

        try
        {
            await provisioner.RevokeAccessAsync(user, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // En GitHub-tillfällighet ska inte hindra eleven från att koppla från lokalt.
            logger.LogWarning(
                exception,
                "Could not revoke GitHub access for {GitHubLogin}",
                user.GitHubLogin);
        }

        user.GitHubLogin = null;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }
}

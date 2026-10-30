using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Infrastructure.GitHub;
using Microsoft.Extensions.Options;

namespace Backend.Application.Services;

/// <summary>
/// Mallrepon för lärarens GitHub-sektion. Använder skolans installation och fallbacken för lokal
/// utveckling, men returnerar bara sanerad metadata.
/// </summary>
public class GitHubTemplateService(
    IGitHubAppService github,
    ISchoolGitHubInstallationRepository installations,
    IUserRepository users,
    IPermissions permissions,
    ICurrentUser currentUser,
    IOptions<GitHubOptions> options)
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<ServiceResult<List<GitHubTemplateRepoDto>>> GetTemplatesAsync(
        CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<List<GitHubTemplateRepoDto>>.Forbidden();
        }

        var connection = await ResolveInstallationAsync(ct);
        if (connection.Error is not null)
        {
            return ServiceResult<List<GitHubTemplateRepoDto>>.Invalid(connection.Error);
        }

        try
        {
            var repositories = await github.ListTemplateRepositoriesAsync(
                connection.InstallationId!.Value, ct);
            return ServiceResult<List<GitHubTemplateRepoDto>>.Ok(
                repositories
                    .Select(repository => new GitHubTemplateRepoDto(
                        repository.FullName,
                        repository.HtmlUrl,
                        repository.DefaultBranch,
                        repository.IsPrivate,
                        repository.IsArchived))
                    .ToList());
        }
        catch (GitHubApiException exception)
        {
            return ServiceResult<List<GitHubTemplateRepoDto>>.Invalid(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ServiceResult<List<GitHubTemplateRepoDto>>.Invalid(exception.Message);
        }
    }

    public async Task<ServiceResult<List<string>>> GetTestsAsync(
        string owner, string repo, CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<List<string>>.Forbidden();
        }

        var connection = await ResolveInstallationAsync(ct);
        if (connection.Error is not null)
        {
            return ServiceResult<List<string>>.Invalid(connection.Error);
        }

        try
        {
            return ServiceResult<List<string>>.Ok(
                (await github.ListWorkflowNamesAsync(
                    connection.InstallationId!.Value, owner, repo, ct)).ToList());
        }
        catch (GitHubApiException exception)
        {
            return ServiceResult<List<string>>.Invalid(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ServiceResult<List<string>>.Invalid(exception.Message);
        }
    }

    public async Task<ServiceResult<GitHubTemplateCommitDto>> GetLatestCommitAsync(
        string owner, string repo, CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<GitHubTemplateCommitDto>.Forbidden();
        }

        var connection = await ResolveInstallationAsync(ct);
        if (connection.Error is not null)
        {
            return ServiceResult<GitHubTemplateCommitDto>.Invalid(connection.Error);
        }

        try
        {
            var commit = await github.GetLatestCommitAsync(
                connection.InstallationId!.Value, owner, repo, ct);
            return commit is null
                ? ServiceResult<GitHubTemplateCommitDto>.NotFound("template repo has no commits")
                : ServiceResult<GitHubTemplateCommitDto>.Ok(
                    new GitHubTemplateCommitDto(commit.Sha, commit.Message, commit.AuthoredAt));
        }
        catch (GitHubApiException exception)
        {
            return ServiceResult<GitHubTemplateCommitDto>.Invalid(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ServiceResult<GitHubTemplateCommitDto>.Invalid(exception.Message);
        }
    }

    private async Task<(long? InstallationId, string? Error)> ResolveInstallationAsync(
        CancellationToken ct)
    {
        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return (null, "Användaren hittades inte.");
        }

        var saved = await installations.GetForSchoolAsync(user.SchoolId, ct);
        if (saved is not null)
        {
            if (!saved.Connected)
            {
                return (null, "Skolans GitHub-anslutning är frånkopplad.");
            }

            if (!saved.Enabled)
            {
                return (null, "GitHub är avstängt för skolan.");
            }

            return (saved.InstallationId, null);
        }

        // Dev-fallback tills skolan har kopplat via Inställningar.
        if (_options.HasLegacyInstallation)
        {
            return (_options.InstallationId, null);
        }

        return (null, "Skolan har ingen GitHub-anslutning.");
    }
}

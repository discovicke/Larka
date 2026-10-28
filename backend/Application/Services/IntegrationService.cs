using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;

namespace Backend.Application.Services;

// Läser GitHub-anslutningen för skolans admin. Behörigheten prövas här, inte i klienten.
public class IntegrationService(
    IPermissions permissions,
    IGitHubConnectionStatusProvider githubStatus)
{
    public async Task<ServiceResult<GitHubConnectionDto>> GetGitHubStatusAsync(CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GitHubConnectionDto>.Forbidden();
        }

        var status = await githubStatus.GetStatusAsync(ct);
        return ServiceResult<GitHubConnectionDto>.Ok(new GitHubConnectionDto(
            status.Configured,
            status.AppId,
            status.InstallationId,
            status.Org,
            status.ApiBaseUrl,
            status.Connected,
            status.AccountLogin,
            status.LastVerifiedAt,
            status.Error,
            status.MissingSettings.ToList()));
    }
}

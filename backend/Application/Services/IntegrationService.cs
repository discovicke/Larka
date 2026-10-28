using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

// Hanterar GitHub-anslutningen för skolans admin. Behörigheten prövas här, inte i klienten.
public class IntegrationService(
    IPermissions permissions,
    IGitHubConnectionStatusProvider githubStatus,
    ISchoolGitHubInstallationRepository installations,
    IUserRepository users,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<ServiceResult<GitHubConnectionDto>> GetGitHubStatusAsync(CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GitHubConnectionDto>.Forbidden();
        }

        var status = await githubStatus.GetStatusAsync(ct);
        var user = await users.GetAsync(currentUser.UserId, ct);
        var connected = status.Connected;

        if (user is not null)
        {
            var installation = await installations.GetForSchoolAsync(user.SchoolId, ct);
            if (installation is not null && !installation.Connected)
            {
                connected = false;
            }
        }

        return ServiceResult<GitHubConnectionDto>.Ok(new GitHubConnectionDto(
            status.Configured,
            status.AppId,
            status.InstallationId,
            status.Org,
            status.ApiBaseUrl,
            connected,
            status.AccountLogin,
            status.LastVerifiedAt,
            status.Error,
            status.MissingSettings.ToList()));
    }

    public async Task<ServiceResult<GitHubConnectionDto>> ConnectAsync(CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GitHubConnectionDto>.Forbidden();
        }

        var status = await githubStatus.GetStatusAsync(ct);
        if (!status.Configured)
        {
            return ServiceResult<GitHubConnectionDto>.Invalid("GitHub-integrationen är inte konfigurerad.");
        }

        if (!status.Connected)
        {
            return ServiceResult<GitHubConnectionDto>.Invalid(
                status.Error ?? "Kunde inte ansluta till GitHub-organisationen.");
        }

        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return ServiceResult<GitHubConnectionDto>.NotFound("Användaren hittades inte.");
        }

        var installation = await installations.GetForSchoolAsync(user.SchoolId, ct);
        var now = time.GetUtcNow();
        if (installation is null)
        {
            installation = new SchoolGitHubInstallation
            {
                SchoolId = user.SchoolId,
                InstallationId = status.InstallationId,
                Org = status.Org,
                Connected = true,
                ConnectedAt = now,
                DisconnectedAt = null
            };
            installations.Add(installation);
        }
        else
        {
            installation.Connected = true;
            installation.InstallationId = status.InstallationId;
            installation.Org = status.Org;
            installation.ConnectedAt = now;
            installation.DisconnectedAt = null;
        }

        await unitOfWork.SaveChangesAsync(ct);

        return ServiceResult<GitHubConnectionDto>.Ok(new GitHubConnectionDto(
            status.Configured,
            status.AppId,
            status.InstallationId,
            status.Org,
            status.ApiBaseUrl,
            true,
            status.AccountLogin,
            status.LastVerifiedAt,
            status.Error,
            status.MissingSettings.ToList()));
    }

    public async Task<ServiceResult<bool>> DisconnectAsync(CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return ServiceResult<bool>.NotFound("Användaren hittades inte.");
        }

        var status = await githubStatus.GetStatusAsync(ct);
        var installation = await installations.GetForSchoolAsync(user.SchoolId, ct);
        var now = time.GetUtcNow();
        if (installation is null)
        {
            installation = new SchoolGitHubInstallation
            {
                SchoolId = user.SchoolId,
                InstallationId = status.InstallationId,
                Org = status.Org,
                Connected = false,
                ConnectedAt = now,
                DisconnectedAt = now
            };
            installations.Add(installation);
        }
        else
        {
            installation.Connected = false;
            installation.DisconnectedAt = now;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }
}

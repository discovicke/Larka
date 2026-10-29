using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;

namespace Backend.Application.Services;

// Hanterar GitHub-anslutningen för skolans admin. Behörigheten prövas här, inte i klienten.
public class IntegrationService(
    IPermissions permissions,
    IGitHubConnectionService github,
    IGitHubInstallStateProtector stateProtector,
    IUserRepository users,
    ICurrentUser currentUser)
{
    public async Task<ServiceResult<GitHubConnectionDto>> GetGitHubStatusAsync(CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GitHubConnectionDto>.Forbidden();
        }

        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return ServiceResult<GitHubConnectionDto>.NotFound("Användaren hittades inte.");
        }

        var status = await github.GetStatusAsync(user.SchoolId, ct);
        return ServiceResult<GitHubConnectionDto>.Ok(ToDto(status));
    }

    /// <summary>
    /// Klienten öppnar GitHub-installationen i webbläsaren. State binder flödet till admin och skola.
    /// </summary>
    public async Task<ServiceResult<GitHubInstallLinkDto>> CreateInstallLinkAsync(CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GitHubInstallLinkDto>.Forbidden();
        }

        if (!github.IsConfigured)
        {
            return ServiceResult<GitHubInstallLinkDto>.Invalid(
                "GitHub är inte konfigurerat. Saknas: " + string.Join(", ", github.MissingSettings) + ".");
        }

        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return ServiceResult<GitHubInstallLinkDto>.NotFound("Användaren hittades inte.");
        }

        var state = stateProtector.Create(user.Id, user.SchoolId);
        try
        {
            return ServiceResult<GitHubInstallLinkDto>.Ok(
                new GitHubInstallLinkDto(github.BuildInstallUrl(state), state));
        }
        catch (InvalidOperationException exception)
        {
            return ServiceResult<GitHubInstallLinkDto>.Invalid(exception.Message);
        }
    }

    /// <summary>
    /// Efter GitHubs setup-redirect skickar klienten installation-id och state. Utan installation-id
    /// verifieras skolans sparade installation i stället, så att en lokal frånkoppling kan återställas.
    /// </summary>
    public async Task<ServiceResult<GitHubConnectionDto>> ConnectAsync(
        GitHubConnectRequest request, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GitHubConnectionDto>.Forbidden();
        }

        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return ServiceResult<GitHubConnectionDto>.NotFound("Användaren hittades inte.");
        }

        if (request.InstallationId is null or <= 0)
        {
            var reconnected = await github.ReconnectAsync(user.SchoolId, ct);
            return reconnected.Connected
                ? ServiceResult<GitHubConnectionDto>.Ok(ToDto(reconnected))
                : ServiceResult<GitHubConnectionDto>.Invalid(
                    reconnected.Error ?? "Kunde inte återansluta GitHub.");
        }

        if (string.IsNullOrWhiteSpace(request.State)
            || !stateProtector.TryValidate(request.State, user.Id, out var stateSchoolId)
            || stateSchoolId != user.SchoolId)
        {
            return ServiceResult<GitHubConnectionDto>.Invalid(
                "GitHub-anslutningen kunde inte verifieras. Försök igen från Inställningar.");
        }

        var status = await github.ConnectAsync(user.SchoolId, request.InstallationId.Value, ct);
        return status.Connected
            ? ServiceResult<GitHubConnectionDto>.Ok(ToDto(status))
            : ServiceResult<GitHubConnectionDto>.Invalid(
                status.Error ?? "Kunde inte ansluta GitHub-installationen.");
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

        await github.DisconnectAsync(user.SchoolId, ct);
        return ServiceResult<bool>.Ok(true);
    }

    private static GitHubConnectionDto ToDto(GitHubConnectionStatus status) =>
        new(
            status.Configured,
            status.AppId,
            status.InstallationId,
            status.Org,
            status.ApiBaseUrl,
            status.Connected,
            status.AccountLogin,
            status.LastVerifiedAt,
            status.Error,
            status.MissingSettings.ToList(),
            status.UsingLegacyInstallation);
}

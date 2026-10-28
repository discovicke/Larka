namespace Backend.Application.Abstractions;

/// <summary>
/// Sanerad GitHub-status för klienten. Innehåller bara sådant som får visas för en admin:
/// app-id, org, installation och om anslutningen gick att verifiera. Aldrig nycklar eller tokens.
/// </summary>
public record GitHubConnectionStatus(
    bool Configured,
    long AppId,
    long InstallationId,
    string Org,
    string ApiBaseUrl,
    bool Connected,
    string? AccountLogin,
    DateTimeOffset? LastVerifiedAt,
    string? Error,
    IReadOnlyList<string> MissingSettings);

public interface IGitHubConnectionStatusProvider
{
    Task<GitHubConnectionStatus> GetStatusAsync(CancellationToken ct);
}

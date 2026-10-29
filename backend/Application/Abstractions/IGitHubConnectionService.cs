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
    IReadOnlyList<string> MissingSettings,
    bool UsingLegacyInstallation,
    bool Enabled);

/// <summary>
/// Skolans GitHub-anslutning. App-credentials är gemensamma för hela plattformen, medan installation
/// och organisation sparas per skola. Tjänsten verifierar alltid mot GitHub innan den sparar.
/// </summary>
public interface IGitHubConnectionService
{
    bool IsConfigured { get; }

    IReadOnlyList<string> MissingSettings { get; }

    /// <summary>GitHub App-installationslänk. state skickas med tillbaka till setup-URL:en.</summary>
    string BuildInstallUrl(string state);

    Task<GitHubConnectionStatus> GetStatusAsync(Guid schoolId, CancellationToken ct);

    /// <summary>Verifierar en installation och sparar den på skolan.</summary>
    Task<GitHubConnectionStatus> ConnectAsync(
        Guid schoolId, long installationId, CancellationToken ct);

    /// <summary>Verifierar om skolans sparade installation fortfarande fungerar.</summary>
    Task<GitHubConnectionStatus> ReconnectAsync(Guid schoolId, CancellationToken ct);

    /// <summary>Pausar/aktiverar GitHub utan att koppla från installationen.</summary>
    Task<GitHubConnectionStatus> SetEnabledAsync(
        Guid schoolId, bool enabled, CancellationToken ct);

    /// <summary>Kopplar från lokalt. Appen är kvar på GitHub och befintliga repon påverkas inte.</summary>
    Task DisconnectAsync(Guid schoolId, CancellationToken ct);
}

/// <summary>Signerad state för att binda en GitHub-installation till rätt användare och skola.</summary>
public interface IGitHubInstallStateProtector
{
    string Create(Guid userId, Guid schoolId);

    bool TryValidate(string state, Guid userId, out Guid schoolId);

    /// <summary>Läser state utan att känna till användaren. Används av GitHubs setup-callback.</summary>
    bool TryValidate(string state, out Guid userId, out Guid schoolId);
}

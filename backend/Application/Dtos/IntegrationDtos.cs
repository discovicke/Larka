namespace Backend.Application.Dtos;

/// <summary>GitHub-anslutningen som admin kan se i klienten. Inga hemligheter.</summary>
public record GitHubConnectionDto(
    bool Configured,
    long AppId,
    long InstallationId,
    string Org,
    string ApiBaseUrl,
    bool Connected,
    string? AccountLogin,
    DateTimeOffset? LastVerifiedAt,
    string? Error,
    List<string> MissingSettings,
    bool UsingLegacyInstallation);

/// <summary>Installationslänk och den signerade state som klienten skickar tillbaka efter setup.</summary>
public record GitHubInstallLinkDto(string Url, string State);

/// <summary>
/// installationId sätts efter GitHubs setup-redirect. Utan id verifierar servern om skolans sparade
/// installation fortfarande fungerar (återanslut).
/// </summary>
public record GitHubConnectRequest(long? InstallationId, string? State);

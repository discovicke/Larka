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
    List<string> MissingSettings);

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
    bool UsingLegacyInstallation,
    bool Enabled,
    int StudentCount,
    int LinkedStudentCount,
    int RepoCount);

/// <summary>Installationslänk och den signerade state som klienten skickar tillbaka efter setup.</summary>
public record GitHubInstallLinkDto(string Url, string State);

/// <summary>
/// installationId sätts efter GitHubs setup-redirect. Utan id verifierar servern om skolans sparade
/// installation fortfarande fungerar (återanslut).
/// </summary>
public record GitHubConnectRequest(long? InstallationId, string? State);

public record GitHubEnabledRequest(bool Enabled);

/// <summary>Ett mallrepo som läraren kan välja. Innehåller inga tokens eller hemligheter.</summary>
public record GitHubTemplateRepoDto(
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    bool IsPrivate,
    bool IsArchived);

public record GitHubTemplateCommitDto(string Sha, string Message, DateTimeOffset? AuthoredAt);

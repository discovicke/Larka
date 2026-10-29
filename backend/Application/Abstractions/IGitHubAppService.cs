using System.Net;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

/// <summary>Ett GitHub-repo som appen ser. FullName är "owner/name".</summary>
public record GitHubRepository(
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    bool IsPrivate,
    bool IsTemplate,
    bool IsArchived);

/// <summary>Senaste commit på repots default branch.</summary>
public record GitHubCommit(string Sha, string Message, DateTimeOffset? AuthoredAt);

/// <summary>
/// Installationen som appen är installerad på. AccountLogin är org eller användare, AccountType är
/// "Organization" eller "User". Permissions är GitHubs behörighetskarta, t.ex. contents=write.
/// </summary>
public record GitHubInstallation(
    long Id,
    string AccountLogin,
    string AccountType,
    long AppId,
    IReadOnlyDictionary<string, string> Permissions);

/// <summary>
/// En kortlivad token för en installation. Token är en hemlighet och ska aldrig loggas.
/// ToString är avsiktligt redigerad så att den inte läcker token om objektet loggas.
/// </summary>
public sealed class GitHubInstallationToken(string token, DateTimeOffset expiresAt)
{
    public string Token { get; } = token;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    public override string ToString() => $"GitHubInstallationToken(expires {ExpiresAt:O})";
}

/// <summary>Ett fel från GitHub API. ResponseBody innehåller GitHubs felmeddelande, inte vår token.</summary>
public sealed class GitHubApiException(HttpStatusCode statusCode, string responseBody)
    : Exception($"GitHub API returned {(int)statusCode} ({statusCode}).")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;
}

/// <summary>
/// GitHub App REST API. Installationer är per skola, så metoderna tar installation id i stället för att
/// läsa ett enda globalt id. Det gör att samma tjänst kan användas av flera skolor i phase 2.
/// </summary>
public interface IGitHubAppService
{
    Task<GitHubInstallationToken> GetInstallationTokenAsync(long installationId, CancellationToken ct);

    Task<GitHubInstallation> GetInstallationAsync(long installationId, CancellationToken ct);

    Task<IReadOnlyList<GitHubRepository>> ListRepositoriesAsync(long installationId, CancellationToken ct);

    Task<IReadOnlyList<GitHubRepository>> ListTemplateRepositoriesAsync(long installationId, CancellationToken ct);

    Task<GitHubRepository> GetRepositoryAsync(
        long installationId, string owner, string name, CancellationToken ct);

    Task<GitHubCommit?> GetLatestCommitAsync(
        long installationId, string owner, string name, CancellationToken ct);

    /// <summary>Sammanfattar check-runs för en commit: Pending, Green eller Red.</summary>
    Task<CheckStatus> GetCheckStatusAsync(
        long installationId, string owner, string name, string reference, CancellationToken ct);

    Task<GitHubRepository> CreateRepositoryFromTemplateAsync(
        long installationId,
        string templateOwner,
        string templateRepo,
        string owner,
        string name,
        bool isPrivate,
        string? description,
        CancellationToken ct);

    Task AddCollaboratorAsync(
        long installationId, string owner, string name, string username, CancellationToken ct);

    Task ArchiveRepositoryAsync(long installationId, string owner, string name, CancellationToken ct);
}

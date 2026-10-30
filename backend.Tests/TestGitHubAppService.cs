using Backend.Application.Abstractions;
using Backend.Domain.Enums;

namespace backend.Tests;

/// <summary>
/// GitHub-klienten i integrationstesterna. Den går aldrig ut på nätet; testerna styr installationen.
/// </summary>
public sealed class TestGitHubAppService : IGitHubAppService
{
    public static IReadOnlyDictionary<string, string> FullPermissions { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["contents"] = "write",
            ["administration"] = "write",
            ["checks"] = "read",
            ["actions"] = "read",
            ["metadata"] = "read"
        };

    public GitHubInstallation? Installation { get; set; } = new(
        5678,
        "skol-org",
        "Organization",
        1234,
        FullPermissions);

    public GitHubApiException? InstallationException { get; set; }

    public int InstallationCalls { get; private set; }

    public Task<GitHubInstallation> GetInstallationAsync(long installationId, CancellationToken ct)
    {
        InstallationCalls++;
        if (InstallationException is not null)
        {
            throw InstallationException;
        }

        return Task.FromResult(Installation ?? throw new NotSupportedException());
    }

    public Task<GitHubInstallationToken> GetInstallationTokenAsync(
        long installationId, CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyList<GitHubRepository>> ListRepositoriesAsync(
        long installationId, CancellationToken ct) => throw new NotSupportedException();

    public List<GitHubRepository> TemplateRepositories { get; set; } = [];

    public Task<IReadOnlyList<GitHubRepository>> ListTemplateRepositoriesAsync(
        long installationId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GitHubRepository>>(TemplateRepositories);

    public Task<GitHubRepository> GetRepositoryAsync(
        long installationId, string owner, string name, CancellationToken ct) =>
        throw new NotSupportedException();

    public GitHubCommit? LatestTemplateCommit { get; set; }

    public GitHubCommitSummary? CommitSummary { get; set; }

    public GitHubCheckSummary CheckSummary { get; set; } = new(CheckStatus.Pending, 0, 0);

    public Task<GitHubCommit?> GetLatestCommitAsync(
        long installationId,
        string owner,
        string name,
        CancellationToken ct,
        DateTimeOffset? until = null) =>
        Task.FromResult(LatestTemplateCommit);

    public Task<GitHubCommitSummary?> GetCommitSummaryAsync(
        long installationId,
        string owner,
        string name,
        CancellationToken ct,
        DateTimeOffset? until = null) =>
        Task.FromResult(CommitSummary);

    public Task<GitHubCheckSummary> GetCheckSummaryAsync(
        long installationId, string owner, string name, string reference, CancellationToken ct) =>
        Task.FromResult(CheckSummary);

    public Task<CheckStatus> GetCheckStatusAsync(
        long installationId, string owner, string name, string reference, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<GitHubRepository> CreateRepositoryFromTemplateAsync(
        long installationId,
        string templateOwner,
        string templateRepo,
        string owner,
        string name,
        bool isPrivate,
        string? description,
        CancellationToken ct) => throw new NotSupportedException();

    public Task AddCollaboratorAsync(
        long installationId, string owner, string name, string username, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task RemoveCollaboratorAsync(
        long installationId, string owner, string name, string username, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task ArchiveRepositoryAsync(
        long installationId, string owner, string name, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task UnarchiveRepositoryAsync(
        long installationId, string owner, string name, CancellationToken ct) =>
        throw new NotSupportedException();
}

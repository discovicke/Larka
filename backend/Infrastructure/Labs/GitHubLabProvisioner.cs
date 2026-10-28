using System.Net;
using System.Text;
using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.GitHub;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Labs;

/// <summary>
/// Riktig GitHub-provisionering: skapar ett privat repo per elev från assignmentens template, bjuder in
/// eleven som collaborator och uppdaterar senaste SHA + check-status. Används när GitHub är konfigurerat;
/// annars används FakeLabProvisioner.
/// </summary>
public sealed class GitHubLabProvisioner(
    IGitHubAppService github,
    IOptions<GitHubOptions> options,
    TimeProvider time,
    ILogger<GitHubLabProvisioner> logger) : ILabProvisioner
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<IReadOnlyList<StudentRepo>> ProvisionAsync(
        string templateRepo, string assignmentTitle, IReadOnlyList<RepoRequest> requests, CancellationToken ct)
    {
        EnsureConfigured();
        var (templateOwner, templateName) = ParseRepo(templateRepo, nameof(templateRepo));
        var owner = string.IsNullOrWhiteSpace(_options.Org)
            ? templateOwner
            : _options.Org;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repos = new List<StudentRepo>(requests.Count);

        foreach (var request in requests)
        {
            var repoName = BuildRepoName(assignmentTitle, request.Student, usedNames);
            var repository = await CreateOrGetRepositoryAsync(
                templateOwner,
                templateName,
                owner,
                repoName,
                assignmentTitle,
                ct);
            var (repoOwner, repoNameFromResponse) = ParseRepo(repository.FullName, nameof(repository.FullName));

            if (!string.IsNullOrWhiteSpace(request.Student.GitHubLogin))
            {
                await AddCollaboratorAsync(
                    repoOwner, repoNameFromResponse, request.Student.GitHubLogin, ct);
            }
            else
            {
                logger.LogWarning(
                    "Student {StudentId} has no GitHub login; {Repo} was created without a collaborator",
                    request.Student.Id,
                    repository.FullName);
            }

            repos.Add(new StudentRepo
            {
                SubmissionId = request.SubmissionId,
                RepoFullName = repository.FullName,
                CheckStatus = CheckStatus.Pending,
                UpdatedAt = time.GetUtcNow()
            });
        }

        return repos;
    }

    public async Task RefreshCheckStatusAsync(IReadOnlyList<StudentRepo> repos, CancellationToken ct)
    {
        EnsureConfigured();

        foreach (var repo in repos)
        {
            try
            {
                var (owner, name) = ParseRepo(repo.RepoFullName, nameof(repo.RepoFullName));
                var latest = await github.GetLatestCommitAsync(_options.InstallationId, owner, name, ct);
                if (latest is null)
                {
                    continue;
                }

                repo.LastSha = latest.Sha;
                repo.CheckStatus = await github.GetCheckStatusAsync(
                    _options.InstallationId, owner, name, latest.Sha, ct);
                repo.UpdatedAt = time.GetUtcNow();
            }
            catch (GitHubApiException exception)
            {
                logger.LogWarning(exception, "Could not refresh GitHub checks for {Repo}", repo.RepoFullName);
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Could not parse GitHub repo name {Repo}", repo.RepoFullName);
            }
        }
    }

    private async Task<GitHubRepository> CreateOrGetRepositoryAsync(
        string templateOwner,
        string templateName,
        string owner,
        string repoName,
        string assignmentTitle,
        CancellationToken ct)
    {
        try
        {
            return await github.CreateRepositoryFromTemplateAsync(
                _options.InstallationId,
                templateOwner,
                templateName,
                owner,
                repoName,
                true,
                $"Labb: {assignmentTitle}",
                ct);
        }
        catch (GitHubApiException exception) when (exception.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            // GitHub svarar 422 om namnet redan finns. En omkörning ska återanvända repot i stället för att fallera.
            logger.LogWarning(exception, "GitHub repo {Owner}/{Repo} already exists; reusing it", owner, repoName);
            return await github.GetRepositoryAsync(_options.InstallationId, owner, repoName, ct);
        }
    }

    private async Task AddCollaboratorAsync(
        string owner, string name, string githubLogin, CancellationToken ct)
    {
        try
        {
            await github.AddCollaboratorAsync(_options.InstallationId, owner, name, githubLogin, ct);
        }
        catch (GitHubApiException exception)
        {
            // Repot finns redan. En misslyckad inbjudan ska inte rulla tillbaka hela utdelningen.
            logger.LogWarning(
                exception,
                "Could not add {GitHubLogin} as collaborator to {Owner}/{Repo}",
                githubLogin,
                owner,
                name);
        }
    }

    private static string BuildRepoName(string assignmentTitle, User student, HashSet<string> usedNames)
    {
        var title = Slug(assignmentTitle, 50, "assignment");
        var studentKey = string.IsNullOrWhiteSpace(student.GitHubLogin)
            ? student.Email.Split('@', 2)[0]
            : student.GitHubLogin;
        var studentSlug = Slug(studentKey, 40, student.Id.ToString("N")[..8]);
        var name = $"{title}-{studentSlug}";

        if (!usedNames.Add(name))
        {
            name = $"{name}-{student.Id.ToString("N")[..8]}";
            usedNames.Add(name);
        }

        return name;
    }

    // GitHub-reponamn: gemener, siffror och bindestreck. Samma titel/elev ska ge samma namn vid omkörning.
    private static string Slug(string value, int maxLength, string fallback)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length == 0)
        {
            slug = fallback;
        }

        return slug.Length > maxLength
            ? slug[..maxLength].TrimEnd('-')
            : slug;
    }

    private static (string Owner, string Name) ParseRepo(string value, string parameterName)
    {
        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"{parameterName} must be owner/repo.");
        }

        return (parts[0], parts[1]);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "GitHub is not configured. Set GitHub:AppId, GitHub:InstallationId, GitHub:Org and a private key.");
        }
    }
}

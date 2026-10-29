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
/// eleven som collaborator och uppdaterar senaste SHA + check-status. Används när appens credentials är
/// konfigurerade; annars används FakeLabProvisioner. Installationen hämtas per skola.
/// </summary>
public sealed class GitHubLabProvisioner(
    IGitHubAppService github,
    ISchoolGitHubInstallationRepository installations,
    ISubmissionRepository submissions,
    IOptions<GitHubOptions> options,
    TimeProvider time,
    ILogger<GitHubLabProvisioner> logger) : ILabProvisioner
{
    private readonly GitHubOptions _options = options.Value;

    private sealed record SchoolConnection(long InstallationId, string Org);

    public async Task<IReadOnlyList<StudentRepo>> ProvisionAsync(
        string templateRepo, string assignmentTitle, IReadOnlyList<RepoRequest> requests, CancellationToken ct)
    {
        EnsureAppConfigured();
        var (templateOwner, templateName) = ParseRepo(templateRepo, nameof(templateRepo));
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connections = new Dictionary<Guid, SchoolConnection>();
        var repos = new List<StudentRepo>(requests.Count);

        foreach (var request in requests)
        {
            var connection = await GetConnectionAsync(request.Student.SchoolId, connections, ct);
            var repoName = BuildRepoName(assignmentTitle, request.Student, usedNames);
            var repository = await CreateOrGetRepositoryAsync(
                connection,
                templateOwner,
                templateName,
                repoName,
                assignmentTitle,
                ct);
            var (repoOwner, repoNameFromResponse) = ParseRepo(repository.FullName, nameof(repository.FullName));

            if (!string.IsNullOrWhiteSpace(request.Student.GitHubLogin))
            {
                await AddCollaboratorAsync(
                    connection, repoOwner, repoNameFromResponse, request.Student.GitHubLogin, ct);
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

    public async Task RefreshCheckStatusAsync(
        IReadOnlyList<StudentRepoContext> repos, CancellationToken ct)
    {
        EnsureAppConfigured();
        var connections = new Dictionary<Guid, SchoolConnection>();

        foreach (var context in repos)
        {
            var repo = context.Repo;
            try
            {
                var connection = await GetConnectionAsync(context.SchoolId, connections, ct);
                var (owner, name) = ParseRepo(repo.RepoFullName, nameof(repo.RepoFullName));
                var latest = await github.GetLatestCommitAsync(
                    connection.InstallationId, owner, name, ct);
                if (latest is null)
                {
                    continue;
                }

                repo.LastSha = latest.Sha;
                repo.CheckStatus = await github.GetCheckStatusAsync(
                    connection.InstallationId, owner, name, latest.Sha, ct);
                repo.UpdatedAt = time.GetUtcNow();
            }
            catch (GitHubApiException exception)
            {
                logger.LogWarning(exception, "Could not refresh GitHub checks for {Repo}", repo.RepoFullName);
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Could not refresh GitHub checks for {Repo}", repo.RepoFullName);
            }
        }
    }

    public async Task InviteToExistingReposAsync(User student, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(student.GitHubLogin) || !_options.IsAppConfigured)
        {
            return;
        }

        var repos = await submissions.GetReposForStudentAsync(student.Id, ct);
        if (repos.Count == 0)
        {
            return;
        }

        try
        {
            var connection = await GetConnectionAsync(
                student.SchoolId, new Dictionary<Guid, SchoolConnection>(), ct);
            foreach (var repo in repos)
            {
                var (owner, name) = ParseRepo(repo.RepoFullName, nameof(repo.RepoFullName));
                await AddCollaboratorAsync(
                    connection, owner, name, student.GitHubLogin, ct);
            }
        }
        catch (InvalidOperationException exception)
        {
            // Kopplingen ska lyckas även om skolan saknar installation. Repona kan bjudas in senare.
            logger.LogWarning(
                exception,
                "Could not invite {GitHubLogin} to existing repositories",
                student.GitHubLogin);
        }
    }

    private async Task<SchoolConnection> GetConnectionAsync(
        Guid schoolId,
        Dictionary<Guid, SchoolConnection> cache,
        CancellationToken ct)
    {
        if (cache.TryGetValue(schoolId, out var cached))
        {
            return cached;
        }

        var saved = await installations.GetForSchoolAsync(schoolId, ct);
        if (saved is not null)
        {
            if (!saved.Connected)
            {
                throw new InvalidOperationException(
                    "Skolans GitHub-anslutning är frånkopplad. Anslut GitHub i Inställningar.");
            }

            if (string.IsNullOrWhiteSpace(saved.Org))
            {
                throw new InvalidOperationException(
                    "Skolans GitHub-anslutning saknar organisation. Anslut GitHub igen i Inställningar.");
            }

            var connection = new SchoolConnection(saved.InstallationId, saved.Org);
            cache[schoolId] = connection;
            return connection;
        }

        // Dev-fallback tills skolan har kopplat via Inställningar. Kan tas bort senare.
        if (_options.HasLegacyInstallation)
        {
            logger.LogWarning(
                "School {SchoolId} has no GitHub connection; using the legacy GitHub:InstallationId/Org settings",
                schoolId);
            var connection = new SchoolConnection(_options.InstallationId, _options.Org);
            cache[schoolId] = connection;
            return connection;
        }

        throw new InvalidOperationException(
            "Skolan har ingen GitHub-anslutning. Anslut GitHub i Inställningar.");
    }

    private async Task<GitHubRepository> CreateOrGetRepositoryAsync(
        SchoolConnection connection,
        string templateOwner,
        string templateName,
        string repoName,
        string assignmentTitle,
        CancellationToken ct)
    {
        try
        {
            return await github.CreateRepositoryFromTemplateAsync(
                connection.InstallationId,
                templateOwner,
                templateName,
                connection.Org,
                repoName,
                true,
                $"Labb: {assignmentTitle}",
                ct);
        }
        catch (GitHubApiException exception) when (exception.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            // GitHub svarar 422 om namnet redan finns. En omkörning ska återanvända repot i stället för att fallera.
            logger.LogWarning(
                exception,
                "GitHub repo {Owner}/{Repo} already exists; reusing it",
                connection.Org,
                repoName);
            return await github.GetRepositoryAsync(
                connection.InstallationId, connection.Org, repoName, ct);
        }
    }

    private async Task AddCollaboratorAsync(
        SchoolConnection connection,
        string owner,
        string name,
        string githubLogin,
        CancellationToken ct)
    {
        try
        {
            await github.AddCollaboratorAsync(
                connection.InstallationId, owner, name, githubLogin, ct);
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

    private void EnsureAppConfigured()
    {
        if (!_options.IsAppConfigured)
        {
            throw new InvalidOperationException(
                "GitHub is not configured. Set GitHub:AppId, GitHub:AppSlug and a private key.");
        }
    }
}

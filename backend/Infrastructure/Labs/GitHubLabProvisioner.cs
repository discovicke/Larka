using System.Net;
using Backend.Application.Abstractions;
using Backend.Application.Services;
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
        string templateRepo,
        string assignmentTitle,
        string? repoNamePattern,
        IReadOnlyList<RepoRequest> requests,
        CancellationToken ct)
    {
        EnsureAppConfigured();
        var (templateOwner, templateName) = ParseRepo(templateRepo, nameof(templateRepo));
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connections = new Dictionary<Guid, SchoolConnection>();
        var repos = new List<StudentRepo>(requests.Count);

        foreach (var request in requests)
        {
            var connection = await GetConnectionAsync(request.Student.SchoolId, connections, ct);
            var repoName = GitHubRepoNaming.ForStudent(
                repoNamePattern, assignmentTitle, request.Student, usedNames);
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
                ProvisioningState = RepoProvisioningState.Ready,
                CheckStatus = CheckStatus.Pending,
                UpdatedAt = time.GetUtcNow()
            });
        }

        return repos;
    }

    public async Task<StudentRepo> ProvisionOneAsync(RepoProvisioningContext context, CancellationToken ct)
    {
        EnsureAppConfigured();
        var repo = context.Repo;
        if (string.IsNullOrWhiteSpace(repo.TemplateRepo))
        {
            throw new InvalidOperationException("Studentrepo saknar mallrepo.");
        }

        var (templateOwner, templateName) = ParseRepo(repo.TemplateRepo, nameof(repo.TemplateRepo));
        var connection = await GetConnectionAsync(
            context.Student.SchoolId, new Dictionary<Guid, SchoolConnection>(), ct);
        var repoName = string.IsNullOrWhiteSpace(repo.DesiredRepoName)
            ? GitHubRepoNaming.ForStudent(
                repo.RepoNamePattern, repo.AssignmentTitle ?? "assignment", context.Student)
            : repo.DesiredRepoName;

        var repository = await CreateOrGetRepositoryAsync(
            connection,
            templateOwner,
            templateName,
            repoName,
            repo.AssignmentTitle ?? "assignment",
            ct);
        var (repoOwner, repoNameFromResponse) = ParseRepo(repository.FullName, nameof(repository.FullName));

        if (!string.IsNullOrWhiteSpace(context.Student.GitHubLogin))
        {
            await AddCollaboratorAsync(
                connection, repoOwner, repoNameFromResponse, context.Student.GitHubLogin, ct);
        }
        else
        {
            logger.LogWarning(
                "Student {StudentId} has no GitHub login; {Repo} was created without a collaborator",
                context.Student.Id,
                repository.FullName);
        }

        var now = time.GetUtcNow();
        repo.RepoFullName = repository.FullName;
        repo.ProvisioningState = RepoProvisioningState.Ready;
        repo.ProvisioningError = null;
        repo.NextProvisioningAttemptAt = null;
        repo.ProvisionedAt ??= now;

        // Spara repots startpunkt så att "inte startat" kan skiljas från elevens första commit.
        try
        {
            var initial = await github.GetCommitSummaryAsync(
                connection.InstallationId, repoOwner, repoNameFromResponse, ct);
            if (initial is not null)
            {
                repo.InitialSha ??= initial.Latest.Sha;
                repo.InitialCommitAt ??= initial.Latest.AuthoredAt;
                repo.LastSha ??= initial.Latest.Sha;
                repo.LastCommitMessage ??= initial.Latest.Message;
                repo.LastCommitAt ??= initial.Latest.AuthoredAt;
                repo.CommitCount = Math.Max(repo.CommitCount, initial.Count);
            }
        }
        catch (GitHubApiException exception)
        {
            logger.LogWarning(exception, "Could not read initial commit for {Repo}", repository.FullName);
        }

        repo.UpdatedAt = now;
        return repo;
    }

    public async Task<RepoLockResult> LockAtDeadlineAsync(
        StudentRepoContext context, DateTimeOffset deadline, CancellationToken ct)
    {
        EnsureAppConfigured();
        var repo = context.Repo;
        if (repo.ProvisioningState != RepoProvisioningState.Ready
            || string.IsNullOrWhiteSpace(repo.RepoFullName))
        {
            return new RepoLockResult(false, null, null, "Repot är inte skapat än.");
        }

        var connection = await GetConnectionAsync(
            context.SchoolId, new Dictionary<Guid, SchoolConnection>(), ct);
        var (owner, name) = ParseRepo(repo.RepoFullName, nameof(repo.RepoFullName));
        var latest = await github.GetCommitSummaryAsync(
            connection.InstallationId, owner, name, ct, deadline);
        if (latest is not null)
        {
            repo.LastSha = latest.Latest.Sha;
            repo.LastCommitMessage = latest.Latest.Message;
            repo.LastCommitAt = latest.Latest.AuthoredAt;
            repo.CommitCount = latest.Count;
            var checks = await github.GetCheckSummaryAsync(
                connection.InstallationId, owner, name, latest.Latest.Sha, ct);
            var statusChanged = repo.CheckStatus != checks.Status;
            repo.CheckStatus = checks.Status;
            if (statusChanged || repo.CheckStatusChangedAt is null)
            {
                repo.CheckStatusChangedAt = time.GetUtcNow();
            }

            repo.ChecksPassed = checks.Passed;
            repo.ChecksTotal = checks.Total;
            await ApplyCheckRunsAsync(repo, checks.Runs, time.GetUtcNow(), ct);
        }

        await github.ArchiveRepositoryAsync(connection.InstallationId, owner, name, ct);

        var lockedAt = time.GetUtcNow();
        repo.SubmissionSha = latest?.Latest.Sha;
        repo.LockedAt = lockedAt;
        repo.LockError = latest is null
            ? "Ingen commit före deadline."
            : null;
        repo.ReopenedAt = null;
        repo.UpdatedAt = lockedAt;
        return new RepoLockResult(
            true, latest?.Latest.Sha, latest?.Latest.AuthoredAt, repo.LockError);
    }

    public async Task ReopenForResubmissionAsync(
        StudentRepoContext context, CancellationToken ct)
    {
        EnsureAppConfigured();
        var repo = context.Repo;
        if (string.IsNullOrWhiteSpace(repo.RepoFullName))
        {
            return;
        }

        var connection = await GetConnectionAsync(
            context.SchoolId, new Dictionary<Guid, SchoolConnection>(), ct);
        var (owner, name) = ParseRepo(repo.RepoFullName, nameof(repo.RepoFullName));
        await github.UnarchiveRepositoryAsync(connection.InstallationId, owner, name, ct);
        repo.LockError = null;
        repo.UpdatedAt = time.GetUtcNow();
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
                if (string.IsNullOrWhiteSpace(repo.RepoFullName))
                {
                    continue;
                }

                var connection = await GetConnectionAsync(context.SchoolId, connections, ct);
                var (owner, name) = ParseRepo(repo.RepoFullName, nameof(repo.RepoFullName));
                var latest = await github.GetCommitSummaryAsync(
                    connection.InstallationId, owner, name, ct);
                if (latest is null)
                {
                    continue;
                }

                var checks = await github.GetCheckSummaryAsync(
                    connection.InstallationId, owner, name, latest.Latest.Sha, ct);
                repo.LastSha = latest.Latest.Sha;
                repo.LastCommitMessage = latest.Latest.Message;
                repo.LastCommitAt = latest.Latest.AuthoredAt;
                repo.CommitCount = latest.Count;
                var statusChanged = repo.CheckStatus != checks.Status;
                repo.CheckStatus = checks.Status;
                if (statusChanged || repo.CheckStatusChangedAt is null)
                {
                    repo.CheckStatusChangedAt = time.GetUtcNow();
                }

                repo.ChecksPassed = checks.Passed;
                repo.ChecksTotal = checks.Total;
                var now = time.GetUtcNow();
                await ApplyCheckRunsAsync(repo, checks.Runs, now, ct);
                repo.UpdatedAt = now;
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

        SchoolConnection connection;
        try
        {
            connection = await GetConnectionAsync(
                student.SchoolId, new Dictionary<Guid, SchoolConnection>(), ct);
        }
        catch (InvalidOperationException exception)
        {
            // Kopplingen ska lyckas även om skolan saknar installation. Repona kan bjudas in senare.
            logger.LogWarning(
                exception,
                "Could not invite {GitHubLogin} to existing repositories",
                student.GitHubLogin);
            return;
        }

        foreach (var repo in repos)
        {
            // Ett repo som fortfarande provisioning-skaps har inget fullständigt namn än.
            if (repo.ProvisioningState != RepoProvisioningState.Ready
                || string.IsNullOrWhiteSpace(repo.RepoFullName))
            {
                continue;
            }

            try
            {
                var (owner, name) = ParseRepo(repo.RepoFullName, nameof(repo.RepoFullName));
                await AddCollaboratorAsync(
                    connection, owner, name, student.GitHubLogin, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not invite {GitHubLogin} to {Repo}",
                    student.GitHubLogin,
                    repo.RepoFullName);
            }
        }
    }

    public async Task RevokeAccessAsync(User student, CancellationToken ct)
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

        SchoolConnection connection;
        try
        {
            connection = await GetConnectionAsync(
                student.SchoolId, new Dictionary<Guid, SchoolConnection>(), ct);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(
                exception,
                "Could not revoke {GitHubLogin} from existing repositories",
                student.GitHubLogin);
            return;
        }

        foreach (var repo in repos)
        {
            if (string.IsNullOrWhiteSpace(repo.RepoFullName))
            {
                continue;
            }

            try
            {
                var (owner, name) = ParseRepo(repo.RepoFullName, nameof(repo.RepoFullName));
                await github.RemoveCollaboratorAsync(
                    connection.InstallationId, owner, name, student.GitHubLogin, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not remove {GitHubLogin} from {Repo}",
                    student.GitHubLogin,
                    repo.RepoFullName);
            }
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

            if (!saved.Enabled)
            {
                throw new InvalidOperationException(
                    "GitHub är avstängt för skolan. Aktivera det i Inställningar.");
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

    private async Task ApplyCheckRunsAsync(
        StudentRepo repo, IReadOnlyList<GitHubCheckRun> runs, DateTimeOffset now, CancellationToken ct)
    {
        await submissions.ReplaceCheckRunsAsync(
            repo.SubmissionId,
            runs.Select(run => new StudentRepoCheckRun
            {
                SubmissionId = repo.SubmissionId,
                ExternalKey = run.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Name = run.Name,
                Status = run.Status,
                Conclusion = run.Conclusion,
                UpdatedAt = now
            }).ToList(),
            ct);
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

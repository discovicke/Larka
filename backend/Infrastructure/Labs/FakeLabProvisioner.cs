using Backend.Application.Abstractions;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Infrastructure.Labs;

// Fake nu: skapar repo-namn + Pending. Byt till Octokit-implementation senare.
public class FakeLabProvisioner(IConfiguration cfg) : ILabProvisioner
{
    private readonly string _org = cfg["GitHub:Org"] ?? "skol-org";

    public Task<IReadOnlyList<StudentRepo>> ProvisionAsync(
        string templateRepo,
        string assignmentTitle,
        string? repoNamePattern,
        IReadOnlyList<RepoRequest> requests,
        CancellationToken ct)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var repos = requests.Select(request =>
        {
            var name = GitHubRepoNaming.ForStudent(
                repoNamePattern, assignmentTitle, request.Student, usedNames);
            return new StudentRepo
            {
                SubmissionId = request.SubmissionId,
                RepoFullName = $"{_org}/{name}",
                ProvisioningState = RepoProvisioningState.Ready,
                InitialSha = "fake-initial-sha",
                InitialCommitAt = now,
                ProvisionedAt = now,
                LastSha = "fake-initial-sha",
                LastCommitAt = now,
                CommitCount = 1,
                CheckStatus = CheckStatus.Pending
            };
        }).ToList();

        return Task.FromResult<IReadOnlyList<StudentRepo>>(repos);
    }

    public Task<StudentRepo> ProvisionOneAsync(
        RepoProvisioningContext context, CancellationToken ct)
    {
        var repo = context.Repo;
        var name = string.IsNullOrWhiteSpace(repo.DesiredRepoName)
            ? GitHubRepoNaming.ForStudent(
                repo.RepoNamePattern, repo.AssignmentTitle ?? "assignment", context.Student)
            : repo.DesiredRepoName;
        var now = DateTimeOffset.UtcNow;
        repo.RepoFullName = $"{_org}/{name}";
        repo.ProvisioningState = RepoProvisioningState.Ready;
        repo.ProvisioningError = null;
        repo.NextProvisioningAttemptAt = null;
        repo.ProvisionedAt ??= now;
        repo.InitialSha ??= "fake-initial-sha";
        repo.InitialCommitAt ??= now;
        repo.LastSha ??= repo.InitialSha;
        repo.LastCommitAt ??= now;
        repo.CommitCount = Math.Max(repo.CommitCount, 1);
        return Task.FromResult(repo);
    }

    public Task<RepoLockResult> LockAtDeadlineAsync(
        StudentRepoContext context, DateTimeOffset deadline, CancellationToken ct)
    {
        var repo = context.Repo;
        var sha = repo.LastSha ?? "fake-submission-sha";
        repo.SubmissionSha = sha;
        repo.LockedAt = DateTimeOffset.UtcNow;
        repo.LockError = null;
        repo.ReopenedAt = null;
        repo.UpdatedAt = repo.LockedAt.Value;
        return Task.FromResult(new RepoLockResult(true, sha, deadline, null));
    }

    public Task ReopenForResubmissionAsync(
        StudentRepoContext context, CancellationToken ct)
    {
        context.Repo.LockError = null;
        context.Repo.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task RefreshCheckStatusAsync(
        IReadOnlyList<StudentRepoContext> repos, CancellationToken ct) =>
        Task.CompletedTask;

    public Task InviteToExistingReposAsync(User student, CancellationToken ct) =>
        Task.CompletedTask;

    public Task RevokeAccessAsync(User student, CancellationToken ct) =>
        Task.CompletedTask;

    public Task AddRepoCollaboratorAsync(
        StudentRepo repo, Guid schoolId, User student, CancellationToken ct) =>
        Task.CompletedTask;

    public Task RemoveRepoCollaboratorAsync(
        StudentRepo repo, Guid schoolId, User student, CancellationToken ct) =>
        Task.CompletedTask;
}

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
        var repos = requests.Select(request =>
        {
            var name = GitHubRepoNaming.ForStudent(
                repoNamePattern, assignmentTitle, request.Student, usedNames);
            return new StudentRepo
            {
                SubmissionId = request.SubmissionId,
                RepoFullName = $"{_org}/{name}",
                ProvisioningState = RepoProvisioningState.Ready,
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
        repo.RepoFullName = $"{_org}/{name}";
        repo.ProvisioningState = RepoProvisioningState.Ready;
        repo.ProvisioningError = null;
        repo.NextProvisioningAttemptAt = null;
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
        repo.UpdatedAt = repo.LockedAt.Value;
        return Task.FromResult(new RepoLockResult(true, sha, deadline, null));
    }

    public Task RefreshCheckStatusAsync(
        IReadOnlyList<StudentRepoContext> repos, CancellationToken ct) =>
        Task.CompletedTask;

    public Task InviteToExistingReposAsync(User student, CancellationToken ct) =>
        Task.CompletedTask;

    public Task RevokeAccessAsync(User student, CancellationToken ct) =>
        Task.CompletedTask;
}

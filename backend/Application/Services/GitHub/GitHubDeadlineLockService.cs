using Backend.Application.Abstractions;
using Backend.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Backend.Application.Services;

/// <summary>
/// Låser elevrepon när uppgiftens deadline har passerat. Senaste commit före deadline sparas som
/// inlämning. Körningen är idempotent eftersom rader med LockedAt redan är undantagna.
/// </summary>
public class GitHubDeadlineLockService(
    ISubmissionRepository submissions,
    ILabProvisioner provisioner,
    IUnitOfWork unitOfWork,
    IOptions<GitHubDeadlineLockOptions> options,
    TimeProvider time,
    ILogger<GitHubDeadlineLockService> logger)
{
    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var now = time.GetUtcNow();
        var repos = await submissions.GetReposToLockAsync(
            now, Math.Max(1, settings.BatchSize), ct);
        var locked = 0;

        foreach (var context in repos)
        {
            try
            {
                var result = await provisioner.LockAtDeadlineAsync(
                    new StudentRepoContext(context.Repo, context.SchoolId),
                    context.Deadline,
                    ct);
                if (!result.Locked)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(result.Sha))
                {
                    var groupSubmissions = context.Repo.AssignmentGroupId is not null
                        ? await submissions.GetSubmissionsForGroupAsync(
                            context.Repo.AssignmentGroupId.Value, ct)
                        : [context.Submission];
                    foreach (var submission in groupSubmissions)
                    {
                        submission.Status = SubmissionStatus.Submitted;
                        submission.SubmittedAt = result.CommitAt ?? context.Deadline;
                    }
                }

                context.Repo.LockError = result.Error;
                context.Repo.UpdatedAt = now;
                locked++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                context.Repo.LockError = SafeError(exception);
                context.Repo.UpdatedAt = now;
                logger.LogWarning(
                    exception,
                    "Could not lock GitHub repo for submission {SubmissionId}",
                    context.Repo.SubmissionId);
            }

            await unitOfWork.SaveChangesAsync(ct);
        }

        return locked;
    }

    private static string SafeError(Exception exception) =>
        exception is InvalidOperationException
            ? exception.Message
            : "Kunde inte låsa GitHub-repot. Försök igen.";
}

using Backend.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Application.Services;

/// <summary>
/// Vid varje deadline (huvud- och kompletteringsdeadlines) låses elevrepon som har en egen commit, och
/// senaste commit före deadline sparas som inlämning. Repon utan egen commit lämnas öppna tills nästa
/// deadline, och efter sista deadline låses alla. Varje deadline behandlas en gång per repo.
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
                    RepoLockPolicy.LocksWithoutOwnCommit(context, now),
                    ct);

                // Stämplar behandlingen så att samma deadline inte tas om, även när repot lämnas öppet.
                context.Repo.UpdatedAt = now;
                if (!result.Locked)
                {
                    await unitOfWork.SaveChangesAsync(ct);
                    continue;
                }

                if (result.OwnCommit && !string.IsNullOrWhiteSpace(result.Sha))
                {
                    var groupSubmissions = context.Repo.AssignmentGroupId is not null
                        ? await submissions.GetSubmissionsForGroupAsync(
                            context.Repo.AssignmentGroupId.Value, ct)
                        : [context.Submission];
                    RepoLockPolicy.MarkSubmitted(groupSubmissions, result.CommitAt ?? context.Deadline);
                }

                context.Repo.LockError = result.Error;
                locked++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                context.Repo.LockError = SafeError(exception);
                // Behåller deadlinen som aktuell så att den provas igen, men efter orörda repon i kön.
                context.Repo.UpdatedAt = context.Deadline;
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

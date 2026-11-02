using Backend.Application.Abstractions;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

/// <summary>
/// Åtgärder som påverkar ett redan skapat elev- eller grupprepo. Håller GitHub-fel och tidsstämplar
/// på ett ställe så att inlämningsflödet slipper känna till provisioneraren.
/// </summary>
public class GitHubRepoActions(
    ILabProvisioner provisioner,
    TimeProvider time,
    ILogger<GitHubRepoActions> logger)
{
    public async Task ReopenAsync(StudentRepo repo, Guid schoolId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        repo.ReopenedAt = now;
        repo.UpdatedAt = now;

        if (string.IsNullOrWhiteSpace(repo.RepoFullName))
        {
            return;
        }

        try
        {
            await provisioner.ReopenForResubmissionAsync(new StudentRepoContext(repo, schoolId), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            repo.LockError = "Repot kunde inte öppnas för komplettering. Öppna det manuellt i GitHub.";
            logger.LogWarning(
                exception,
                "Could not reopen GitHub repo for submission {SubmissionId}",
                repo.SubmissionId);
        }
    }
}

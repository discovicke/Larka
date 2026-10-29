using System.Net;
using Backend.Application.Abstractions;
using Backend.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Backend.Application.Services;

/// <summary>
/// Plockar upp pending/failed repo-rader och skapar dem i GitHub. Ett fel på en elev stoppar inte
/// övriga elever, och raden markeras Failed med backoff så att jobbet kan försöka igen.
/// </summary>
public class GitHubRepoProvisioningService(
    ISubmissionRepository submissions,
    ILabProvisioner provisioner,
    IUnitOfWork unitOfWork,
    IOptions<GitHubProvisioningOptions> options,
    TimeProvider time,
    ILogger<GitHubRepoProvisioningService> logger)
{
    public async Task<int> ProcessPendingAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var now = time.GetUtcNow();
        var pending = await submissions.GetPendingReposAsync(
            now, Math.Max(1, settings.BatchSize), Math.Max(1, settings.MaxAttempts), ct);
        var ready = 0;

        foreach (var context in pending)
        {
            var repo = context.Repo;
            repo.ProvisioningState = RepoProvisioningState.Creating;
            repo.ProvisioningAttempts++;
            repo.NextProvisioningAttemptAt = null;
            repo.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(ct);

            try
            {
                await provisioner.ProvisionOneAsync(context, ct);
                ready++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                repo.ProvisioningState = RepoProvisioningState.Failed;
                repo.ProvisioningError = SafeError(exception);
                repo.NextProvisioningAttemptAt = repo.ProvisioningAttempts >= settings.MaxAttempts
                    ? null
                    : now + Backoff(repo.ProvisioningAttempts);
                repo.UpdatedAt = now;
                logger.LogWarning(
                    exception,
                    "Could not provision GitHub repo for submission {SubmissionId} (attempt {Attempt})",
                    repo.SubmissionId,
                    repo.ProvisioningAttempts);
            }

            await unitOfWork.SaveChangesAsync(ct);
        }

        return ready;
    }

    private static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(900, Math.Pow(2, Math.Max(1, attempts)) * 15));

    private static string SafeError(Exception exception) => exception switch
    {
        InvalidOperationException => exception.Message,
        GitHubApiException github when github.StatusCode == HttpStatusCode.NotFound =>
            "Mallrepot hittades inte eller är inte markerat som template i GitHub.",
        GitHubApiException github when github.StatusCode == HttpStatusCode.Forbidden
            || github.StatusCode == HttpStatusCode.Unauthorized =>
            "GitHub-appen saknar behörighet att skapa repon i organisationen.",
        GitHubApiException github when github.StatusCode == HttpStatusCode.UnprocessableEntity =>
            "GitHub kunde inte skapa repot. Kontrollera att mallrepot är markerat som template och att reponamnet är ledigt.",
        GitHubApiException github =>
            $"GitHub API svarade {(int)github.StatusCode} ({github.StatusCode}).",
        _ => "Kunde inte skapa GitHub-repot. Försök igen."
    };
}

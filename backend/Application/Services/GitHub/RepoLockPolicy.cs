using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

/// <summary>
/// När ett elevrepo räknas som inlämnat vid en deadline. Reglerna bor här och ingen annanstans.
/// </summary>
public static class RepoLockPolicy
{
    /// <summary>
    /// Sant om repot ska låsas även utan egen commit: ingen deadline återstår, eller inlämningen är redan
    /// inlämnad eller bedömd och eleven får därför inte lämna in igen.
    /// </summary>
    public static bool LocksWithoutOwnCommit(RepoLockContext context, DateTimeOffset now)
    {
        // Elevens personliga deadline är en deadline till, så repot står öppet tills även den har passerat.
        var window = new GradingWindow(
            context.DueAt, context.ResubmitDueAt ?? [], now, context.PersonalDueAt);
        return !window.DeadlineRemains
            || context.Submission.Status is SubmissionStatus.Submitted or SubmissionStatus.Graded;
    }

    /// <summary>Markerar inlämningarna som inlämnade. Bedömda inlämningar rörs inte.</summary>
    public static void MarkSubmitted(IEnumerable<Submission> submissions, DateTimeOffset submittedAt)
    {
        foreach (var submission in submissions.Where(item => item.Status != SubmissionStatus.Graded))
        {
            submission.Status = SubmissionStatus.Submitted;
            submission.SubmittedAt = submittedAt;
        }
    }

    /// <summary>
    /// Sant om senaste commit före deadline är elevens egen. Mallens första commit räknas inte, och efter
    /// en begärd komplettering måste det finnas en commit som är nyare än den som lämnades in förra gången.
    /// </summary>
    public static bool HasOwnCommit(
        StudentRepo repo, string sha, int commitCount, DateTimeOffset? authoredAt)
    {
        if (string.IsNullOrWhiteSpace(sha))
        {
            return false;
        }

        if (repo.InitialSha is not null)
        {
            if (string.Equals(sha, repo.InitialSha, StringComparison.Ordinal))
            {
                return false;
            }
        }
        else if (commitCount <= 1)
        {
            // Äldre repon saknar InitialSha. Då räcker bara mallens egen commit inte som elevarbete.
            return false;
        }

        if (repo.ReopenedAt is null)
        {
            return true;
        }

        // Efter en komplettering räknas bara en ny commit. SHA:n jämförs med förra inlämningen eftersom
        // en commit kan vara författad före öppningen men pushad efter den.
        return repo.SubmissionSha is not null
            ? !string.Equals(sha, repo.SubmissionSha, StringComparison.Ordinal)
            : authoredAt is not null && authoredAt > repo.ReopenedAt;
    }
}

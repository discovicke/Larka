using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Regler för när en del eller vecka innehåller sådant som inte får raderas eller flyttas.
// NotStarted skapas när en uppgift delas ut och är inget elevarbete.
public static class StudentWorkPolicy
{
    public static bool HasStudentActivity(Submission submission) =>
        submission.Status != SubmissionStatus.NotStarted
        || submission.SubmittedAt is not null
        || submission.Files.Count > 0
        || submission.Grade is not null
        || HasRepoActivity(submission.Repo);

    // Elevarbete på delen: i en inlämning, eller en markering som klar utan inlämning.
    public static async Task<bool> LinkHasStudentWorkAsync(
        IReadOnlyCollection<Submission> linkSubmissions,
        IPartCompletionRepository completions,
        Guid linkId,
        CancellationToken ct) =>
        linkSubmissions.Any(HasStudentActivity) || await completions.AnyForLinkAsync(linkId, ct);

    // Ett repo skapas när uppgiften delas ut. Det är först när eleven har en egen commit,
    // har lämnat in eller fått en inlämnad commit som repot räknas som elevarbete.
    private static bool HasRepoActivity(StudentRepo? repo)
    {
        if (repo is null)
        {
            return false;
        }

        if (repo.SubmissionSha is not null)
        {
            return true;
        }

        if (repo.InitialSha is not null)
        {
            return repo.LastSha is not null
                && !string.Equals(repo.LastSha, repo.InitialSha, StringComparison.Ordinal);
        }

        if (repo.LastCommitAt is not null && repo.ProvisionedAt is not null)
        {
            return repo.LastCommitAt > repo.ProvisionedAt.Value;
        }

        return repo.CommitCount > 1;
    }

    // En publicerad inlämning (uppgift med betygsskala) får inte raderas innan den avpublicerats.
    public static bool IsPublishedGradedAssignment(Content content)
    {
        if (content.Type != ContentType.Assignment || content.Status != ContentStatus.Published)
        {
            return false;
        }

        return content.Assignment is not null && content.Assignment.GradeScale != GradeScale.None;
    }
}

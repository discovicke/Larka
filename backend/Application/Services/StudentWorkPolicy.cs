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
        || submission.Repo is not null
        || submission.Grade is not null;

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
